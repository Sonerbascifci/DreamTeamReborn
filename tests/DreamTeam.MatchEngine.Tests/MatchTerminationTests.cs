using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// Maçın sonlanması: periyot sayısı, guard, eşitlik ve geçersiz giriş.
/// 06 §2 ve §8'e dayanır.
/// </summary>
public class MatchTerminationTests
{
    [Fact]
    public void MatchCompletesAfterTheConfiguredPeriodCount()
    {
        var config = M2TestData.Config();
        var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror());

        Assert.Equal(MatchStatus.Completed, result.Status);
        Assert.Equal(config.Rules.PeriodCount, result.PeriodsPlayed);
        Assert.Null(result.AbortReason);

        var periodStarts = result.Events.Count(e => e.Type == MatchEventType.PeriodStarted);
        var periodEnds = result.Events.Count(e => e.Type == MatchEventType.PeriodEnded);

        Assert.Equal(config.Rules.PeriodCount, periodStarts);
        Assert.Equal(config.Rules.PeriodCount, periodEnds);
        Assert.Single(result.Events.Where(e => e.Type == MatchEventType.MatchEnded));
    }

    [Fact]
    public void MatchStartedIsEmittedExactlyOnceAndFirst()
    {
        var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror());

        Assert.Equal(MatchEventType.MatchStarted, result.Events[0].Type);
        Assert.Single(result.Events.Where(e => e.Type == MatchEventType.MatchStarted));
    }

    [Fact]
    public void MatchEndedIsTheLastEvent()
    {
        var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror());

        Assert.Equal(MatchEventType.MatchEnded, result.Events[^1].Type);

        var payload = result.Events[^1].PayloadAs<MatchEndedPayload>();

        Assert.Equal(result.HomeScore, payload.HomeScore);
        Assert.Equal(result.AwayScore, payload.AwayScore);
        Assert.Equal(result.IsTie, payload.IsTie);
    }

    [Fact]
    public void NoOvertimeIsInventedForATiedMatch()
    {
        // 06 §8: eşit maça rastgele kazanan seçilmez. M2'de uzatma yoktur, bu
        // yüzden eşitlik kazanan üretmeden Completed olur.
        var ties = 0;
        var completed = 0;

        for (var seed = 0UL; seed < 120UL; seed++)
        {
            var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(seed));

            if (result.Status != MatchStatus.Completed)
            {
                continue;
            }

            completed += 1;

            var payload = result.Events.Single(e => e.Type == MatchEventType.MatchEnded)
                .PayloadAs<MatchEndedPayload>();

            Assert.Equal(result.HomeScore == result.AwayScore, payload.IsTie);
            Assert.Equal(result.IsTie, payload.IsTie);

            if (result.IsTie)
            {
                ties += 1;
            }
        }

        Assert.True(ties > 0, "Hiç eşitlik oluşmadı; eşitlik yolu test edilemedi.");

        // Eşitlikte de tam 4 periyot oynanır; uzatma periyodu üretilmez.
        var tied = Enumerable.Range(0, (int)completed)
            .Select(index => new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror((ulong)index)))
            .First(r => r.Status == MatchStatus.Completed && r.IsTie);

        Assert.Equal(4, tied.PeriodsPlayed);
        Assert.DoesNotContain(tied.Events, e => e.Type == MatchEventType.PeriodStarted && e.PayloadAs<PeriodStartedPayload>().IsOvertime);
    }

    [Fact]
    public void GuardAbortsTheMatchWithoutFalsifyingTheScore()
    {
        // 06 §2: guard aşılırsa maç Aborted olur; skor uydurulup Completed
        // yapılmaz.
        var config = M2TestData.Config(maxActionsPerMatch: 25);
        var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror());

        Assert.Equal(MatchStatus.Aborted, result.Status);
        Assert.NotNull(result.AbortReason);
        Assert.Contains("guard", result.AbortReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.IsTie);

        var aborted = result.Events.Single(e => e.Type == MatchEventType.MatchAborted);
        Assert.Equal(result.AbortReason, aborted.PayloadAs<MatchAbortedPayload>().Reason);

        // Terminal event'ten sonra başka event üretilmez.
        Assert.Equal(MatchEventType.MatchAborted, result.Events[^1].Type);
        Assert.DoesNotContain(result.Events, e => e.Type == MatchEventType.MatchEnded);
    }

    [Fact]
    public void InvalidSetupIsRejectedBeforeAnyEventIsProduced()
    {
        var invalid = M2TestData.NeutralMirror() with { HomeLineup = new Domain.Teams.Lineup { PlayerIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()] } };

        var result = new MatchSimulation(M2TestData.Config()).Simulate(invalid);

        Assert.Equal(MatchStatus.Aborted, result.Status);
        Assert.NotNull(result.AbortReason);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.HomeScore);
        Assert.Equal(0, result.AwayScore);
    }

    [Fact]
    public void ShotClockExhaustionEndsThePossessionWithoutAKick()
    {
        // Hücum saati dolduğunda possession kapanmalı; aksi halde döngü sonsuzlaşır.
        // Bu, planda 09'un dört sonucuna eklenen beşinci güvenlik sonucudur.
        var actions = M2TestData.Config().Actions with { ShotCompletionProbability = 0.0 };
        var config = M2TestData.Config(actions: actions);
        var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror());

        Assert.Equal(MatchStatus.Completed, result.Status);

        var violations = result.Events
            .Where(e => e.Type == MatchEventType.Turnover)
            .Where(e => e.PayloadAs<TurnoverPayload>().Kind == TurnoverKind.ShotClockViolation)
            .ToList();

        Assert.NotEmpty(violations);

        // Her ihlal bir possession'ı kapatır ve top kaybı olarak sayılır.
        foreach (var violation in violations)
        {
            var ended = result.Events.First(e =>
                e.Sequence > violation.Sequence && e.Type == MatchEventType.PossessionEnded);

            Assert.Equal(violation.PossessionId, ended.PayloadAs<PossessionEndedPayload>().EndedPossessionId);
        }

        Assert.DoesNotContain(result.Events, e => e.Type == MatchEventType.ShotAttempt);
    }

    [Fact]
    public void EngineTerminatesForEveryTestedSeed()
    {
        for (var seed = 0UL; seed < 60UL; seed++)
        {
            var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(seed));

            Assert.True(
                result.Status is MatchStatus.Completed or MatchStatus.Aborted,
                $"Seed {seed} sonlanmadı.");
            Assert.NotEmpty(result.Events);
        }
    }

    [Fact]
    public void AdvanceOnATerminalStateIsANoOp()
    {
        var engine = new MatchSimulation(M2TestData.Config());
        var terminal = engine.Simulate(M2TestData.NeutralMirror());

        var state = engine.Create(M2TestData.NeutralMirror());

        while (!state.IsTerminal)
        {
            state = engine.Advance(state).State;
        }

        var step = engine.Advance(state);

        Assert.True(step.IsTerminal);
        Assert.Empty(step.Events);
        Assert.Equal(state, step.State);
        Assert.Equal(terminal.HomeScore, state.HomeScore);
    }

    [Fact]
    public void PossessionCountGrowsWithTheMatch()
    {
        var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror());

        Assert.True(result.HomePossessions > 50, $"Pozisyon sayısı beklenmedik: {result.HomePossessions}");
        Assert.Equal(
            result.HomePossessions + result.AwayPossessions,
            result.Events.Count(e => e.Type == MatchEventType.PossessionStarted));
    }
}
