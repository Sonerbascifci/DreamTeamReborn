using System.Collections.Immutable;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Fatigue;
using DreamTeam.MatchEngine.Projection;

namespace DreamTeam.Simulator.Batch;

/// <summary>
/// M6: runs one match through the engine's <c>Create</c> + <c>Advance</c> loop and
/// folds the events into a <see cref="MatchSummary"/> as they arrive.
/// <b><c>MatchResult</c> is never constructed.</b>
///
/// <para><b>Why this is not a second simulation.</b> It calls the exact same two
/// public methods that <c>MatchSimulation.Simulate</c> calls, in the same order,
/// with the same command handling. The only difference is what happens to the
/// events: <c>Simulate</c> appends them to a <c>List</c>, this folds each one into
/// a projector and drops it. H03 is preserved — there is one engine core, and
/// <c>MatchRunnerProducesTheSameOutcomeAsSimulate</c> proves the two agree on real
/// matches.</para>
///
/// <para><b>Why the guards are duplicated.</b> <c>Simulate</c> has a "no progress"
/// guard that aborts rather than spinning forever. A batch driver that omitted it
/// would hang on 100K matches, so the same guard and the same reason string are
/// reproduced here. Duplicated on purpose, with a comment, rather than
/// forgotten.</para>
///
/// <para><b>Thread safety.</b> A <c>MatchSimulation</c> holds only readonly
/// resolvers, so one instance can be shared by every worker. A <c>MatchRunner</c>
/// is NOT shared: it holds no mutable state, but the caller gives each worker its
/// own so that nothing has to be argued about.</para>
/// </summary>
public sealed class MatchRunner
{
    private readonly MatchSimulation _simulation;

    public MatchRunner(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _simulation = new MatchSimulation(config);
    }

    /// <summary>The engine's own config hash. This is the value 08 §119 wants recorded.</summary>
    public string ConfigHash => _simulation.ConfigHash;

