using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Fatigue;
using DreamTeam.MatchEngine.Projection;
using DreamTeam.MatchEngine.Randomness;
using DreamTeam.MatchEngine.Ratings;
using DreamTeam.MatchEngine.Rules;
using DreamTeam.MatchEngine.Tactics;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Possession -> action -> shot/foul/turnover/rebound cekirdegi.
///
/// 03'teki sozlesmenin M3 hali: <c>Create</c> / <c>Advance</c> / <c>Simulate</c>.
///
/// H03 geregi offline ve canli yurutme <b>ayni</b> cekirdegi kullanir; burada ikinci
/// bir simulasyon algoritmasi yoktur. Motor duvar saati okumaz, beklemez, dosya
/// yazmaz ve loglamaz.
///
/// <b>Adim siniri (M3'te degisti).</b> M2'de bir <c>Advance</c> bir aksiyonu bastan
/// sona bitiriyordu. M3'te iki arac durum vardir: birakilmis sut
/// (<c>ShotPending</c>) ve devam eden serbest atis serisi (<c>FreeThrows</c>).
/// <c>Advance</c> bir sonraki <b>anlamli sinra</b> ilerler ve bu sinir bir
/// aksiyonu bitirmek zorunda degildir. <c>Simulate</c> dongusu degismez.
///
/// <para><b>Determinism sozlesmesi</b> — her aksiyon su sirayla RNG tuketir:</para>
/// <list type="number">
///   <item><description>aksiyon (1) + oyuncu (1)</description></item>
///   <item><description><b>birincil savunmaci (1) — her zaman</b> (D68)</description></item>
///   <item><description>top kaybi (1)</description></item>
///   <item><description>faul olma (1)</description></item>
///   <item><description>suta donusme (1)</description></item>
///   <item><description>hucre faulu (1) — yalniz faul varsa</description></item>
///   <item><description>shooting faulu (1) — yalniz faul ve sut varsa</description></item>
///   <item><description>cember teması (1) — yalniz sut varsa</description></item>
///   <item><description>blok (1) — yalniz sut varsa</description></item>
///   <item><description>isabet (1) — yalniz sut varsa ve bloklanmadıysa</description></item>
///   <item><description>asist (1) — yalniz isabetli sut varsa</description></item>
///   <item><description>serbest atis (1) — her atis icin</description></item>
///   <item><description>ribaund (1) + ribaund alan (1) — yalniz canli miss sonrasi</description></item>
/// </list>
/// <para><b>M4'te birincil savunmaci cekilisi sabitlendi (D68).</b> M3'te faul
/// atfı icin <c>PickDefender</c> koşula bagli bir cekiliş yaparken blok kendi
/// agirlikli cekilisiyle ayri bir savunmaci seçiyordu. Bu, 05 §127'nin yasakladigi
/// "ayni olayi iki kez örnekleme" desenidir ve faul ile blogun farkli kişilere
/// yazilmasi riskini tasir. M4'te tek cekiliş vardir ve <b>ayni oyuncu</b> faul
/// atfi, blogu ve kalite eslesmesi icin kullanilir. Yan etki: koşul bagimli
/// konum kaymalari biter, cagri sayisi sabit +1 olur.</para>
/// Bu sira degistirilirse tum golden sonuclar degisir; sira testlerle sabitlenir.
/// Blok gerceklesirse isabet cekilisi <b>tuketilmez</b>: ayni sut iki kez
/// orneklenmez (05 §127).
/// </summary>
public sealed class MatchSimulation
{
    public const int EventSchemaVersion = 4;

    private readonly EngineConfig _config;
    private readonly string _configHash;
    private readonly ShotResolver _shotResolver;
    private readonly TurnoverResolver _turnoverResolver;
    private readonly ReboundResolver _reboundResolver;
    private readonly FoulResolver _foulResolver;
    private readonly BlockResolver _blockResolver;
    private readonly RimContactResolver _rimContactResolver;
    private readonly FreeThrowResolver _freeThrowResolver;
    private readonly OffensivePolicy _offense;
    private readonly DefensivePolicy _defense;
    private readonly PlayerRatingCalculator _ratings;
    private readonly TeamRatingCalculator _teamRatings;

    public MatchSimulation(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _configHash = config.ComputeConfigHash();
        _ratings = new PlayerRatingCalculator(config.SelectionSpread);
        _defense = new DefensivePolicy(config.Defense, _ratings);
        _offense = new OffensivePolicy(config.Tactics, config.ActionProfiles, _ratings);
        _teamRatings = TeamRatingCalculator.Baseline;
        _shotResolver = new ShotResolver(config.Shot, config.Fatigue);
        _turnoverResolver = new TurnoverResolver(config.Actions, _defense);
        _reboundResolver = new ReboundResolver(config.Actions, _ratings);
        _foulResolver = new FoulResolver(config.Fouls);
        _blockResolver = new BlockResolver(_defense);
        _rimContactResolver = new RimContactResolver(config.Shot);
        _freeThrowResolver = new FreeThrowResolver(config.FreeThrows);
    }

    /// <summary>M4: takım OVR'si. <b>Gösterim amaçlıdır, çözüm girdisi değildir</b> (T03).</summary>
    public int OverallRating(MatchSetup setup, TeamSide side) =>
        _teamRatings.OverallFor(
            (side == TeamSide.Home ? setup.Home : setup.Away).Team,
            (side == TeamSide.Home ? setup.Home : setup.Away).Lineup);

    public string ConfigHash => _configHash;

    /// <summary>
    /// Dogrulanmis bir setup'tan baslangic durumu uretir.
    ///
    /// <paramref name="setup"/> <see cref="MatchSetupValidator"/> tarafindan kabul
    /// edilmis olmalidir. Dogrulama <b>bu metodun sorumlulugu degildir</b>: kullanici
    /// girdisinin reddi istisnayla degil, <see cref="MatchSetupValidationResult"/> ile
    /// bildirilir (D26). <see cref="Simulate"/> bu sozlesmeyi kendisi uygular;
    /// dogrudan <c>Create</c> cagirana gecersiz setup gecirirse program hatasi
    /// yapmis olur ve bu bir <see cref="InvalidOperationException"/> ile belirtilir.
    /// </summary>
    public MatchState Create(MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var validation = MatchSetupValidator.Validate(setup);

        if (!validation.IsValid)
        {
            var reasons = string.Join(
                "; ",
                validation.Errors.Select(error => $"{error.Field} ({error.Code}): {error.Message}"));

            throw new InvalidOperationException($"Geçersiz MatchSetup reddedildi -> {reasons}");
        }

        var home = BuildTeamState(setup.Home, TeamSide.Home);
        var away = BuildTeamState(setup.Away, TeamSide.Away);

        return new MatchState
        {
            Config = _config,
            Setup = setup,
            Clock = MatchClock.Initial(),
            Phase = MatchPhase.NotStarted,
            Home = home,
            Away = away,
            HomeScore = 0,
            AwayScore = 0,
            Possession = null,
            PendingShot = null,
            PendingFrees = null,
            CommandQueue = CommandQueue.Empty,
            NextSequence = 1,
            NextActionId = 1,
            NextShotId = 1,
            NextTurnoverId = 1,
            NextFoulId = 1,
            NextFTSeriesId = 1,
            TotalActionCount = 0,
            PossessionCount = 0,
            Random = new SeededRandom(setup.Seed),
            Diagnostics = Diagnostics.DiagnosticCounters.Empty,
        };
    }

    /// <summary>
    /// Bir sonraki anlamli sınıra ilerler: periyot başlangıcı, bir aksiyonun
    /// başındaki adım, bırakılmış şutun çözümü veya bir serbest atış.
    ///
    /// <para><b>M5: imza genişledi.</b> 03 §"Offline ile canlı yürütme" hedef
    /// sözleşmesi <c>Advance(MatchState, IReadOnlyList&lt;ScheduledManagerCommand&gt;)</c>.
    /// Komutlar mantıksal sınıra bağlıdır, duvar saatine değil (07 §6).</para>
    ///
    /// <para><b>Parametresiz aşırı yükleme korunur</b> ve <c>CommandList.Empty</c>
    /// ile aynıdır. M2–M4'ün testleri ve canlı kullanicisi bozulmaz; ikinci bir
    /// motor yolu DEĞİLDİR, ayni cekirdek calisir (H03).</para>
    ///
    /// <para><b>Komutlar RNG TÜKETMEZ.</b> Gecersiz komut bile cekilis
    /// harcamaz (07 §5); gecerli komut da harcamaz. M4'ün cagri sirasi
    /// sozlesmesi bozulmaz.</para>
    /// </summary>
    public StepResult Advance(MatchState state) =>
        Advance(state, CommandList.Empty);

