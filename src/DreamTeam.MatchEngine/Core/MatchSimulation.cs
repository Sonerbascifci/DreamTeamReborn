using System.Collections.Immutable;
using System.Text;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Projection;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Possession → action → shot/turnover/rebound çekirdeği. 03'teki sözleşmenin M2
/// hâli: <c>Create</c> / <c>Advance</c> / <c>Simulate</c>.
///
/// H03 gereği offline ve canlı yürütme <b>aynı</b> çekirdeği kullanır; burada ikinci
/// bir simülasyon algoritması yoktur. Motor duvar saati okumaz, beklemez, dosya
/// yazmaz ve loglamaz.
///
/// Determinizm sözleşmesi — her aksiyon şu sırayla RNG tüketir:
/// <list type="number">
///   <item><description><c>ActionSelector</c>: aksiyon (1) + oyuncu (1)</description></item>
///   <item><description><c>TurnoverResolver</c>: top kaybı (1)</description></item>
///   <item><description>şut denemesine dönüşme (1)</description></item>
///   <item><description>asist (1) — yalnız şut atıldıysa</description></item>
///   <item><description><c>ShotResolver</c>: isabet (1) — yalnız şut atıldıysa</description></item>
///   <item><description><c>ReboundResolver</c>: OREB/DREB (1) + ribaund alan (1) — yalnız canlı miss ise</description></item>
/// </list>
/// Bu sıra değiştirilirse tüm golden sonuçlar değişir; sıra testlerle sabitlenir.
/// </summary>
public sealed class MatchSimulation
{
    public const int EventSchemaVersion = 1;

    private readonly EngineConfig _config;
    private readonly string _configHash;
    private readonly ShotResolver _shotResolver;
    private readonly TurnoverResolver _turnoverResolver;
    private readonly ReboundResolver _reboundResolver;

    public MatchSimulation(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _configHash = config.ComputeConfigHash();
        _shotResolver = new ShotResolver(config.Shot);
        _turnoverResolver = new TurnoverResolver(config.Actions);
        _reboundResolver = new ReboundResolver(config.Actions);
    }

    public string ConfigHash => _configHash;

