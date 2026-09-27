using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// Determinizm ve event akışı bütünlüğü. 08'in T01/T02 testleri ve
/// 07 §3'teki sözleşme kuralları.
/// </summary>
public class DeterminismTests
{
    [Fact]
    public void SameSetupAndSeedProduceAnIdenticalEventStream()
    {
        // T01: aynı setup + config + seed -> aynı canonical event akışı.
        var first = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror());
        var second = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror());

        Assert.Equal(
            M2TestData.Fingerprint(first.Events),
            M2TestData.Fingerprint(second.Events));
        Assert.Equal(first.HomeScore, second.HomeScore);
        Assert.Equal(first.AwayScore, second.AwayScore);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentEventStreams()
    {
        // 08 section 59: different seeds are NOT required to produce different
        // scores; the event streams differing is the meaningful assertion.
        var first = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(1));
        var second = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(2));

        Assert.NotEqual(
            M2TestData.Fingerprint(first.Events),
            M2TestData.Fingerprint(second.Events));
    }

    [Fact]
    public void RosterInputOrderDoesNotChangeTheOutcome()
    {
        // T02: kadronun girdi sırası sonucu değiştirmemelidir. Kanonik sıralama
        // (RosterOrdering) bunun tek kaynağıdır.
        var forward = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.WithRosterOrder(555, reverse: false));
        var reversed = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.WithRosterOrder(555, reverse: true));

        Assert.Equal(
            M2TestData.Fingerprint(forward.Events),
            M2TestData.Fingerprint(reversed.Events));
    }

    [Fact]
    public void DifferentConfigChangesTheOutcome()
    {
        // H04: seed tek başına maçı tanımlamaz.
        var baseline = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(88));

        var slower = M2TestData.Config().Actions with { SetupActionMs = 11_000 };
        var changed = new MatchSimulation(M2TestData.Config(actions: slower)).Simulate(M2TestData.NeutralMirror(88));

        Assert.NotEqual(
            M2TestData.Fingerprint(baseline.Events),
            M2TestData.Fingerprint(changed.Events));
    }

    [Fact]
    public void ManualAdvanceLoopMatchesSimulate()
    {
        // H03: Simulate ikinci bir algoritma değildir; Advance'in döngüsüdür.
        // İki yol ayrışırsa gizli state veya iki motor sınıfı vardır.
        var engine = new MatchSimulation(M2TestData.Config());
        var setup = M2TestData.NeutralMirror(4321);
        var direct = engine.Simulate(setup);

        var state = engine.Create(setup);
        var events = new List<MatchEvent>();

        while (!state.IsTerminal)
        {
            var step = engine.Advance(state);
            state = step.State;
            events.AddRange(step.Events);
        }

        Assert.Equal(
            M2TestData.Fingerprint(direct.Events),
            M2TestData.Fingerprint(events));
    }

    [Fact]
    public void SequenceStartsAtOneAndHasNoGaps()
    {
        foreach (var seed in new ulong[] { 3, 12_345, 4_242 })
        {
            var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(seed));

            for (var index = 0; index < result.Events.Length; index++)
            {
                Assert.Equal(index + 1, result.Events[index].Sequence);
            }
        }
    }

    [Fact]
    public void EveryEventBelongsToTheMatch()
    {
        var setup = M2TestData.NeutralMirror();
        var result = new MatchSimulation(M2TestData.Config()).Simulate(setup);

        Assert.All(result.Events, matchEvent =>
        {
            Assert.Equal(setup.MatchId, matchEvent.MatchId);
            Assert.Equal(EngineVersion.Current, matchEvent.EngineVersion);
            Assert.Equal(MatchSimulation.EventSchemaVersion, matchEvent.SchemaVersion);
            Assert.False(string.IsNullOrWhiteSpace(matchEvent.ConfigHash));
        });
    }

    [Fact]
    public void PayloadTypeAlwaysMatchesTheEventType()
    {
        // 07 §1: payload türüne göre doğrulanır. Uyuşmazlık sessizce yanlış
        // istatistik yazdırır.
        var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror());

        foreach (var matchEvent in result.Events)
        {
            var expected = matchEvent.Type switch
            {
                MatchEventType.MatchStarted => typeof(MatchStartedPayload),
                MatchEventType.PeriodStarted => typeof(PeriodStartedPayload),
                MatchEventType.PeriodEnded => typeof(PeriodEndedPayload),
                MatchEventType.MatchEnded => typeof(MatchEndedPayload),
                MatchEventType.MatchAborted => typeof(MatchAbortedPayload),
                MatchEventType.PossessionStarted => typeof(PossessionStartedPayload),
                MatchEventType.PossessionEnded => typeof(PossessionEndedPayload),
                MatchEventType.ActionCompleted => typeof(ActionCompletedPayload),
                MatchEventType.ShotAttempt => typeof(ShotAttemptPayload),
                MatchEventType.ShotMade => typeof(ShotMadePayload),
                MatchEventType.ShotMissed => typeof(ShotMissedPayload),
                MatchEventType.Rebound => typeof(ReboundPayload),
                MatchEventType.Turnover => typeof(TurnoverPayload),
                _ => throw new InvalidOperationException($"Bilinmeyen event türü: {matchEvent.Type}"),
            };

            Assert.Equal(expected, matchEvent.Payload.GetType());
        }
    }

    [Fact]
    public void MismatchedPayloadAccessFailsLoudly()
    {
        var matchEvent = new MatchEvent
        {
            MatchId = Guid.NewGuid(),
            Sequence = 1,
            SchemaVersion = 1,
            EngineVersion = EngineVersion.Current,
            ConfigHash = "test",
            Type = MatchEventType.ShotMade,
            Period = 1,
            GameClockMs = 0,
            ElapsedGameTimeMs = 0,
            Payload = new ShotMissedPayload(1, Config.ShotType.AtRim, true),
        };

        var failure = Assert.Throws<InvalidOperationException>(
            () => matchEvent.PayloadAs<ShotMadePayload>());

        Assert.Contains("ShotMade", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventClocksNeverRunBackwards()
    {
        foreach (var seed in new ulong[] { 11, 12_345, 999 })
        {
            var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(seed));

            var previousElapsed = -1L;

            foreach (var matchEvent in result.Events)
            {
                Assert.True(
                    matchEvent.ElapsedGameTimeMs >= previousElapsed,
                    "Event akışında oynanan süre geriye gitti.");
                Assert.True(matchEvent.GameClockMs >= 0);
                Assert.True(matchEvent.ElapsedGameTimeMs >= 0);
                Assert.InRange(matchEvent.Period, 1, 4);

                previousElapsed = matchEvent.ElapsedGameTimeMs;
            }
        }
    }

    [Fact]
    public void EveryProducedEventIsObservable()
    {
        // Zaman ilerleyen ama event üretmeyen bir adım, akışı görünmez bırakır ve
        // motor "ilerleme yok" güvenlik ağına takılır. Her aksiyon tam olarak bir
        // sonuç event'i üretmelidir.
        var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(246));

        var actionIds = result.Events
            .Where(e => e.Type is MatchEventType.Turnover
                or MatchEventType.ShotAttempt
                or MatchEventType.ActionCompleted)
            .Select(e => e.ActionId!.Value)
            .OrderBy(id => id)
            .ToList();

        Assert.NotEmpty(actionIds);
        Assert.Equal(actionIds.Count, actionIds.Distinct().Count());
        Assert.Equal(1, actionIds[0]);
    }

    [Fact]
    public void ShotClockIsResetForEveryNewPossession()
    {
        // Regression guard. If the shot clock is not reset for a new possession, the
        // first possession that exhausts it leaves zero behind and almost every
        // later possession ends in an instant violation: the match degenerates into
        // turnovers with almost no shots. The observable signature of that bug is a
        // very high violation share and a very low field goal attempt count.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var result = simulation.Simulate(M2TestData.NeutralMirror(64));

        var possessions = result.Events.Count(e => e.Type == MatchEventType.PossessionStarted);
        var violations = result.Events.Count(e =>
            e.Type == MatchEventType.Turnover
            && e.PayloadAs<TurnoverPayload>().Kind == TurnoverKind.ShotClockViolation);
        var attempts = result.Events.Count(e => e.Type == MatchEventType.ShotAttempt);

        Assert.True(possessions > 100, $"Yeterli possession gorulmedi: {possessions}");

        var violationShare = (double)violations / possessions;

        Assert.True(
            violationShare < 0.35,
            $"Hucum saati ihlali orani cok yuksek: {violations}/{possessions} = {violationShare:P1}");

        Assert.True(
            attempts > 100,
            $"Sut denemesi sayisi cok az: {attempts}. Hucre saati sifirlanmamis olabilir.");
    }
}