    /// <summary>M5: komutlu aşırı yükleme (03 hedef sözleşmesi).</summary>
    public StepResult Advance(
        MatchState state,
        IReadOnlyList<ScheduledManagerCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(commands);

        if (state.IsTerminal)
        {
            // 07 §6 madde 4: mac bittiginde bekleyen komutlar Expired/Rejected
            // olarak sonuclanir. Terminal durumda yeni komut kabul edilmez.
            var (expiredQueue, expiredResults) = state.CommandQueue.ExpireAll(state.NextSequence);

            if (expiredResults.IsEmpty)
            {
                return StepResult.Terminal(state, [], []);
            }

            var terminalStep = new Step(this, state with { CommandQueue = expiredQueue });
            terminalStep.PublishCommandResults(expiredResults);

            return StepResult.Terminal(terminalStep.State, [.. terminalStep.Events], expiredResults);
        }

        var step = new Step(this, state);

        // D95: gelen komutlar KABULunden once son kullanma suresi denetlenir.
        //
        // Sebep: `Simulate` komutu hedef sinira gelene kadar kuyrukta tutar.
        // Bu arada `NextSequence` ilerler ve komutun `ExpiresAfterSequence`
        // degeri asilmis olabilir. Denetim kabulde yapilmazsa komut kuyruga
        // girer, hemen uygulanir (D94) ve `ExpireStaleCommands` onu yakalayamaz;
        // boylece "suresi dolmus" komut yine de etki ederdi.
        var incoming = ImmutableArray.CreateBuilder<ScheduledManagerCommand>(commands.Count);

        foreach (var command in commands)
        {
            if (command.IsExpiredAfter(state.NextSequence))
            {
                step.RejectExpiredOnArrival(command);
                continue;
            }

            incoming.Add(command);
        }

        if (incoming.Count > 0)
        {
            // M5: kabul siralamasi ve idempotency burada cozulur; UYGULAMA
            // asagidaki sinirlarda olur.
            step.AcceptCommands(incoming.ToImmutable());
        }

        // D94: kabul edilen DeadBall/PeriodBreak komutlari, bu adimin
        // basinda sunulan sinir ISE AYNI ADIMDA bosaltilir.
        //
        // Sebep: `Advance(state, commands)` cagirisi "bu komutlar simdi
        // gonderiliyor" demektir. Kabul edilen bir komut, o sunulan sinir
        // bu adimda mevcutsa **beklemez** — aksi halde `Advance`'e verilen
        // komutun etkisi bir sonraki adima kayar ve cagiran, gonderdigini
        // sanma yerine kuyrugu kontrol etmek zorunda kalir.
        //
        // `Simulate` zaten komutlari yalnizca sunulan sinira gore gonderir
        // (bkz. `BoundariesFor`), dolayisiyla bu yol onda da ayni davranir.
        var currentBoundary = CurrentBoundary(state);

        if (currentBoundary is { } boundary)
        {
            step.ApplyAtBoundary(boundary, isDeadBallWindow: boundary is not CommandBoundary.ActionDecision);
        }
        if (state.PendingFrees is not null)
        {
            step.ResolveFreeThrow();
        }
        else if (state.PendingShot is not null)
        {
            step.ResolvePendingShot();
        }
        else if (state.Phase is MatchPhase.NotStarted or MatchPhase.PeriodBreak)
        {
            step.StartPeriod();
        }
        else if (state.Phase == MatchPhase.LiveBall)
        {
            step.RunAction();
        }
        else
        {
            // Ulasilamaz durum: bekleyen sut/seri yok, terminal degil, ama faz
            // tanimli degil. Sessizce 0 event uretmek yerine aninda ve acikca
            // hata vermek, bu turden bir hatayi gizlemez.
            throw new InvalidOperationException(
                $"Beklenmeyen match phase: {state.Phase} "
                + $"(PossessionId={state.Possession?.PossessionId.ToString() ?? "-"}).");
        }

        step.ExpireStaleCommands();
        step.ExpandOnTerminal();

        // M6 (D100): bu adimin sayaclari EN SONDA tek seferde state'e yazilir.
        // Buraya kadar hicbir sayac okunmaz; bu yuzden diagnostics domain sonucunu
        // degistiremez (T15). Adim basina tek immutable kayit yeterlidir.
        step.FlushDiagnostics();

        return StepResult.NonTerminal(step.State, [.. step.Events], [.. step.CommandResults]);
    }

    /// <summary>
    /// <c>Create</c> + <c>Advance</c> dongusunu sonuna kadar calistirir. Konsol,
    /// batch deneyleri ve ileride canli runner hep bu yolu kullanir.
    ///
    /// Setup reddedilirse mac oynanmaz; <c>Aborted</c> sonuc ve gerekce doner.
    ///
    /// <para><b>M5: imza genişledi</b>, parametresiz aşırı yükleme korunur. Ayni
    /// çekirdek çalışır; ikinci bir simülasyon algoritması <b>yazılmaz</b>
    /// (H03).</para>
    /// </summary>
    public MatchResult Simulate(MatchSetup setup) => Simulate(setup, CommandList.Empty);

    /// <summary>
    /// M5: komutlu toplu kosu. Komutlar <b>adim sinirina baglanir</b>: her
    /// komut, hedefledigi mantiksal sinira gelindiginde uygulanir.
    /// </summary>
    /// <param name="commands">
    /// Tum mac boyunca gonderilecek komutlar. Sirasi onemlidir (D86 FIFO).
    /// </param>
    public MatchResult Simulate(
        MatchSetup setup,
        IReadOnlyList<ScheduledManagerCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(commands);

        var validation = MatchSetupValidator.Validate(setup);

        if (!validation.IsValid)
        {
            return AbortedResult(setup, Describe(validation));
        }

        var state = Create(setup);
        var events = new List<MatchEvent>();

        // Kalan komutlar. Her adimda, hedef siniri BU ADIMDA olanlar gonderilir.
        var remaining = ImmutableArray.CreateBuilder<ScheduledManagerCommand>(commands.Count);

        foreach (var command in commands)
        {
            remaining.Add(command);
        }

        var pending = remaining.ToImmutable();

        while (!state.IsTerminal)
        {
            // D92: kaldirma `forThisStep.Length` kadar DEGIL, eslesen
            // komutlarin KENDI indeksleriyle yapilir. Onceki surum
            // `RemoveRange(0, n)` idi; bu, `SelectForStep` filtreledigi icin
            // yanlis komutlari siliyor ve sonraki komutlar bir sonraki
            // adima siziyordu. Tek duz cikti: 1. adimda gonderilen
            // DeadBall substitution'i hic gonderilmemisti ve mac sonunda
            // "Expired" olarak reddediliyordu.
            var sendIndices = new List<int>();

            for (var index = 0; index < pending.Length; index++)
            {
                if (BoundariesFor(state).Contains(pending[index].TargetBoundary))
                {
                    sendIndices.Add(index);
                }
            }

            var batch = ImmutableArray.CreateBuilder<ScheduledManagerCommand>(sendIndices.Count);

            foreach (var index in sendIndices)
            {
                batch.Add(pending[index]);
            }

            var keep = ImmutableArray.CreateBuilder<ScheduledManagerCommand>(
                pending.Length - sendIndices.Count);

            for (var index = 0; index < pending.Length; index++)
            {
                if (!sendIndices.Contains(index))
                {
                    keep.Add(pending[index]);
                }
            }

            pending = keep.ToImmutable();

            var step = Advance(state, batch.ToImmutable());
            state = step.State;
            events.AddRange(step.Events);

            if (step.Events.Length == 0 && !state.IsTerminal)
            {
                // Ilerleme yoksa sonsuz dongu riski olusur. 06 §2: sonsuz dongu
                // skoru bozmaz, maci Aborted yapar.
                return AbortedResult(setup, "Ilerleme yok; motor durdu. Bu bir hata durumudur.", events)
                    with
                    {
                        HomeOverall = OverallRating(setup, TeamSide.Home),
                        AwayOverall = OverallRating(setup, TeamSide.Away),
                    };
            }
        }

        // M4: OVR burada hesaplanir ama motor HICBIR YERDE okumaz (D23/D24).
        // Yalnizca rapor ciktisinda gorunur; T03 bunu kanitlar.
        //
        // M5: timeout ozeti de ayni sekilde yalniz RAPOR icindir.
        return WithTimeoutSummary(
            Project(setup, state, events),
            state) with
        {
            HomeOverall = OverallRating(setup, TeamSide.Home),
            AwayOverall = OverallRating(setup, TeamSide.Away),
        };
    }

    /// <summary>
    /// M5: bu adımda sunulan mantıksal sınırlar.
    ///
    /// <para><b>Neden adımın durumuna göre seçim?</b> 07 §6 komutu <b>mantıksal
    /// sınıra** bağlar. Bir adım ya <c>StartPeriod</c>, <c>RunAction</c>,
    /// <c>ResolvePendingShot</c> ya da <c>ResolveFreeThrow</c>'tır. Yalnız o
    /// an sunulan sınırlar komut kabul eder; diğerleri bir sonraki adıma
    /// kalır. Böylece komut <b>duvar saatine değil, mantığa** bağlı kalır.</para>
    /// </summary>
    private static IReadOnlyList<CommandBoundary> BoundariesFor(MatchState state)
    {
        var current = CurrentBoundary(state);

        return current is null ? [] : [current.Value];
    }

    /// <summary>
    /// M5: bu adım sunan mantıksal sınır. <c>null</c> ise hiçbir sınır sunulmuyor
    /// (ör. bekleyen şutun çözümü — top hâlâ canlı, savunma atfında hiçbir şey
    /// değişmez, 06 §7).
    /// </summary>
    private static CommandBoundary? CurrentBoundary(MatchState state)
    {
        if (state.PendingFrees is not null)
        {
            // Serbest atis serisinin SONUCU dead-ball'dir.
            return CommandBoundary.DeadBall;
        }

        if (state.PendingShot is not null)
        {
            return null;
        }

        if (state.Phase is MatchPhase.NotStarted or MatchPhase.PeriodBreak)
        {
            return CommandBoundary.PeriodBreak;
        }

        if (state.Phase == MatchPhase.LiveBall)
        {
            return CommandBoundary.ActionDecision;
        }

        return null;
    }

    private static string Describe(MatchSetupValidationResult validation) =>
        "Geçersiz setup: "
        + string.Join("; ", validation.Errors.Select(error => $"{error.Field} ({error.Code})"));

    private static MatchResult AbortedResult(MatchSetup setup, string reason, List<MatchEvent>? events = null) =>
        new()
        {
            MatchId = setup.MatchId,
            Status = MatchStatus.Aborted,
            HomeScore = 0,
            AwayScore = 0,
            IsTie = false,
            HomePossessions = 0,
            AwayPossessions = 0,
            ElapsedGameTimeMs = 0,
            PeriodsPlayed = 0,
            BoxScores = [],
            PlayerBoxScores = [],
            Events = events is null ? [] : [.. events],
            AbortReason = reason,
            Diagnostics = Diagnostics.DiagnosticCounters.Empty,
        };

    private static MatchResult Project(
        MatchSetup setup,
        MatchState state,
        List<MatchEvent> events)
    {
        var projection = new BoxScoreProjector(setup).Project(events);

        var homePossessions = 0;
        var awayPossessions = 0;
        string? abortReason = null;

        foreach (var matchEvent in events)
        {
            switch (matchEvent.Type)
            {
                case MatchEventType.PossessionStarted:
                    if (matchEvent.PayloadAs<PossessionStartedPayload>().Offense == TeamSide.Home)
                    {
                        homePossessions += 1;
                    }
                    else
                    {
                        awayPossessions += 1;
                    }

                    break;

                case MatchEventType.MatchAborted:
                    abortReason = matchEvent.PayloadAs<MatchAbortedPayload>().Reason;
                    break;
            }
        }

        var completed = state.Phase == MatchPhase.Completed;

        return new MatchResult
        {
            MatchId = setup.MatchId,
            Status = completed ? MatchStatus.Completed : MatchStatus.Aborted,
            HomeScore = state.HomeScore,
            AwayScore = state.AwayScore,
            IsTie = completed && state.HomeScore == state.AwayScore,
            HomePossessions = homePossessions,
            AwayPossessions = awayPossessions,
            ElapsedGameTimeMs = state.Clock.ElapsedGameTimeMs,
            PeriodsPlayed = state.Clock.Period,
            BoxScores = [projection.Home, projection.Away],
            PlayerBoxScores = projection.Players,
            Events = [.. events],
            PlayerEnergy = completed ? BuildEnergyReport(state) : [],
            AbortReason = abortReason,

            // M6 (D100): rapor yuzeyi. Motor bu degeri HICBIR YERDE okumaz.
            Diagnostics = state.Diagnostics,
        };
    }