    /// <summary>
    /// Doğrulanmış bir setup'tan başlangıç durumu üretir.
    ///
    /// <paramref name="setup"/> <see cref="MatchSetupValidator"/> tarafından kabul
    /// edilmiş olmalıdır. Doğrulama <b>bu metodun sorumluluğu değildir</b>: kullanıcı
    /// girdisinin reddi istisnayla değil, <see cref="MatchSetupValidationResult"/> ile
    /// bildirilir (D26). <see cref="Simulate"/> bu sözleşmeyi kendisi uygular;
    /// doğrudan <c>Create</c> çağıran, geçersiz setup geçirirse program hatası
    /// yapmış olur ve bu bir <see cref="InvalidOperationException"/> ile belirtilir.
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
            NextSequence = 1,
            NextActionId = 1,
            NextShotId = 1,
            NextTurnoverId = 1,
            TotalActionCount = 0,
            PossessionCount = 0,
            Random = new SeededRandom(setup.Seed),
        };
    }

    /// <summary>
    /// Bir sonraki anlamlı sınra ilerler: periyot başlangıcı, bir aksiyonun tamamı veya
    /// terminal durum. Yarıda kalmış şut veya serbest atış yoktur (M3 konusu).
    /// </summary>
    public StepResult Advance(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.IsTerminal)
        {
            return StepResult.NonTerminal(state, []);
        }

        var step = new Step(this, state);

        if (state.Phase is MatchPhase.NotStarted or MatchPhase.PeriodBreak)
        {
            step.StartPeriod();
        }

        if (!step.State.IsTerminal)
        {
            step.RunAction();
        }

        return StepResult.NonTerminal(step.State, [.. step.Events]);
    }

    /// <summary>
    /// <c>Create</c> + <c>Advance</c> döngüsünü sonuna kadar çalıştırır. Konsol,
    /// batch deneyleri ve ileride canlı runner hep bu yolu kullanır.
    ///
    /// Setup reddedilirse maç oynanmaz; <c>Aborted</c> sonuç ve gerekçe döner.
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
                // İlerleme yoksa sonsuz döngü riski oluşur. 06 §2: sonsuz döngü
                // skoru bozmaz, maçı Aborted yapar.
                return AbortedResult(setup, "İlerleme yok; motor durdu. Bu bir hata durumudur.", events);
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
        };
    }

    /// <summary>
    /// Tek bir <c>Advance</c> adımının çalışma bağlamı. Event'leri toplar ve zarfı
    /// tek noktadan damgalar; böylece hiçbir event eksik veya tutarsız zarla üretilemez.
    /// </summary>
    private sealed class Step(MatchSimulation engine, MatchState state)
    {
        private readonly MatchSimulation _engine = engine;
        private MatchState _state = state;

        public List<MatchEvent> Events { get; } = [];

        public MatchState State => _state;

        public void StartPeriod()
        {
            var period = _state.Clock.Period + 1;
            var isFirstPeriod = period == 1;

            var clock = _state.Clock.BeginPeriod(
                period,
                _engine._config.Rules.PeriodDurationMs,
                _engine._config.Rules.ShotClockMs);

            _state = _state with
            {
                Clock = clock,
                Phase = MatchPhase.LiveBall,
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
                new PeriodStartedPayload(period, _engine._config.Rules.PeriodDurationMs, IsOvertime: false),
                possessionId: null,
                actionId: null,
                teamId: null,
                playerId: null,
                secondaryPlayerId: null);

            StartPossession(DrawStarterSide());
        }

        public void RunAction()
        {
            if (_state.TotalActionCount >= _engine._config.MaxActionsPerMatch)
            {
                Abort(
                    $"Eylem guard'ı aşıldı: {_state.TotalActionCount} >= "
                    + $"{_engine._config.MaxActionsPerMatch}. Skor geçerli değildir.");
                return;
            }

            var offense = _state.Possession?.Offense ?? throw new InvalidOperationException("Aksiyon yok: possession yok.");
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

            var turnover = _engine._turnoverResolver.Resolve(_state.Random);

            if (turnover.IsTurnover)
            {
                _state = _state.Consume(_engine._config.Actions.SetupActionMs);
                RecordTurnover(offense, selection.PlayerId, actionId, TurnoverKind.LostBall);
                EndPossession(PossessionEndReason.Turnover);
                HandleClockAfterPossession(offense);
                return;
            }

            // Aksiyonun kurulum aşaması canlı süre tüketir.
            _state = _state.Consume(_engine._config.Actions.SetupActionMs);

            if (_state.Clock.IsGameTimeExhausted)
            {
                EndPossession(PossessionEndReason.PeriodExpired);
                ClosePeriod();
                return;
            }

            var completesIntoShot = _state.Random.NextDouble() < _engine._config.Actions.ShotCompletionProbability;

            if (!completesIntoShot)
            {
                if (_state.Clock.IsShotClockExhausted)
                {
                    RecordTurnover(offense, selection.PlayerId, actionId, TurnoverKind.ShotClockViolation);
                    EndPossession(PossessionEndReason.Turnover);
                    HandleClockAfterPossession(offense);
                    return;
                }

                // Aksiyon şutla sonuçlanmadı; possession canlı devam eder. Zaman
                // ilerlediği için bu adım event akışında görünür olmalıdır.
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

            if (_state.Clock.IsShotClockExhausted)
            {
                RecordTurnover(offense, selection.PlayerId, actionId, TurnoverKind.ShotClockViolation);
                EndPossession(PossessionEndReason.Turnover);
                HandleClockAfterPossession(offense);
                return;
            }

            AttemptShot(offense, selection, actionId);
        }

        private void AttemptShot(TeamSide offense, ActionSelection selection, long actionId)
        {
            var shotId = _state.NextShotId;
            _state = _state with
            {
                NextShotId = shotId + 1,
                Possession = _state.Possession! with
                {
                    ShotCount = _state.Possession!.ShotCount + 1,
                },
            };

            var shooterId = selection.PlayerId;

            Emit(
                MatchEventType.ShotAttempt,
                new ShotAttemptPayload(shotId, selection.ShotType),
                possessionId: _state.Possession!.PossessionId,
                actionId: actionId,
                teamId: offense,
                playerId: shooterId,
                secondaryPlayerId: null);

            var assisterId = SelectAssister(offense, shooterId);

            // Uçuş süresi canlı oyun süresidir; saat burada da ilerler.
            _state = _state.Consume(_engine._config.Actions.ShotFlightMs);

            var outcome = _engine._shotResolver.Resolve(
                selection.ShotType,
                selection.SkillRating,
                _state.Random);

            if (outcome.IsMade)
            {
                Emit(
                    MatchEventType.ShotMade,
                    new ShotMadePayload(shotId, selection.ShotType, outcome.Points, CountsAsFieldGoalAttempt: true),
                    possessionId: _state.Possession!.PossessionId,
                    actionId: actionId,
                    teamId: offense,
                    playerId: shooterId,
                    secondaryPlayerId: assisterId);

                _state = _state with
                {
                    HomeScore = offense == TeamSide.Home ? _state.HomeScore + outcome.Points : _state.HomeScore,
                    AwayScore = offense == TeamSide.Away ? _state.AwayScore + outcome.Points : _state.AwayScore,
                };

                EndPossession(PossessionEndReason.Scored);
                HandleClockAfterPossession(offense);
                return;
            }

            Emit(
                MatchEventType.ShotMissed,
                new ShotMissedPayload(shotId, selection.ShotType, CountsAsFieldGoalAttempt: true),
                possessionId: _state.Possession!.PossessionId,
                actionId: actionId,
                teamId: offense,
                playerId: shooterId,
                secondaryPlayerId: null);

            if (_state.Clock.IsGameTimeExhausted)
            {
                // Periyot bitti: 06 §5, periyot sonunda ribaund üretilmez.
                EndPossession(PossessionEndReason.PeriodExpired);
                ClosePeriod();
                return;
            }

            ResolveRebound(offense, shotId, actionId);
        }

        private void ResolveRebound(TeamSide offense, long shotId, long actionId)
        {
            var attacking = _state.Team(offense);
            var rebound = _engine._reboundResolver.Resolve(attacking.OnCourt, _state.Random);

            Emit(
                MatchEventType.Rebound,
                new ReboundPayload(shotId, rebound.Offensive, IsTeamRebound: false),
                possessionId: _state.Possession!.PossessionId,
                actionId: actionId,
                teamId: offense,
                playerId: rebound.RebounderId,
                secondaryPlayerId: null);

            if (rebound.Offensive)
            {
                // 06 §4: OREB possession'ı bitirmez; aynı kimlik devam eder (T05).
                return;
            }

            EndPossession(PossessionEndReason.DefensiveRebound);
            HandleClockAfterPossession(offense);
        }

        /// <summary>
        /// İsabetli şut için asist atfedilir. Asist, sahadaki başka bir oyuncudan
        /// <c>Passing + 1</c> ağırlığıyla seçilir.
        ///
        /// Bu ayrı bir RNG çağrısıdır ve çağrı sırası sabittir. Burada rastgele
        /// "asist yok" kararı verilmez: M2'de her isabetli şutta bir asist atfedilir.
        /// Gelecekte asist opsiyonelliği eklenirse bu, açık bir olasılık olur.
        /// </summary>
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

        private void StartPossession(TeamSide offense)
        {
            var possessionId = _state.PossessionCount + 1;

            // 06 §6: yeni hücum 24 saniyelik hücum saatiyle başlar. OREB bu
            // sıfırlamayı YAPMAZ — hücum kimliği korunur ve kalan süre devreder.
            // Saat sıfırlanmazsa ilk hücumu tüketen saat sonraki her hücumu anında
            // ihlal ettirir.
            _state = _state with
            {
                Clock = _state.Clock with
                {
                    ShotClockMs = _engine._config.Rules.ShotClockMs,
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

            _state = _state with
            {
                Possession = null,
                Phase = _state.Phase == MatchPhase.LiveBall ? MatchPhase.DeadBall : _state.Phase,
            };
        }

        /// <summary>Possession kapandıktan sonra periyot/terminal durumu ele alır.</summary>
        private void HandleClockAfterPossession(TeamSide previousOffense)
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
                // 06 §4: topu devreden taraf yeni hücumu başlatır. Devir, canlı
                // süre tüketmez; bu yüzden state'e DeadBall yazılmadan bir sonraki
                // possession doğrudan başlar.
                _state = _state with { Phase = MatchPhase.LiveBall };
                StartPossession(previousOffense.Opponent());
            }
        }

        private void EndPossessionIfOpen(PossessionEndReason reason)
        {
            if (_state.Possession is not null)
            {
                EndPossession(reason);
            }
        }

        private void ClosePeriod()
        {
            if (_state.Clock.IsGameTimeExhausted)
            {
                EndPossessionIfOpen(PossessionEndReason.PeriodExpired);
            }

            Emit(
                MatchEventType.PeriodEnded,
                new PeriodEndedPayload(_state.Clock.Period, IsOvertime: false),
                possessionId: null,
                actionId: null,
                teamId: null,
                playerId: null,
                secondaryPlayerId: null);

            if (_state.Clock.Period >= _engine._config.Rules.PeriodCount)
            {
                // M2'de uzatma yoktur. Eşitlikte kazanan seçilmez (06 §8).
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

        private void Abort(string reason)
        {
            _state = _state with
            {
                Possession = null,
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

        /// <summary>
        /// Periyot açılışını belirler (D32). Tek bir bit çekilir: modulo bias yoktur
        /// ve aynı seed aynı başlangıcı verir. 06 §4, home/away bias yaratmayacak
        /// seeded yöntem ister; toplu denge M6'nın işidir.
        /// </summary>
        private TeamSide DrawStarterSide() =>
            (_state.Random.NextUInt64() & 1UL) == 0UL ? TeamSide.Home : TeamSide.Away;

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