    /// <summary>
    /// Plays one match. No commands are sent, so the engine manages the match on
    /// its own (D81: the AI fallback is "the engine plays it", not a takeover mode).
    /// </summary>
    public MatchSummary Run(MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var validation = MatchSetupValidator.Validate(setup);

        if (!validation.IsValid)
        {
            return RejectedSummary(setup, Describe(validation));
        }

        var projector = new BoxScoreProjector(setup);
        var state = _simulation.Create(setup);
        var eventCount = 0;
        var homePossessions = 0;
        var awayPossessions = 0;
        // Iki AYRI ornek. Empty tek bir paylasilan nesne donerse ikisi de
        // ayni nesneyi gosterir ve iki taraf tek yere karisir.
        var homeShots = ShotTypeTally.Empty;
        var awayShots = ShotTypeTally.Empty;
        string? abortReason = null;

        while (!state.IsTerminal)
        {
            var step = _simulation.Advance(state);
            state = step.State;
            eventCount += step.Events.Length;

            foreach (var matchEvent in step.Events)
            {
                // Event'ler burada ATILIR. Bellek maç başına O(1)'de kalır.
                projector.Accumulate(matchEvent);
                Tally(matchEvent, ref homeShots, ref awayShots);

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

            if (step.Events.Length == 0 && !state.IsTerminal)
            {
                // `Simulate` burada ayni nedenle ayni korumayi yapar. Aksi halde
                // 100K kosuda sonsuz dongu olurdu (06 §2).
                return AbortedSummary(
                    setup, "Ilerleme yok; motor durdu. Bu bir hata durumudur.", projector,
                    homePossessions, awayPossessions, state, eventCount, homeShots, awayShots);
            }
        }

        var projection = projector.Project([]);

        return Build(
            setup, state, projection, homePossessions, awayPossessions, eventCount, abortReason,
            homeShots, awayShots);
    }

    /// <summary>
    /// M6: per-shot-type counters, straight off the event stream. Attempts come
    /// from <c>ShotAttempt</c> and misses from <c>ShotMissed</c>, so a missed
    /// shooting foul is an attempt and a miss but NOT a field goal — the same
    /// distinction the engine's own counters make (08 §2).
    /// </summary>
    private static void Tally(
        MatchEvent matchEvent,
        ref ShotTypeTally home,
        ref ShotTypeTally away)
    {
        var target = matchEvent.TeamId == TeamSide.Away ? ref away : ref home;

        switch (matchEvent.Type)
        {
            case MatchEventType.ShotAttempt:
                switch (matchEvent.PayloadAs<ShotAttemptPayload>().ShotType)
                {
                    case ShotType.AtRim:
                        target.AtRimAttempts += 1;
                        break;

                    case ShotType.ClosePost:
                        target.ClosePostAttempts += 1;
                        break;

                    case ShotType.MidRange:
                        target.MidRangeAttempts += 1;
                        break;

                    case ShotType.ThreePoint:
                        target.ThreePointAttempts += 1;
                        break;
                }

                break;

            case MatchEventType.ShotMissed:
                switch (matchEvent.PayloadAs<ShotMissedPayload>().ShotType)
                {
                    case ShotType.AtRim:
                        target.AtRimMisses += 1;
                        break;

                    case ShotType.ClosePost:
                        target.ClosePostMisses += 1;
                        break;

                    case ShotType.MidRange:
                        target.MidRangeMisses += 1;
                        break;

                    case ShotType.ThreePoint:
                        target.ThreePointMisses += 1;
                        break;
                }

                break;
        }
    }

    private static MatchSummary Build(
        MatchSetup setup,
        MatchState state,
        ProjectionResult projection,
        int homePossessions,
        int awayPossessions,
        int eventCount,
        string? abortReason,
        ShotTypeTally homeShots,
        ShotTypeTally awayShots)
    {
        var completed = state.Phase == MatchPhase.Completed;

        return new MatchSummary
        {
            MatchId = setup.MatchId,
            Seed = setup.Seed,
            Status = completed ? MatchStatus.Completed : MatchStatus.Aborted,
            HomeScore = state.HomeScore,
            AwayScore = state.AwayScore,
            IsTie = completed && state.HomeScore == state.AwayScore,
            HomePossessions = homePossessions,
            AwayPossessions = awayPossessions,
            ElapsedGameTimeMs = state.Clock.ElapsedGameTimeMs,
            PeriodsPlayed = state.Clock.Period,
            TotalActions = state.TotalActionCount,
            Home = TeamTotals.From(projection.Home, homePossessions),
            Away = TeamTotals.From(projection.Away, awayPossessions),
            HomeShots = homeShots,
            AwayShots = awayShots,
            Diagnostics = state.Diagnostics,
            PlayerEnergy = completed ? EnergySamples(state) : [],
            AbortReason = abortReason,
            EventCount = eventCount,
        };
    }

    private static MatchSummary AbortedSummary(
        MatchSetup setup,
        string reason,
        BoxScoreProjector projector,
        int homePossessions,
        int awayPossessions,
        MatchState state,
        int eventCount,
        ShotTypeTally homeShots,
        ShotTypeTally awayShots) => Build(
            setup,
            state,
            projector.Project([]),
            homePossessions,
            awayPossessions,
            eventCount,
            reason,
            homeShots,
            awayShots);

    private static MatchSummary RejectedSummary(MatchSetup setup, string reason) => new()
    {
        MatchId = setup.MatchId,
        Seed = setup.Seed,
        Status = MatchStatus.Aborted,
        HomeScore = 0,
        AwayScore = 0,
        IsTie = false,
        HomePossessions = 0,
        AwayPossessions = 0,
        ElapsedGameTimeMs = 0,
        PeriodsPlayed = 0,
        TotalActions = 0,
        Home = TeamTotals.Zero,
        Away = TeamTotals.Zero,
        HomeShots = ShotTypeTally.Empty,
        AwayShots = ShotTypeTally.Empty,
        Diagnostics = DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters.Empty,
        AbortReason = reason,
        EventCount = 0,
    };

    /// <summary>
    /// Final energy per player, in the same order the engine's own report uses
    /// (home roster order, then away). Fixed order keeps aggregation deterministic
    /// (08 §4).
    /// </summary>
    private static ImmutableArray<PlayerEnergySample> EnergySamples(MatchState state)
    {
        var builder = ImmutableArray.CreateBuilder<PlayerEnergySample>();

        foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
        {
            var team = state.Team(side);
            var names = new Dictionary<Guid, string>();

            foreach (var player in team.Roster)
            {
                names[player.Id] = player.DisplayName;
            }

            foreach (var playerState in team.PlayerStates)
            {
                builder.Add(new PlayerEnergySample(
                    playerState.PlayerId,
                    names.GetValueOrDefault(playerState.PlayerId, playerState.PlayerId.ToString()),
                    side,
                    FatigueCalculator.ForDisplay(playerState.Energy),
                    playerState.SecondsOnCourt));
            }
        }

        return builder.ToImmutable();
    }

    private static string Describe(MatchSetupValidationResult validation) =>
        "Geçersiz setup: "
        + string.Join("; ", validation.Errors.Select(error => $"{error.Field} ({error.Code})"));
}