    /// <summary>
    /// M5 (D84, D89): maç sonu timeout özeti. Rapor ve M7'nin canlı runner'ı
    /// kullanır; <b>motor hiçbir yerde okumaz</b> — bütçe yalnız komut
    /// doğrulamasında girdidir.
    /// </summary>
    private static MatchResult WithTimeoutSummary(MatchResult result, MatchState state)
    {
        var rules = state.Config.Rules;

        return result with
        {
            HomeTimeoutsUsed = state.Home.FullTimeoutsUsed,
            AwayTimeoutsUsed = state.Away.FullTimeoutsUsed,
            HomeShortTimeoutsUsed = state.Home.ShortTimeoutsUsed,
            AwayShortTimeoutsUsed = state.Away.ShortTimeoutsUsed,
            HomeTimeoutBudget = TimeoutPolicy.FullBudget(rules, state.Clock),
            AwayTimeoutBudget = TimeoutPolicy.FullBudget(rules, state.Clock),
            HomeShortTimeoutBudget = TimeoutPolicy.ShortBudget(rules, state.Clock),
            AwayShortTimeoutBudget = TimeoutPolicy.ShortBudget(rules, state.Clock),
        };
    }

    /// <summary>
    /// Maç sonu enerji ve dakika raporu. Kanonik kadro sırasına göre üretilir ki
    /// çıktı deterministik olsun (08 §4).
    /// </summary>
    private static ImmutableArray<PlayerEnergyReport> BuildEnergyReport(MatchState state)
    {
        var builder = ImmutableArray.CreateBuilder<PlayerEnergyReport>();

        foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
        {
            var team = state.Team(side);
            var definitions = new Dictionary<Guid, string>();

            foreach (var player in team.Roster)
            {
                definitions[player.Id] = player.DisplayName;
            }

            foreach (var playerState in team.PlayerStates)
            {
                builder.Add(new PlayerEnergyReport(
                    playerState.PlayerId,
                    definitions.GetValueOrDefault(
                        playerState.PlayerId,
                        playerState.PlayerId.ToString()),
                    side,
                    FatigueCalculator.ForDisplay(playerState.Energy),
                    playerState.SecondsOnCourt));
            }
        }

        return builder.ToImmutable();
    }

    private TeamMatchState BuildTeamState(TeamMatchSetup setup, TeamSide side)
    {
        var byId = new Dictionary<Guid, Player>();

        foreach (var player in setup.Team.Roster)
        {
            byId[player.Id] = player;
        }

        var onCourt = ImmutableArray.CreateBuilder<Player>(setup.Lineup.PlayerIds.Length);

        foreach (var playerId in setup.Lineup.PlayerIds)
        {
            if (!byId.TryGetValue(playerId, out var player))
            {
                throw new InvalidOperationException(
                    $"Doğrulanmış setup'ta lineup oyuncusu kadroda bulunamadı: {playerId} ({side}).");
            }

            onCourt.Add(player);
        }

        // Roster ve PlayerStates ayni kanonik sirayi paylasir: 04 §11 oyuncu
        // durumu, kadro sirasina gore indekslenir.
        var roster = RosterOrdering.Canonical(setup.Team.Roster);

        return new TeamMatchState
        {
            Side = side,
            Team = setup.Team,
            OffensiveTactic = setup.Offensive,
            DefensiveTactic = setup.Defense,
            Pace = setup.Pace,
            Roster = roster,
            OnCourt = onCourt.ToImmutable(),
            PlayerStates = TeamMatchState.InitialStates(roster, _config.Fatigue),
            Fouls = FoulCounters.Empty,
            FoulOutPlayerIds = [],
        };
    }

    /// <summary>
    /// Tek bir <c>Advance</c> adiminin calisma baglami. Event'leri toplar ve zarfi
    /// tek noktadan damgalar; boylece hicbir event eksik veya tutarsiz zarla
    /// uretilemez.
    /// </summary>
    private sealed class Step(MatchSimulation engine, MatchState state)
    {
        private readonly MatchSimulation _engine = engine;
        private MatchState _state = state;

        public List<MatchEvent> Events { get; } = [];

        /// <summary>
        /// M5: bu adımda üretilen komut sonuçları. 03: StepResult command
        /// sonuçlarını da taşır.
        /// </summary>
        public List<CommandResult> CommandResults { get; } = [];

        /// <summary>
        /// M6 (D100): bu adımın kural sayaçları. <b>Yalnız yazılır.</b>
        /// <c>Advance</c> sonunda <see cref="FlushDiagnostics"/> tek bir immutable
        /// kayda çevirip state'e yazar.
        ///
        /// <para>Adı bilerek <c>Tally</c>: bir <c>Diagnostics</c> özelliği
        /// <c>DreamTeam.MatchEngine.Diagnostics</c> ad alanını gölgeler ve
        /// <c>Diagnostics.DiagnosticCounters.Empty</c> gibi ifadeler derlenmez.</para>
        /// </summary>
        public Diagnostics.DiagnosticTally Tally { get; } = new();

        public MatchState State => _state;

        /// <summary>
        /// M6 (D100): adım sayaclarını duruma yazar. <b>Adım bittikten SONRA</b>
        /// çağrılır; karar hiçbir noktada bu değerleri okumaz.
        /// </summary>
        public void FlushDiagnostics()
        {
            var delta = Tally.ToCounters();

            if (delta == Diagnostics.DiagnosticCounters.Empty)
            {
                return;
            }

            _state = _state with
            {
                Diagnostics = Diagnostics.DiagnosticCounters.Merge(_state.Diagnostics, delta),
            };
        }

        // ------------------------------------------------- M5: komut yonetimi

        /// <summary>
        /// Gelen komutları kabul eder. Kabul; siralama, idempotency ve zarf
        /// dogrulamasi yapar. <b>Uygulama burada olmaz</b>: kabul edilen komut
        /// kuyruga girer ve hedefledigi mantıksal sinira gelince bosaltilir
        /// (07 §5: "ACK = alindi/kuyruga girdi. Applied = state'e islendi").
        ///
        /// <para><b>RNG tüketmez.</b> Kabul saf bir yazma islemidir.</para>
        /// </summary>
        /// <summary>
        /// D95: gelir gelmez suresi dolmus komutu reddeder. Kuyruga hic girmez.
        /// </summary>
        public void RejectExpiredOnArrival(ScheduledManagerCommand command)
        {
            _state = _state with
            {
                CommandQueue = _state.CommandQueue.MarkSettled(command.CommandId),
            };

            var rejected = CommandResult.Rejected1(
                command,
                CommandRejectionReason.Expired,
                _state.NextSequence,
                "Komut gonderildigi anda suresi dolmustu.");

            CommandResults.Add(rejected);
            PublishCommandResultEvent(rejected);
        }

        public void AcceptCommands(ImmutableArray<ScheduledManagerCommand> incoming)
        {
            if (incoming.IsEmpty)
            {
                return;
            }

            var accepted = ImmutableArray.CreateBuilder<ScheduledManagerCommand>(incoming.Length);

            foreach (var candidate in incoming)
            {
                var team = _state.Team(candidate.Side);

                var failure = CommandValidator.ValidateEnvelope(candidate, team);

                if (failure is not null)
                {
                    // Zarf gecersizse komut KUYRUGA GIRMEZ; dogrudan reddedilir
                    // ve kalici olarak isaretlenir ki tekrar gonderilirse
                    // yeniden denenmesin (T14).
                    _state = _state with
                    {
                        CommandQueue = _state.CommandQueue.MarkSettled(candidate.CommandId),
                    };

                    var rejected = CommandResult.Rejected1(
                        candidate,
                        failure.Value.Reason,
                        _state.NextSequence,
                        failure.Value.Message);

                    CommandResults.Add(rejected);
                    PublishCommandResultEvent(rejected);
                    continue;
                }

                accepted.Add(candidate);
                Tally.CommandsAccepted += 1;
            }

            if (accepted.Count == 0)
            {
                return;
            }

            var (nextQueue, acceptResults) = _state.CommandQueue.Accept(
                [.. accepted], _state.NextSequence);

            _state = _state with { CommandQueue = nextQueue };

            foreach (var result in acceptResults)
            {
                CommandResults.Add(result);
                PublishCommandResultEvent(result);
            }
        }

        /// <summary>
        /// Suresi dolmus komutlari dusurur (07 §6 "queue/expire/reject policy acik
        /// olmali"). Her adimin sonunda cagrilir.
        /// </summary>
        public void ExpireStaleCommands()
        {
            var (queue, results) = _state.CommandQueue.ExpirePast(_state.NextSequence);

            if (results.IsEmpty)
            {
                return;
            }

            _state = _state with { CommandQueue = queue };
            PublishCommandResults(results);
        }

        /// <summary>
        /// Terminal duruma girildiginde kalan tum komutlari sonlandirir
        /// (07 §6 madde 4).
        /// </summary>
        public void ExpandOnTerminal()
        {
            if (!_state.IsTerminal)
            {
                return;
            }

            var (queue, results) = _state.CommandQueue.ExpireAll(_state.NextSequence);

            if (results.IsEmpty)
            {
                return;
            }

            _state = _state with { CommandQueue = queue };
            PublishCommandResults(results);
        }

