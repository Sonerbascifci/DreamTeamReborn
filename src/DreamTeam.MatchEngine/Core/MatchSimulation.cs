using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Projection;
using DreamTeam.MatchEngine.Randomness;

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
///   <item><description>top kaybi (1)</description></item>
///   <item><description>faul olma (1)</description></item>
///   <item><description>suta donusme (1)</description></item>
///   <item><description>hucre faulu (1) — yalniz faul varsa</description></item>
///   <item><description>shooting faulu (1) — yalniz faul ve sut varsa</description></item>
///   <item><description>cember teması (1) — yalniz sut varsa</description></item>
///   <item><description>blok (1) — yalniz sut varsa</description></item>
///   <item><description>isabet (1) — yalniz sut varsa ve bloklanmadıysa</description></item>
///   <item><description>serbest atis (1) — her atis icin</description></item>
///   <item><description>ribaund (1) + ribaund alan (1) — yalniz canli miss sonrasi</description></item>
/// </list>
/// Bu sira degistirilirse tum golden sonuclar degisir; sira testlerle sabitlenir.
/// Blok gerceklesirse isabet cekilisi <b>tuketilmez</b>: ayni sut iki kez
/// orneklenmez (05 §127).
/// </summary>
public sealed class MatchSimulation
{
    public const int EventSchemaVersion = 2;

    private readonly EngineConfig _config;
    private readonly string _configHash;
    private readonly ShotResolver _shotResolver;
    private readonly TurnoverResolver _turnoverResolver;
    private readonly ReboundResolver _reboundResolver;
    private readonly FoulResolver _foulResolver;
    private readonly BlockResolver _blockResolver;
    private readonly RimContactResolver _rimContactResolver;
    private readonly FreeThrowResolver _freeThrowResolver;

    public MatchSimulation(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _configHash = config.ComputeConfigHash();
        _shotResolver = new ShotResolver(config.Shot);
        _turnoverResolver = new TurnoverResolver(config.Actions);
        _reboundResolver = new ReboundResolver(config.Actions);
        _foulResolver = new FoulResolver(config.Fouls);
        _blockResolver = new BlockResolver(config.Shot);
        _rimContactResolver = new RimContactResolver(config.Shot);
        _freeThrowResolver = new FreeThrowResolver(config.FreeThrows);
    }

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