        /// <summary>
        /// <b>Sinir bosaltma.</b> Verilen mantıksal sinira gelen komutlari
        /// sirayla uygular. D86: FIFO; last-write-wins taktik komutlarindan
        /// dogal olarak cikar.
        ///
        /// <para><b>Uygulama dogrulamasi burada tekrarlanir</b> (06 §7): araya
        /// baska bir komut girmis olabilir.</para>
        /// </summary>
        public void ApplyAtBoundary(CommandBoundary boundary, bool isDeadBallWindow)
        {
            // M6 (D100): pencere ACIKLANDI. Komut gelmese de sayilir; 06 §7
            // pencere sayimi komut gonderiminden bagimsizdir.
            if (isDeadBallWindow)
            {
                Tally.DeadBallWindows += 1;
            }

            if (_state.CommandQueue.PendingCount == 0)
            {
                return;
            }

            var (queue, atBoundary) = _state.CommandQueue.TakeAt(boundary);

            if (atBoundary.IsEmpty)
            {
                return;
            }

            _state = _state with { CommandQueue = queue };

            var rules = _engine._config.Rules;
            var isFinalTwoMinutes = rules.IsFinalTwoMinutes(
                rules.PeriodDurationMs - _state.Clock.GameClockMs);

            var processed = ImmutableArray.CreateBuilder<ScheduledManagerCommand>(atBoundary.Length);

            foreach (var command in atBoundary)
            {
                var team = _state.Team(command.Side);

                var failure = CommandValidator.ValidateForApplication(
                    command,
                    team,
                    rules,
                    _state.Clock,
                    isDeadBallWindow,
                    isFinalTwoMinutes);

                if (failure is not null)
                {
                    var rejected = CommandResult.Rejected1(
                        command, failure.Value.Reason, _state.NextSequence, failure.Value.Message);

                    CommandResults.Add(rejected);
                    PublishCommandResultEvent(rejected);
                    processed.Add(command);
                    continue;
                }

                ApplyCommand(command);
                processed.Add(command);
            }

            _state = _state with { CommandQueue = _state.CommandQueue.Settle(processed.ToImmutable()) };
        }

        private void ApplyCommand(ScheduledManagerCommand command)
        {
            var team = _state.Team(command.Side);

            switch (command.Kind)
            {
                case ManagerCommandKind.ChangeOffense:
                {
                    var next = command.Payload.OffensiveTactic!.Value;

                    _state = ReplaceTeam(command.Side, team with { OffensiveTactic = next });

                    Emit(
                        MatchEventType.TacticChanged,
                        new TacticChangedPayload(command.CommandId, team.OffensiveTactic, next),
                        possessionId: _state.Possession?.PossessionId,
                        actionId: null,
                        teamId: command.Side,
                        playerId: null,
                        secondaryPlayerId: null);
                    break;
                }

                case ManagerCommandKind.ChangeDefense:
                {
                    var next = command.Payload.DefensiveTactic!.Value;

                    _state = ReplaceTeam(command.Side, team with { DefensiveTactic = next });

                    Emit(
                        MatchEventType.DefenseChanged,
                        new DefenseChangedPayload(command.CommandId, team.DefensiveTactic, next),
                        possessionId: _state.Possession?.PossessionId,
                        actionId: null,
                        teamId: command.Side,
                        playerId: null,
                        secondaryPlayerId: null);
                    break;
                }

                case ManagerCommandKind.ChangePace:
                {
                    var next = command.Payload.Pace!.Value;

                    _state = ReplaceTeam(command.Side, team with { Pace = next });

                    Emit(
                        MatchEventType.PaceChanged,
                        new PaceChangedPayload(command.CommandId, team.Pace, next),
                        possessionId: _state.Possession?.PossessionId,
                        actionId: null,
                        teamId: command.Side,
                        playerId: null,
                        secondaryPlayerId: null);
                    break;
                }

                case ManagerCommandKind.Substitute:
                {
                    var incoming = command.Payload.IncomingPlayerId!.Value;
                    var outgoing = command.Payload.OutgoingPlayerId!.Value;

                    _state = ReplaceTeam(
                        command.Side, SubstitutionPolicy.Apply(team, incoming, outgoing));

                    Emit(
                        MatchEventType.Substitution,
                        new SubstitutionPayload(command.CommandId, incoming, outgoing),
                        possessionId: _state.Possession?.PossessionId,
                        actionId: null,
                        teamId: command.Side,
                        playerId: null,
                        secondaryPlayerId: outgoing);
                    break;
                }

                case ManagerCommandKind.RequestTimeout:
                {
                    var kind = command.Payload.TimeoutKind!.Value;

                    _state = ReplaceTeam(command.Side, TimeoutPolicy.Spend(team, kind));
                    var updated = _state.Team(command.Side);

                    Emit(
                        MatchEventType.Timeout,
                        new TimeoutPayload(
                            command.CommandId,
                            kind,
                            updated.FullTimeoutsUsed,
                            updated.ShortTimeoutsUsed),
                        possessionId: _state.Possession?.PossessionId,
                        actionId: null,
                        teamId: command.Side,
                        playerId: null,
                        secondaryPlayerId: null);
                    break;
                }

                default:
                    throw new InvalidOperationException($"Bilinmeyen komut turu: {command.Kind}.");
            }

            var applied = CommandResult.Applied1(command, _state.NextSequence);
            CommandResults.Add(applied);
            PublishCommandResultEvent(applied);
        }

        public void PublishCommandResults(ImmutableArray<CommandResult> results)
        {
            foreach (var result in results)
            {
                PublishCommandResultEvent(result);
            }
        }

        private void PublishCommandResultEvent(CommandResult result)
        {
            // M6 (D100): her komut sonucu TEK bir noktadan gecer; sayac burada
            // guvenilir sekilde tam sayilir.
            if (result.Applied)
            {
                Tally.CommandsApplied += 1;
            }
            else
            {
                Tally.CommandsRejected += 1;
            }

            Emit(
                result.Applied ? MatchEventType.CommandApplied : MatchEventType.CommandRejected,
                result.Applied
                    ? new CommandAppliedPayload(
                        result.CommandId,
                        result.Kind,
                        result.Boundary,
                        result.AcceptedOrder)
                    : new CommandRejectedPayload(
                        result.CommandId,
                        result.Kind,
                        result.Reason,
                        result.Message),
                possessionId: _state.Possession?.PossessionId,
                actionId: null,
                teamId: result.Side,
                playerId: null,
                secondaryPlayerId: null);
        }

        // ---------------------------------------------------------------- periyot

        public void StartPeriod()
        {
            var period = _state.Clock.Period + 1;
            var isFirstPeriod = period == 1;
            var rules = _engine._config.Rules;

            // M6 (D100): periyot ve uzatma sayaci. Uzatma, duzenleme periyodu
            // sayisindan fazla olan periyottur (06 §8).
            Tally.PeriodsStarted += 1;

            if (PeriodController.IsOvertime(rules, period))
            {
                Tally.OvertimePeriodsStarted += 1;
            }

            var clock = _state.Clock.BeginPeriod(
                period,
                PeriodController.DurationMsForPeriod(rules, period),
                ClockResetPolicy.ForNewPossession(rules));

            // M4: periyot arasi toparlanma. Canli SURE sayilmaz; SecondsOnCourt
            // degismez (T12c'nin toplami bozulmamalidir).
            //
            // ONEMLI: suresi <c>_state.Clock.GameClockMs</c> DEGIL, onceki periyotun
            // tam sureyi. Periyot bittiginde oyun saati zaten sifirdir; saatten
            // okumak toparlanmayi daima sifir birakirdi.
            if (period > 1)
            {
                var breakMs = PeriodController.DurationMsForPeriod(rules, period - 1);

                _state = _state with
                {
                    Home = _state.Home.WithPlayerStates(FatigueCalculator.RecoverDuringBreak(
                        _engine._config.Fatigue, _state.Home.PlayerStates, breakMs)),
                    Away = _state.Away.WithPlayerStates(FatigueCalculator.RecoverDuringBreak(
                        _engine._config.Fatigue, _state.Away.PlayerStates, breakMs)),
                };
            }

            // Takim faulu periyot sayacidir: her periyot basinda sifirlanir.
            // D42'nin uzatma sarti bunun alt kumesidir.
            var home = _state.Home;
            var away = _state.Away;

            if (PeriodController.ResetsTeamFouls(rules, period))
            {
                home = home.WithFouls(home.Fouls.ResetTeamFouls());
                away = away.WithFouls(away.Fouls.ResetTeamFouls());
            }

            _state = _state with
            {
                Clock = clock,
                Phase = MatchPhase.LiveBall,
                Home = home,
                Away = away,
            };

            if (isFirstPeriod)
            {
                Emit(
                    MatchEventType.MatchStarted,
                    new MatchStartedPayload(_state.Home.Team.Name, _state.Away.Team.Name),
                    possessionId: null,
                    actionId: null,
                    teamId: null,
                    playerId: null,
                    secondaryPlayerId: null);
            }

            Emit(
                MatchEventType.PeriodStarted,
                new PeriodStartedPayload(
                    period,
                    PeriodController.DurationMsForPeriod(rules, period),
                    PeriodController.IsOvertime(rules, period)),
                possessionId: null,
                actionId: null,
                teamId: null,
                playerId: null,
                secondaryPlayerId: null);

            // M5: devre arasi bir dead-ball penceresidir (D82). Substitution ve
            // timeout BURADA uygulanir. Yeni hucrem baslamadan ONCE bosaltilir,
            // boylece lineup degisikligi ilk aksiyonda gecerlidir.
            ApplyAtBoundary(CommandBoundary.PeriodBreak, isDeadBallWindow: true);

            StartPossession(DrawStarterSide());
        }

        private void ClosePeriod()
        {
            EndPossessionIfOpen(PossessionEndReason.PeriodExpired);

            Emit(
                MatchEventType.PeriodEnded,
                new PeriodEndedPayload(
                    _state.Clock.Period,
                    PeriodController.IsOvertime(_engine._config.Rules, _state.Clock.Period)),
                possessionId: null,
                actionId: null,
                teamId: null,
                playerId: null,
                secondaryPlayerId: null);

            var rules = _engine._config.Rules;

            // M5 (D79): uzatma ust siniri. Skor esitse ve sinir dolduysa mac
            // Aborted olur; kazanan UYDURULMAZ ve MatchEnded YAZILMAZ.
            // 08 T10 "guard -> Aborted" yonu korunur, ama artik 521 periyot
            // beklemek yerine acik bir sinir vardir.
            var overtimeLimit = OvertimePolicy.LimitReachedReason(
                rules, _state.Clock, _state.HomeScore, _state.AwayScore);

            if (overtimeLimit is not null)
            {
                Abort(overtimeLimit);
                return;
            }

            if (PeriodController.ShouldStartOvertime(
                    rules,
                    _state.Clock.Period,
                    _state.HomeScore,
                    _state.AwayScore))
            {
                // Uzatma: kazanan rastgele secilmez, periyot oynanir (06 §8).
                _state = _state with { Phase = MatchPhase.PeriodBreak };
                return;
            }

            if (_state.Clock.Period >= rules.PeriodCount)
            {
                _state = _state with { Phase = MatchPhase.Completed };

                Emit(
                    MatchEventType.MatchEnded,
                    new MatchEndedPayload(_state.HomeScore, _state.AwayScore, _state.HomeScore == _state.AwayScore),
                    possessionId: null,
                    actionId: null,
                    teamId: null,
                    playerId: null,
                    secondaryPlayerId: null);
                return;
            }

            _state = _state with { Phase = MatchPhase.PeriodBreak };
        }

        /// <summary>
        /// Periyot acilisini belirler (D32). Tek bir bit cekilir: modulo bias yoktur
        /// ve ayni seed ayni baslangici verir. 06 §4, home/away bias yaratmayacak
        /// seeded yontem ister; toplu denge M6'nin isidir.
        /// </summary>
        private TeamSide DrawStarterSide() =>
            (_state.Random.NextUInt64() & 1UL) == 0UL ? TeamSide.Home : TeamSide.Away;

        // ----------------------------------------------------------------- aksiyon

        /// <summary>
        /// M6 (D100): bu aksiyon bir <c>ShotAttempt</c> yayinladi mi?
        ///
        /// <para><b>Neden bayrak?</b> Ilk tasarimda
        /// <c>ActionsWithoutShot</c> her "suta donusmedi" dalinda ayri ayri
        /// artiriliyordu. Bu <b>yanlis</b>ydi ve test yakaladi: hucre saati
        /// tukenince erken donen yol ile suta donusurken hucre faulu cozulen
        /// yol hic artirmiyordu, oysa ikisi de sut denemiyordu. Sonuc:
        /// <c>ActionsRun != ShotsAttempted + ActionsWithoutShot</c>.</para>
        ///
        /// <para>Bayrak yapilir: kimlik <b>yapisal</b> olarak dogru olur ve
        /// ileride eklenen bir cikis yolu onu bozamaz.</para>
        /// </summary>
        private bool _shotAttemptedThisAction;

        public void RunAction()
        {
            _shotAttemptedThisAction = false;
            RunActionCore();

            if (!_shotAttemptedThisAction)
            {
                Tally.ActionsWithoutShot += 1;
            }
        }

        private void RunActionCore()
        {
            if (_state.TotalActionCount >= _engine._config.MaxActionsPerMatch)
            {
                Abort(
                    $"Eylem guard'i asildi: {_state.TotalActionCount} >= "
                    + $"{_engine._config.MaxActionsPerMatch}. Skor gecerli degildir.");
                return;
            }

            var offense = _state.Possession?.Offense
                ?? throw new InvalidOperationException("Aksiyon yok: possession yok.");

            // M6 (D100): aksiyon sayaci. Suta donusmeyen her yol ayrica
            // `ActionsWithoutShot` artirir; boylece "kac aksiyon sut denedi"
            // raporlanabilir olur (08 §6 shot type dagilimi).
            Tally.ActionsRun += 1;

            // M5 (D86, 07 §6 madde 1): taktik ve tempo BIR SONRAKI AKSIYON KARAR
            // SINIRINDA uygulanir. Cozulmeye baslamis bir sutu geriye donuk
            // DEGISTIRMEZ; yalniz buraya gelen aksiyonun secimini etkiler.
            //
            // Bu, planin en onemli yerlesimidir: komutlar iceride, ayni adimda
            // bosaltilir; MatchPhase.DeadBall KALICI YAZILMAZ (D54 korunur) ve
            // event'siz bir adim olusmaz.
            ApplyAtBoundary(CommandBoundary.ActionDecision, isDeadBallWindow: false);

            var attacking = _state.Team(offense);
            var defending = _state.Team(offense.Opponent());

            // M4: aksiyon ve oyuncu secimi taktiğin normalize dağılımından gelir.
            // Sıra 1) ve 2) — iki çekiliş, M3 ile aynı.
            var actionId = _state.NextActionId;
            var selection = _engine._offense.Select(
                attacking.OffensiveTactic,
                attacking.OnCourt,
                _state.Random);

            _state = _state with
            {
                NextActionId = actionId + 1,
                TotalActionCount = _state.TotalActionCount + 1,
                Possession = _state.Possession! with
                {
                    ActionCount = _state.Possession!.ActionCount + 1,
                },
            };

            // 3) BIRINCIL SAVUNMACI — her zaman bir cekilis (D68). Ayni oyuncu
            // faul atfi, blogu ve kalite eslesmesi icin kullanilir.
            var primaryDefender = PickPrimaryDefender(selection.ShotType);

            // 4) top kaybi — savunma baskisi kanalindan etkilenir
            var turnover = _engine._turnoverResolver.Resolve(
                PlayerOf(defending, primaryDefender).Ratings,
                _state.Random);

            var setupMs = SetupActionMilliseconds(offense);
            if (turnover.IsTurnover)
            {
                ConsumeLive(offense, setupMs);
                RecordTurnover(offense, selection.PlayerId, actionId, TurnoverKind.LostBall);
                EndPossession(PossessionEndReason.Turnover);
                HandleAfterPossession(offense);
                return;
            }

            ConsumeLive(offense, setupMs);

            if (_state.Clock.IsGameTimeExhausted)
            {
                EndPossession(PossessionEndReason.PeriodExpired);
                ClosePeriod();
                return;
            }

            // 5) faul olma — savunma disiplini kanalindan etkilenir
            var foul = _engine._foulResolver.Occurred(
                _state.Random,
                _engine._defense.FoulAggression(defending.DefensiveTactic));

            // 6) suta donusme
            var shotAttempted = _state.Random.NextDouble()
                < _engine._config.Actions.ShotCompletionProbability;

            // Ortak hucre saati kontrolu. Aksiyonun canli suresi bu noktada
            // tuketildi; saat sifira dustuyse ihlal olusur. Kontrolun iki dalda
            // da gecerli olmasi gerekir: aksi halde hucrem suta donusmedigi
            // surece sonsuza kadar devam eder ve ihlal hic kaydedilmez.
            if (_state.Clock.IsShotClockExhausted)
            {
                RecordTurnover(offense, selection.PlayerId, actionId, TurnoverKind.ShotClockViolation);
                EndPossession(PossessionEndReason.Turnover);
                HandleAfterPossession(offense);
                return;
            }

            if (!shotAttempted)
            {
                // Suta donusmedi. Faul ONCE cozulur: hucre faulu aksiyonu iptal
                // eder ve TEK turnover uretir. ActionCompleted ile birlikte
                // sayilirsa ayni aksiyon iki kez muhasebelenmis olur.
                if (foul.IsFoul)
                {
                    if (_engine._foulResolver.IsOffensive(_state.Random))
                    {
                        ApplyOffensiveFoul(offense, _state.NextFoulId, selection, actionId);
                        return;
                    }

                    var bonusActive = _engine._foulResolver.IsInBonus(
                        _state.Team(offense.Opponent()).Fouls.TeamFoulsThisPeriod);

                    ApplyFoul(
                        offense,
                        _state.NextFoulId,
                        primaryDefender,
                        FoulType.NonShooting,
                        0,
                        selection.PlayerId,
                        actionId);

                    if (bonusActive)
                    {
                        StartFreeThrowSeriesWithoutShot(
                            offense,
                            selection,
                            _engine._config.Fouls.BonusFreeThrowCount);
                        return;
                    }

                    // Bonus yok: hucrem korunur, hucre saati 14 s'e iner (06 §4, §89).
                    ResetShotClockForDefensiveFoul(offense);

                    if (_state.IsTerminal)
                    {
                        return;
                    }
                }

                Emit(
                    MatchEventType.ActionCompleted,
                    new ActionCompletedPayload(selection.Action),
                    possessionId: _state.Possession!.PossessionId,
                    actionId: actionId,
                    teamId: offense,
                    playerId: selection.PlayerId,
                    secondaryPlayerId: null);

                return;
            }

            ReleaseShot(offense, selection, actionId, primaryDefender, foul);
        }

        /// <summary>
        /// Hucum faulu tespit edilirse sut hiç denenmez: düdük bırakmadan once
        /// çalar, bu yuzden <c>ShotAttempt</c> yayınlanmaz ve settlement beklenmez
        /// (07 §3: "ShotAttempt sutun kimligini acar"; acilan her shot kapanir).
        /// Tek turnover yazilir, serbest atis yoktur (06 §88).
        /// </summary>
        private void ApplyOffensiveFoul(TeamSide offense, long foulId, ActionSelection selection, long actionId)
        {
            ApplyFoul(offense, foulId, selection.PlayerId, FoulType.Offensive, 0, null, actionId);

            // Faulun kendisi foul-out'a yol actiysa ve yasal yedek yoksa motor
            // Aborted olmustur. Terminal durumun uzerine turnover yazmak
            // possession'i null uzerinden gecersizlestirir.
            if (_state.IsTerminal)
            {
                return;
            }

            RecordTurnover(offense, selection.PlayerId, actionId, TurnoverKind.OffensiveFoul);
            EndPossession(PossessionEndReason.Turnover);
            HandleAfterPossession(offense);
        }