        var home = BuildTeamState(setup.Home, setup.HomeLineup, TeamSide.Home);
        var away = BuildTeamState(setup.Away, setup.AwayLineup, TeamSide.Away);

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
            NextSequence = 1,
            NextActionId = 1,
            NextShotId = 1,
            NextTurnoverId = 1,
            NextFoulId = 1,
            NextFTSeriesId = 1,
            TotalActionCount = 0,
            PossessionCount = 0,
            Random = new SeededRandom(setup.Seed),
        };
    }

    /// <summary>
    /// Bir sonraki anlamli sinra ilerler: periyot baslangici, bir aksiyonun
    /// basindaki adim, birakilmis sutun cozumu veya bir serbest atis.
    /// </summary>
    public StepResult Advance(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.IsTerminal)
        {
            return StepResult.NonTerminal(state, []);
        }

        var step = new Step(this, state);

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

        return StepResult.NonTerminal(step.State, [.. step.Events]);
    }

    /// <summary>
    /// <c>Create</c> + <c>Advance</c> dongusunu sonuna kadar calistirir. Konsol,
    /// batch deneyleri ve ileride canli runner hep bu yolu kullanir.
    ///
    /// Setup reddedilirse mac oynanmaz; <c>Aborted</c> sonuc ve gerekce doner.
    /// </summary>
    public MatchResult Simulate(MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var validation = MatchSetupValidator.Validate(setup);

        if (!validation.IsValid)
        {
            return AbortedResult(setup, Describe(validation));
        }

        var state = Create(setup);
        var events = new List<MatchEvent>();

        while (!state.IsTerminal)
        {
            var step = Advance(state);
            state = step.State;
            events.AddRange(step.Events);

            if (step.Events.Length == 0 && !state.IsTerminal)
            {
                // Ilerleme yoksa sonsuz dongu riski olusur. 06 §2: sonsuz dongu
                // skoru bozmaz, maci Aborted yapar.
                return AbortedResult(setup, "Ilerleme yok; motor durdu. Bu bir hata durumudur.", events);
            }
        }

        return Project(setup, state, events);
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
            AbortReason = abortReason,
        };
    }

    private static TeamMatchState BuildTeamState(Team team, Lineup lineup, TeamSide side)
    {
        var byId = new Dictionary<Guid, Player>();

        foreach (var player in team.Roster)
        {
            byId[player.Id] = player;
        }

        var onCourt = ImmutableArray.CreateBuilder<Player>(lineup.PlayerIds.Length);

        foreach (var playerId in lineup.PlayerIds)
        {
            if (!byId.TryGetValue(playerId, out var player))
            {
                throw new InvalidOperationException(
                    $"Doğrulanmış setup'ta lineup oyuncusu kadroda bulunamadı: {playerId} ({side}).");
            }

            onCourt.Add(player);
        }

        return new TeamMatchState
        {
            Side = side,
            Team = team,
            Roster = RosterOrdering.Canonical(team.Roster),
            OnCourt = onCourt.ToImmutable(),
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

        public MatchState State => _state;

        // ---------------------------------------------------------------- periyot

        public void StartPeriod()
        {
            var period = _state.Clock.Period + 1;
            var isFirstPeriod = period == 1;
            var rules = _engine._config.Rules;

            var clock = _state.Clock.BeginPeriod(
                period,
                PeriodController.DurationMsForPeriod(rules, period),
                ClockResetPolicy.ForNewPossession(rules));

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

        public void RunAction()
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

            var attacking = _state.Team(offense);
            var selector = new ActionSelector(_engine._config.ActionProfiles, attacking.OnCourt);

            var actionId = _state.NextActionId;
            var selection = selector.Select(_state.Random);

            _state = _state with
            {
                NextActionId = actionId + 1,
                TotalActionCount = _state.TotalActionCount + 1,
                Possession = _state.Possession! with
                {
                    ActionCount = _state.Possession!.ActionCount + 1,
                },
            };

            // 1) top kaybi
            var turnover = _engine._turnoverResolver.Resolve(_state.Random);

            if (turnover.IsTurnover)
            {
                _state = _state.Consume(_engine._config.Actions.SetupActionMs);
                RecordTurnover(offense, selection.PlayerId, actionId, TurnoverKind.LostBall);
                EndPossession(PossessionEndReason.Turnover);
                HandleAfterPossession(offense);
                return;
            }

            _state = _state.Consume(_engine._config.Actions.SetupActionMs);

            if (_state.Clock.IsGameTimeExhausted)
            {
                EndPossession(PossessionEndReason.PeriodExpired);
                ClosePeriod();
                return;
            }

            // 2) faul olma
            var foul = _engine._foulResolver.Occurred(_state.Random);

            // 3) suta donusme
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
                        PickDefender(offense),
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

            ReleaseShot(offense, selection, actionId, foul);
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
            FoulOutcome foul)
        {
            var shotId = _state.NextShotId;
            var foulId = _state.NextFoulId;
            long shooterFoulId = 0;
            var foulType = FoulType.NonShooting;

            // 5) hucre faulu — ShotAttempt ONCESI cozulur.
            if (foul.IsFoul)
            {
                if (_engine._foulResolver.IsOffensive(_state.Random))
                {
                    ApplyOffensiveFoul(offense, foulId, selection, actionId);
                    return;
                }

                // 6) shooting faulu: yalniz faul ve sut varsa anlamlidir
                var isShooting = _engine._foulResolver.IsShooting(_state.Random);

                if (isShooting)
                {
                    foulType = FoulType.Shooting;
                    shooterFoulId = foulId;
                }
            }

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
                new ShotAttemptPayload(shotId, selection.ShotType),
                possessionId: _state.Possession!.PossessionId,
                actionId: actionId,
                teamId: offense,
                playerId: selection.PlayerId,
                secondaryPlayerId: null);

            if (shooterFoulId != 0)
            {
                var defenderId = PickDefender(offense);
                ApplyFoul(offense, foulId, defenderId, foulType, 0, selection.PlayerId, actionId);

                // Faul foul-out'a yol actiysa ve yasal yedek yoksa motor Aborted
                // olmustur. Bu durumun uzerine PendingShot yazmak terminal durumu
                // ezer ve sonraki adim "shoot var ama possession yok" ile patlar.
                if (_state.IsTerminal)
                {
                    return;
                }
            }

            // 7) cember teması (06 §6 reset tablosu)
            var rimContact = _engine._rimContactResolver.TouchedRim(_state.Random);

            // Ucus suresi canli oyun suresidir.
            _state = _state.Consume(_engine._config.Actions.ShotFlightMs);

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
                    ShotType = selection.ShotType,
                    SkillRating = selection.SkillRating,
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

            // 8) blok
            var isBlocked = _engine._blockResolver.IsBlocked(_state.Random);

            Guid? blockerId = null;

            if (isBlocked)
            {
                blockerId = PickDefender(offense);
            }

            // 9) isabet: blok gerceklesmisse cekilis tuketilmez
            var isMade = false;

            if (!isBlocked)
            {
                var outcome = _engine._shotResolver.Resolve(
                    pending.ShotType,
                    pending.SkillRating,
                    _state.Random);

                isMade = outcome.IsMade;
            }

            // 06 §87: kacan shooting foul'da FGA sayilmaz. Isabetli shooting
            // foul'da (and-one) FGA ve FGM yazilir.
            var hasFoul = pending.FoulId != 0;
            var countsAsFieldGoalAttempt = !hasFoul || isMade;

            if (isBlocked)
            {
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

            if (isMade)
            {
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
                _state = _state with { Phase = MatchPhase.LiveBall };
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

        private Guid PickDefender(TeamSide offense)
        {
            var defenders = _state.Team(offense.Opponent()).OnCourt;

            if (defenders.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException("Savunma sahada oyuncu yok.");
            }

            return WeightedSelector.Select(
                defenders,
                player => player.Ratings.Block + 1.0,
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
                player => player.Ratings.Passing + 1.0,
                _state.Random).Id;
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

        // ---------------------------------------------------------------- yardimci

        private void RecordTurnover(TeamSide offense, Guid playerId, long actionId, TurnoverKind kind)
        {
            var turnoverId = _state.NextTurnoverId;

            _state = _state with { NextTurnoverId = turnoverId + 1 };

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