        /// <summary>
        /// Sut birakildi; sonucu bir sonraki adimda cozulecek. 06 §2: ucus suresi
        /// canli oyun suresidir, bu yuzden burada tuketilir.
        /// </summary>
        private void ReleaseShot(
            TeamSide offense,
            ActionSelection selection,
            long actionId,
            Guid primaryDefender,
            FoulOutcome foul)
        {
            var shotId = _state.NextShotId;
            var foulId = _state.NextFoulId;
            long shooterFoulId = 0;
            var foulType = FoulType.NonShooting;

            // 7) hucre faulu — ShotAttempt ONCESI cozulur (D50).
            if (foul.IsFoul)
            {
                if (_engine._foulResolver.IsOffensive(_state.Random))
                {
                    ApplyOffensiveFoul(offense, foulId, selection, actionId);
                    return;
                }

                // 8) shooting faulu: yalniz faul ve sut varsa anlamlidir
                var isShooting = _engine._foulResolver.IsShooting(_state.Random);

                foulType = isShooting ? FoulType.Shooting : FoulType.NonShooting;

                // D69 (M4'te bulunan gercek hata): savunma faulu <b>her</b> durumda
                // yazilir. Onceki surum yalniz <c>isShooting</c> dogrulusunda
                // ApplyFoul cagiryordu; shooting OLMAYAN savunma faulu sut
                // denemesinde sessizce kayboluyordu. Boylece kisisel faul sayaci,
                // takim faul sayaci ve bonus hic tetiklenmiyordu.
                shooterFoulId = foulId;
            }

            var attacking = _state.Team(offense);
            var defending = _state.Team(offense.Opponent());
            var shooter = PlayerOf(attacking, selection.PlayerId);
            var defender = PlayerOf(defending, primaryDefender);

            // M4: kalite burada hesaplanir ve PendingShot'a yazilir. Taktik
            // bonusu + savunma cezasi + IQ farki tek noktada toplanir (D58:
            // savunmanin z'ye dogrudan girdigi bir yol yoktur).
            var quality = ShotQualityResolver.Resolve(
                selection.Action,
                attacking.OffensiveTactic,
                _engine._config.Tactics,
                defending.DefensiveTactic,
                _engine._defense,
                shooter.Ratings.BasketballIQ,
                defender.Ratings.BasketballIQ);

            // M4: shooter enerjisi hesap icin kesirli kalir; event yalnizca gozlem
            // icin tam sayiya yuvarlanir (D67).
            var shooterEnergy = attacking.StateFor(selection.PlayerId)?.Energy
                ?? _engine._config.Fatigue.StartingEnergy;

            _state = _state with
            {
                NextShotId = shotId + 1,
                Possession = _state.Possession! with
                {
                    ShotCount = _state.Possession!.ShotCount + 1,
                },
            };

            Emit(
                MatchEventType.ShotAttempt,
                new ShotAttemptPayload(
                    shotId,
                    selection.ShotType,
                    quality,
                    FatigueCalculator.ForDisplay(shooterEnergy)),
                possessionId: _state.Possession!.PossessionId,
                actionId: actionId,
                teamId: offense,
                playerId: selection.PlayerId,
                secondaryPlayerId: null);

            Tally.ShotsAttempted += 1;
            _shotAttemptedThisAction = true;

            if (shooterFoulId != 0)
            {
                ApplyFoul(offense, foulId, primaryDefender, foulType, 0, selection.PlayerId, actionId);

                // Faul foul-out'a yol actiysa ve yasal yedek yoksa motor Aborted
                // olmustur. Bu durumun uzerine PendingShot yazmak terminal durumu
                // ezer ve sonraki adim "shoot var ama possession yok" ile patlar.
                if (_state.IsTerminal)
                {
                    return;
                }
            }

            // 9) cember teması (06 §6 reset tablosu)
            var rimContact = _engine._rimContactResolver.TouchedRim(_state.Random);

            // M6 (D100): motor ici bir olay; event degildir. Hucrem saati
            // politikasini besler ve M6 raporunda ayrica gorunur.
            if (rimContact)
            {
                Tally.RimContacts += 1;
            }

            // Ucus suresi canli oyun suresidir.
            ConsumeLive(offense, _engine._config.Actions.ShotFlightMs);

            _state = _state with
            {
                Phase = MatchPhase.ShotPending,
                PendingShot = new PendingShot
                {
                    ActionId = actionId,
                    ShotId = shotId,
                    FoulId = shooterFoulId,
                    FoulType = foulType,
                    ShooterId = selection.PlayerId,
                    PrimaryDefenderId = primaryDefender,
                    ShotType = selection.ShotType,
                    SkillRating = selection.SkillRating,
                    Quality = quality,
                    ShooterEnergy = FatigueCalculator.ForDisplay(shooterEnergy),
                    RimContact = rimContact,
                },
            };
        }

        /// <summary>
        /// Birakilmis sutun tek canonical settlement'i (07 §3). Burada blok, isabet
        /// ve sayilabilirlik birlikte karar verilir; ikinci bir settlement yolu
        /// yoktur.
        /// </summary>
        public void ResolvePendingShot()
        {
            var pending = _state.PendingShot
                ?? throw new InvalidOperationException("Bekleyen şut yok.");

            var offense = _state.Possession?.Offense
                ?? throw new InvalidOperationException("Şut var ama possession yok.");

            // 10) blok — M4'te savunmacinin ic savunma composite'inden turetilir.
            // D68: bloklayan, pending'te kayitli BIRINCIL savunmacinin kendisidir;
            // burada yeni bir savunmaci cekilisi YAPILMAZ.
            var defender = PlayerOf(_state.Team(offense.Opponent()), pending.PrimaryDefenderId);
            var isBlocked = _engine._blockResolver.IsBlocked(defender.Ratings, _state.Random);

            Guid? blockerId = isBlocked ? pending.PrimaryDefenderId : null;

            // 11) isabet: blok gerceklesmisse cekilis tuketilmez (05 §127).
            // M4: quality ve fatigueLoad kanallari devreye girer.
            var isMade = false;

            if (!isBlocked)
            {
                var fatigueLoad = FatigueCalculator.FatigueLoad(
                    _engine._config.Fatigue,
                    pending.ShooterEnergy);

                var outcome = _engine._shotResolver.Resolve(
                    pending.ShotType,
                    pending.SkillRating,
                    pending.Quality,
                    fatigueLoad,
                    _state.Random);

                isMade = outcome.IsMade;
            }

            // 06 §87: kacan shooting foul'da FGA sayilmaz. Isabetli shooting
            // foul'da (and-one) FGA ve FGM yazilir.
            var hasFoul = pending.FoulId != 0;
            var countsAsFieldGoalAttempt = !hasFoul || isMade;

            if (isBlocked)
            {
                Tally.ShotsBlocked += 1;

                Emit(
                    MatchEventType.Block,
                    new BlockPayload(pending.ShotId, blockerId!.Value),
                    possessionId: _state.Possession!.PossessionId,
                    actionId: pending.ActionId,
                    teamId: offense.Opponent(),
                    playerId: blockerId,
                    secondaryPlayerId: pending.ShooterId);
            }

            if (isMade)
            {
                var points = ShotResolver.PointsFor(pending.ShotType);

                Tally.ShotsMade += 1;

                Emit(
                    MatchEventType.ShotMade,
                    new ShotMadePayload(pending.ShotId, pending.ShotType, points, countsAsFieldGoalAttempt),
                    possessionId: _state.Possession!.PossessionId,
                    actionId: pending.ActionId,
                    teamId: offense,
                    playerId: pending.ShooterId,
                    secondaryPlayerId: SelectAssister(offense, pending.ShooterId));

                _state = _state with
                {
                    HomeScore = offense == TeamSide.Home ? _state.HomeScore + points : _state.HomeScore,
                    AwayScore = offense == TeamSide.Away ? _state.AwayScore + points : _state.AwayScore,
                };
            }
            else
            {
                Tally.ShotsMissed += 1;

                Emit(
                    MatchEventType.ShotMissed,
                    new ShotMissedPayload(pending.ShotId, pending.ShotType, countsAsFieldGoalAttempt),
                    possessionId: _state.Possession!.PossessionId,
                    actionId: pending.ActionId,
                    teamId: offense,
                    playerId: pending.ShooterId,
                    secondaryPlayerId: null);
            }

            var freeThrowCount = hasFoul
                ? _engine._foulResolver.FreeThrowCountFor(
                    pending.FoulType,
                    bonusActive: _engine._foulResolver.IsInBonus(
                        _state.Team(offense.Opponent()).Fouls.TeamFoulsThisPeriod),
                    shotAttempted: true,
                    shotMade: isMade,
                    pending.ShotType)
                : 0;

            // Bekleyen sut kapanir; faz da canli topa donmelidir. Faz
            // ShotPending'te kalirsa sonraki adim hicbir dala girmez ve motor
            // "ilerleme yok" agina takilir.
            _state = _state with
            {
                PendingShot = null,
                Phase = MatchPhase.LiveBall,
            };

            if (freeThrowCount > 0)
            {
                StartFreeThrowSeries(offense, pending, freeThrowCount, fieldGoalWasCounted: isMade && countsAsFieldGoalAttempt);
                return;
            }

            if (isMade)
            {
                // Faul olsa bile sayı işlenir: isabetli non-shooting faulde de
                // basket sayılır, sadece topun gidişi değişir.
                EndPossession(PossessionEndReason.Scored);
                HandleAfterPossession(offense);
                return;
            }

            if (_state.Clock.IsGameTimeExhausted)
            {
                // Periyot bitti: 06 §5, periyot sonunda ribaund uretilmez.
                EndPossession(PossessionEndReason.PeriodExpired);
                ClosePeriod();
                return;
            }

            // Kacan sut. Non-shooting faul olsa bile top canlidir: ribaund firsati
            // vardir (06 §5, "her missed attempt'e otomatik oyuncu ribaundu
            // dagitma" kurali bu yolu degil, fazla ribaund'u yasaklar).
            ResolveRebound(offense, pending);
        }

        // -------------------------------------------------------- serbest atislar

        private void StartFreeThrowSeries(
            TeamSide offense,
            PendingShot pending,
            int count,
            bool fieldGoalWasCounted)
        {
            var seriesId = _state.NextFTSeriesId;

            _state = _state with
            {
                NextFTSeriesId = seriesId + 1,
                Phase = MatchPhase.FreeThrows,
                PendingFrees = new PendingFreeThrowSeries
                {
                    FTSeriesId = seriesId,
                    ShooterId = pending.ShooterId,
                    Index = 0,
                    Count = count,
                    // Yalniz tek atislik seri (and-one) canlidir. 06 §91: "Son FT
                    // miss'i yalniz top canliysa rebound uretir."
                    FinalShotIsLive = count == 1,
                    Offense = offense,
                    PossessionId = _state.Possession!.PossessionId,
                    FieldGoalWasCounted = fieldGoalWasCounted,
                },
            };
        }

        /// <summary>
        /// Serbest atislar canli oyun suresi tuketmez (06 §2). Seri bittiginde top
        /// gidisini bu adim belirler: canli son atis kactiyse ribaund yolu, degilse
        /// rakip inbound.
        /// </summary>
        public void ResolveFreeThrow()
        {
            var series = _state.PendingFrees
                ?? throw new InvalidOperationException("Bekleyen serbest atış serisi yok.");

            var shooter = FindPlayer(series.ShooterId);
            var isMade = _engine._freeThrowResolver.IsMade(shooter.Ratings.FreeThrow, _state.Random);
            var isFinal = series.IsLast;

            Emit(
                MatchEventType.FreeThrowAttempt,
                new FreeThrowAttemptPayload(series.FTSeriesId, series.Index, series.Count, isFinal),
                possessionId: series.PossessionId,
                actionId: null,
                teamId: series.Offense,
                playerId: series.ShooterId,
                secondaryPlayerId: null);

            Tally.FreeThrowsAttempted += 1;

            if (isMade)
            {
                Tally.FreeThrowsMade += 1;

                Emit(
                    MatchEventType.FreeThrowMade,
                    new FreeThrowMadePayload(series.FTSeriesId, series.Index, series.Count),
                    possessionId: series.PossessionId,
                    actionId: null,
                    teamId: series.Offense,
                    playerId: series.ShooterId,
                    secondaryPlayerId: null);

                _state = _state with
                {
                    HomeScore = series.Offense == TeamSide.Home ? _state.HomeScore + 1 : _state.HomeScore,
                    AwayScore = series.Offense == TeamSide.Away ? _state.AwayScore + 1 : _state.AwayScore,
                };
            }
            else
            {
                Emit(
                    MatchEventType.FreeThrowMissed,
                    new FreeThrowMissedPayload(
                        series.FTSeriesId,
                        series.Index,
                        series.Count,
                        BallIsLive: isFinal && series.FinalShotIsLive),
                    possessionId: series.PossessionId,
                    actionId: null,
                    teamId: series.Offense,
                    playerId: series.ShooterId,
                    secondaryPlayerId: null);
            }

            if (!isFinal)
            {
                // Seri devam ediyor: FT arasi canli rebound YOK (06 §91).
                _state = _state with { PendingFrees = series.WithIndex(series.Index + 1) };
                return;
            }

            _state = _state with { PendingFrees = null, Phase = MatchPhase.LiveBall };

            if (isFinal && series.FinalShotIsLive && !isMade)
            {
                if (_state.Clock.IsGameTimeExhausted)
                {
                    EndPossession(PossessionEndReason.PeriodExpired);
                    ClosePeriod();
                    return;
                }

                ResolveReboundAfterFreeThrow(series);
                return;
            }

            // And-one sonrasi veya cok atislik seri sonrasi top rakibe gider.
            EndPossession(PossessionEndReason.BonusFreeThrows);
            HandleAfterPossession(series.Offense);
        }

        // ------------------------------------------------------------------ ribaund

        private void ResolveRebound(TeamSide offense, PendingShot pending)
        {
            var attacking = _state.Team(offense);
            var rebound = _engine._reboundResolver.Resolve(attacking.OnCourt, _state.Random);

            Emit(
                MatchEventType.Rebound,
                new ReboundPayload(pending.ShotId, rebound.Offensive, IsTeamRebound: false),
                possessionId: _state.Possession!.PossessionId,
                actionId: pending.ActionId,
                teamId: offense,
                playerId: rebound.RebounderId,
                secondaryPlayerId: null);

            if (rebound.Offensive)
            {
                // 06 §4: OREB possession'i bitirmez; ayni kimlik devam eder (T05).
                // 06 §6: hucre saati yalniz cembere degen miss'te 14 s'e iner.
                _state = _state with
                {
                    Clock = _state.Clock with
                    {
                        ShotClockMs = ClockResetPolicy.ForOffensiveRebound(
                            _engine._config.Rules,
                            pending.RimContact) ?? _state.Clock.ShotClockMs,
                    },
                };

                return;
            }

            EndPossession(PossessionEndReason.DefensiveRebound);
            HandleAfterPossession(offense);
        }

        private void ResolveReboundAfterFreeThrow(PendingFreeThrowSeries series)
        {
            var offense = series.Offense;
            var attacking = _state.Team(offense);
            var rebound = _engine._reboundResolver.Resolve(attacking.OnCourt, _state.Random);

            Emit(
                MatchEventType.Rebound,
                new ReboundPayload(ShotId: 0, rebound.Offensive, IsTeamRebound: false),
                possessionId: series.PossessionId,
                actionId: null,
                teamId: offense,
                playerId: rebound.RebounderId,
                secondaryPlayerId: null);

            if (rebound.Offensive)
            {
                // Canli son FT hucumda kaldi: ayni possession devam eder.
                _state = _state with
                {
                    Clock = _state.Clock with
                    {
                        ShotClockMs = ClockResetPolicy.ForNewPossession(_engine._config.Rules),
                    },
                };

                return;
            }

            EndPossession(PossessionEndReason.DefensiveRebound);
            HandleAfterPossession(offense);
        }

        // -------------------------------------------------------------------- foul

        /// <summary>
        /// Suta donusmeyen bir aksiyonda faul. Bonuslu non-shooting faul serbest
        /// atis uretir ve top rakibe gider; bonus yoksa hucre saati 14 s'e iner
        /// ve possession korunur (06 §4, §89).
        /// </summary>
        private void StartFreeThrowSeriesWithoutShot(
            TeamSide offense,
            ActionSelection selection,
            int count)
        {
            if (_state.Possession is null)
            {
                // Faul terminal duruma yol actiysa seri kurulamaz.
                return;
            }

            var seriesId = _state.NextFTSeriesId;

            _state = _state with
            {
                NextFTSeriesId = seriesId + 1,
                Phase = MatchPhase.FreeThrows,
                PendingFrees = new PendingFreeThrowSeries
                {
                    FTSeriesId = seriesId,
                    ShooterId = selection.PlayerId,
                    Index = 0,
                    Count = count,
                    FinalShotIsLive = false,
                    Offense = offense,
                    PossessionId = _state.Possession!.PossessionId,
                    FieldGoalWasCounted = false,
                },
            };
        }

        /// <summary>
        /// Faul kaydini yazar, sayacları günceller ve gerekirse foul-out uygular.
        /// Foul-out sonrası yedekleme D41'in parçasıdır; yasal yedek yoksa
        /// terminal policy devreye girer.
        /// </summary>
        private void ApplyFoul(
            TeamSide offense,
            long foulId,
            Guid foulerId,
            FoulType type,
            int freeThrowCount,
            Guid? fouledPlayerId = null,
            long? actionId = null)
        {
            _state = _state with { NextFoulId = foulId + 1 };

            // M6 (D100): faul turu sayaci. Bonus ve free throw dagilimi
            // kalibrasyonda ayri ayri raporlanir.
            if (type == FoulType.Offensive)
            {
                Tally.FoulsOffensive += 1;
            }
            else if (type == FoulType.Shooting)
            {
                Tally.FoulsShooting += 1;
            }
            else
            {
                Tally.FoulsNonShooting += 1;
            }

            // Faul, ait oldugu aksiyonla ayni sinirda yayinlanir; boylece tuketici
            // bir faulu ilgili suta (ShotAttempt) actionId uzerinden baglayabilir.
            // 07 §2 bunu ayrica istemiyor ama korelasyon olmadan payload'daki
            // FreeThrowCount hangi atis oldugunu tespit ettirmez.
            Emit(
                MatchEventType.Foul,
                new FoulPayload(foulId, type, freeThrowCount),
                possessionId: _state.Possession?.PossessionId,
                actionId: actionId,
                teamId: type == FoulType.Offensive ? offense : offense.Opponent(),
                playerId: foulerId,
                secondaryPlayerId: fouledPlayerId);

            // Kisisel faul sayaci faulu eden oyuncunun takiminda artar.
            var foulingSide = type == FoulType.Offensive ? offense : offense.Opponent();

            UpdateTeam(foulingSide, team => team.WithFouls(team.Fouls.WithPersonalFoul(foulerId)));

            if (type != FoulType.Offensive)
            {
                // Takim faul sayaci yalniz savunma faulunde artar (06 §88).
                UpdateTeam(foulingSide, team => team.WithFouls(team.Fouls.WithTeamFoulThisPeriod()));
            }

            var fouls = _state.Team(foulingSide).Fouls;

            if (!EligibilityPolicy.IsFouledOut(
                    fouls,
                    foulerId,
                    _engine._config.Fouls.PersonalFoulLimit))
            {
                return;
            }

            var marked = _state.Team(foulingSide).MarkFoulOut(foulerId);
            _state = ReplaceTeam(foulingSide, marked);

            Tally.FoulOuts += 1;

            // Foul-out oyuncuyu sahadan cikarir ve yedekleme zorunludur (D41).
            if (_state.Team(foulingSide).OnCourt.Any(player => player.Id == foulerId))
            {
                var onCourt = _state.Team(foulingSide).OnCourt
                    .Where(player => player.Id != foulerId)
                    .ToImmutableArray();

                _state = ReplaceTeam(foulingSide, _state.Team(foulingSide).WithOnCourt(onCourt));
                ApplyReplacement(foulingSide);
            }
        }

        /// <summary>Foul-out yedeklemesi (D41).</summary>
        private void ApplyReplacement(TeamSide side)
        {
            var team = _state.Team(side);
            var replacement = EligibilityPolicy.SelectReplacement(team);

            if (replacement is null)
            {
                // D43: motor forfeit kazanani uydurmaz. Terminal policy acikca raporlanir.
                Abort("Yasal yedek yok (NoLegalSubstitute). Mac tamamlanmadi; kazanan secilmedi.");
                return;
            }

            // Cikan oyuncu once sahadan kaldirilir; yedek eklenerek sahadaki bes
            // kişi YENIDEN TAMAMLANIR. Kesme/sinirlama, foul-out'lar biriktikce
            // sahadaki sayiyi azaltir ve motor "sahada oyuncu yok" ile durur.
            var onCourt = team.OnCourt.Append(replacement).ToImmutableArray();

            _state = ReplaceTeam(side, team.WithOnCourt(onCourt));
        }

        // ---------------------------------------------------------------- possession

        private void StartPossession(TeamSide offense)
        {
            var possessionId = _state.PossessionCount + 1;

            Tally.PossessionsStarted += 1;

            // 06 §6: yeni hucremus 24 saniyelik hucre saatiyle baslar. OREB bu
            // sifirlamayi YAPMAZ; hucrem kimligi korunur ve kalan sure devreder.
            _state = _state with
            {
                Clock = _state.Clock with
                {
                    ShotClockMs = ClockResetPolicy.ForNewPossession(_engine._config.Rules),
                },
                PossessionCount = possessionId,
                Possession = new PossessionState
                {
                    PossessionId = possessionId,
                    Offense = offense,
                    ActionCount = 0,
                    ShotCount = 0,
                },
            };

            Emit(
                MatchEventType.PossessionStarted,
                new PossessionStartedPayload(possessionId, offense),
                possessionId: possessionId,
                actionId: null,
                teamId: offense,
                playerId: null,
                secondaryPlayerId: null);
        }

        private void EndPossession(PossessionEndReason reason)
        {
            var possessionId = _state.Possession?.PossessionId
                ?? throw new InvalidOperationException("Kapanacak possession yok.");

            Emit(
                MatchEventType.PossessionEnded,
                new PossessionEndedPayload(possessionId, reason),
                possessionId: possessionId,
                actionId: null,
                teamId: _state.Possession!.Offense,
                playerId: null,
                secondaryPlayerId: null);

            _state = _state with { Possession = null };
        }

        private void EndPossessionIfOpen(PossessionEndReason reason)
        {
            if (_state.Possession is not null)
            {
                EndPossession(reason);
            }
        }

        /// <summary>Possession kapandiktan sonra periyot/terminal durumu ele alir.</summary>
        private void HandleAfterPossession(TeamSide previousOffense)
        {
            if (_state.Clock.IsGameTimeExhausted)
            {
                EndPossessionIfOpen(PossessionEndReason.PeriodExpired);
                ClosePeriod();
                return;
            }

            if (_state.IsTerminal)
            {
                return;
            }

            if (_state.Possession is null)
            {
                // 06 §4: topu devreden taraf yeni hucremu baslatir. Devir, canli
                // sure tuketmez; bu yuzden state'e DeadBall yazilmadan bir sonraki
                // possession dogrudan baslar.
                //
                // M5: bu, possession sonundaki TEK dead-ball penceresidir.
                // Substitution ve timeout BURADA, yeni possession baslamadan
                // ONCE bosaltilir.
                //
                // D82 (kullanici netlestirdi): isabetli basket sonrasi PENCERE
                // ACILIR. 5+1 nokta: isabetli basket, hucrem degisimi, serbest
                // atis serisi sonu, hucrem saati ihlali, duduk sonrasi faul,
                // devre arasi. Yalniz DREB/steal sonrasi oyun CANLIDIR ve
                // pencere YOKTUR — o yol possession degistirmeden gecer.
                _state = _state with { Phase = MatchPhase.LiveBall };

                ApplyAtBoundary(CommandBoundary.DeadBall, isDeadBallWindow: true);

                StartPossession(previousOffense.Opponent());
            }
        }

        private void ResetShotClockForDefensiveFoul(TeamSide offense)
        {
            var rules = _engine._config.Rules;

            _state = _state with
            {
                Clock = _state.Clock with
                {
                    ShotClockMs = ClockResetPolicy.ForDefensiveNonShootingFoul(
                        rules,
                        _state.Clock.ShotClockMs),
                },
            };
        }

        // ------------------------------------------------------------------ secimler

        /// <summary>
        /// Birincil savunmacı seçimi (D68). <b>Her aksiyonda bir çekiliş</b> yapılır
        /// ve seçilen kişi o aksiyonun tamamında kullanılır: top kaybı baskısı, faul
        /// atfı, blok ve şut kalitesi eşleşmesi. Bu, M3'teki iki ayrı savunmacı
        /// çekilişini (05 §127'nin yasakladığı desen) ortadan kaldırır.
        ///
        /// Ağırlık <b>role göre</b> seçilir: iç şut için iç savunma, dış şut için
        /// perimeter savunma composite'i. Pozisyon modeli yoktur (D35, D66).
        /// </summary>
        private Guid PickPrimaryDefender(ShotType shotType)
        {
            var defenders = _state.Team(_state.Possession!.Offense.Opponent()).OnCourt;

            if (defenders.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException("Savunma sahada oyuncu yok.");
            }

            return WeightedSelector.Select(
                defenders,
                player => _engine._defense.SelectionWeight(
                    _engine._defense.RoleComposite(player.Ratings, shotType)),
                _state.Random).Id;
        }

        private Guid? SelectAssister(TeamSide offense, Guid shooterId)
        {
            var candidates = _state.Team(offense)
                .OnCourt.Where(player => player.Id != shooterId)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            return WeightedSelector.Select(
                candidates,
                player => _engine._ratings.SelectionWeight(PlayerRatingTables.Handle(player.Ratings)),
                _state.Random).Id;
        }

        /// <summary>Bir takımın belirli bir oyuncusunu döner. Kadro dışıysa hata.</summary>
        private static Player PlayerOf(TeamMatchState team, Guid playerId)
        {
            foreach (var player in team.Roster)
            {
                if (player.Id == playerId)
                {
                    return player;
                }
            }

            throw new InvalidOperationException($"Oyuncu kadroda bulunamadi: {playerId} ({team.Side}).");
        }

        private Player FindPlayer(Guid playerId)
        {
            foreach (var player in _state.Home.Roster)
            {
                if (player.Id == playerId)
                {
                    return player;
                }
            }

            foreach (var player in _state.Away.Roster)
            {
                if (player.Id == playerId)
                {
                    return player;
                }
            }

            throw new InvalidOperationException($"Oyuncu kadrolarda bulunamadi: {playerId}");
        }

        // --------------------------------------------------------------- tempo / enerji

        /// <summary>
        /// Hücum takımının temposuna göre çarpılmış aksiyon süresi (D59). Yuvarlama
        /// yuvarlamaya yapılır ve sonuç en az 1 ms'dir.
        /// </summary>
        private long SetupActionMilliseconds(TeamSide offense)
        {
            var tuning = _engine._config.Pace.TuningFor(_state.Team(offense).Pace);
            var scaled = _engine._config.Actions.SetupActionMs * tuning.SetupActionMultiplier;

            return Math.Max(1L, (long)Math.Round(scaled, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Canlı oyun süresi tüketir ve <b>on oyuncuya</b> enerji yazar (05 §164,
        /// T12f): sahnede beş drain, yedek beş recovery. İki takım birden
        /// güncellenir çünkü drain/recovery oyuncuya özeldir, tarafa değil.
        /// </summary>
        private void ConsumeLive(TeamSide offense, long requestedMilliseconds)
        {
            if (requestedMilliseconds <= 0)
            {
                return;
            }

            // ONEMLI: istenen degil GERCEKTEN oynanan sure kullanilir. Periyot
            // sonunda bir aksiyonun suresi kalan oyun saatini asabilir; saat
            // sifirlanir ve fazlasi oynanmamistir. Enerji ve SecondsOnCourt da
            // oynanmamis kismi YAZMAMALIDIR — aksi halde toplam oynama suresi
            // "5 x elapsed" esitligini bozar (T12c).
            var consumed = Math.Min(
                requestedMilliseconds,
                Math.Max(0, _state.Clock.GameClockMs));

            if (consumed <= 0)
            {
                return;
            }

            var homeDrain = _engine._config.Pace.TuningFor(_state.Home.Pace).EnergyDrainMultiplier;
            var awayDrain = _engine._config.Pace.TuningFor(_state.Away.Pace).EnergyDrainMultiplier;

            _state = _state with
            {
                Clock = _state.Clock.ConsumeLiveTime(consumed),
                Home = _state.Home.WithPlayerStates(FatigueCalculator.Advance(
                    _engine._config.Fatigue,
                    _state.Home.PlayerStates,
                    _state.Home.Roster,
                    _state.Home.OnCourt,
                    consumed,
                    homeDrain)),
                Away = _state.Away.WithPlayerStates(FatigueCalculator.Advance(
                    _engine._config.Fatigue,
                    _state.Away.PlayerStates,
                    _state.Away.Roster,
                    _state.Away.OnCourt,
                    consumed,
                    awayDrain)),
            };
        }

        // ---------------------------------------------------------------- yardimci

        private void RecordTurnover(TeamSide offense, Guid playerId, long actionId, TurnoverKind kind)
        {
            var turnoverId = _state.NextTurnoverId;

            _state = _state with { NextTurnoverId = turnoverId + 1 };

            // M6 (D100): turnover nedeni ayri ayri sayilir. 08 §6 top kaybi
            // orani kalibrasyonda bu ayrimi kullaniyor.
            if (kind == TurnoverKind.LostBall)
            {
                Tally.TurnoversLostBall += 1;
            }
            else if (kind == TurnoverKind.OffensiveFoul)
            {
                Tally.TurnoversOffensiveFoul += 1;
            }
            else
            {
                Tally.TurnoversShotClockViolation += 1;
            }

            Emit(
                MatchEventType.Turnover,
                new TurnoverPayload(turnoverId, kind),
                possessionId: _state.Possession!.PossessionId,
                actionId: actionId,
                teamId: offense,
                playerId: playerId,
                secondaryPlayerId: null);
        }

        private void Abort(string reason)
        {
            _state = _state with
            {
                Possession = null,
                PendingShot = null,
                PendingFrees = null,
                Phase = MatchPhase.Aborted,
            };

            Emit(
                MatchEventType.MatchAborted,
                new MatchAbortedPayload(reason),
                possessionId: null,
                actionId: null,
                teamId: null,
                playerId: null,
                secondaryPlayerId: null);
        }

        private void UpdateTeam(TeamSide side, Func<TeamMatchState, TeamMatchState> update) =>
            _state = ReplaceTeam(side, update(_state.Team(side)));

        private MatchState ReplaceTeam(TeamSide side, TeamMatchState team) =>
            side == TeamSide.Home
                ? _state with { Home = team }
                : _state with { Away = team };

        private void Emit(
            MatchEventType type,
            MatchEventPayload payload,
            int? possessionId,
            long? actionId,
            TeamSide? teamId,
            Guid? playerId,
            Guid? secondaryPlayerId)
        {
            Events.Add(new MatchEvent
            {
                MatchId = _state.Setup.MatchId,
                Sequence = _state.NextSequence,
                SchemaVersion = MatchSimulation.EventSchemaVersion,
                EngineVersion = EngineVersion.Current,
                ConfigHash = _engine._configHash,
                Type = type,
                Period = _state.Clock.Period,
                GameClockMs = _state.Clock.GameClockMs,
                ElapsedGameTimeMs = _state.Clock.ElapsedGameTimeMs,
                Payload = payload,
                PossessionId = possessionId,
                ActionId = actionId,
                TeamId = teamId,
                PlayerId = playerId,
                SecondaryPlayerId = secondaryPlayerId,
            });

            _state = _state with { NextSequence = _state.NextSequence + 1 };
        }
    }
}
