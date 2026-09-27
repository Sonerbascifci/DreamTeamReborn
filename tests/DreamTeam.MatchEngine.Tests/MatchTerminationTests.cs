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
    public void TieIsResolvedByOvertimeNotByARandomWinner()
    {
        // 06 §8: esit maca rastgele kazanan secilmez. M3'te uzatma vardir: mac
        // 4 periyottan sonra esitse 5. periyot acilir.
        var overtimeMatches = 0;
        var tiedAfterFour = 0;

        // M4: dusuk skorlu profil kullanilir. Varsayilan config'de savunma ve
        // kalite kanallari acilinca esitlik araliklari daraldi ve 120 seed'de
        // uzatma gozlemlenemiyordu. Bu bir DENGE ayari degil, uzatma yolunu
        // gozlemleyebilmek icin fixture secimi.
        var config = M3TestData.LowScoring();

        for (var seed = 0UL; seed < 120UL; seed++)
        {
            var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(seed));

            if (result.Status != MatchStatus.Completed)
            {
                continue;
            }

            var payload = result.Events.Single(e => e.Type == MatchEventType.MatchEnded)
                .PayloadAs<MatchEndedPayload>();

            Assert.Equal(result.HomeScore == result.AwayScore, payload.IsTie);

            if (result.PeriodsPlayed > config.Rules.PeriodCount)
            {
                overtimeMatches += 1;
            }
            else if (result.IsTie)
            {
                tiedAfterFour += 1;
            }
        }

        Assert.True(overtimeMatches > 0, "Hicbir mac uzatmaya gitmedi; uzatma yolu test edilemedi.");
        Assert.True(tiedAfterFour == 0, "Esitlik 4 periyotta birakildi; uzatma kurali devreye girmeli.");
    }

    [Fact]
    public void OvertimePeriodsAreShorterAndFlagged()
    {
        var config = M3TestData.LowScoring();
        var rules = config.Rules;
        var found = false;

        for (var seed = 0UL; seed < 120UL && !found; seed++)
        {
            var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(seed));

            foreach (var period in result.Events
                         .Where(e => e.Type == MatchEventType.PeriodStarted)
                         .Select(e => e.PayloadAs<PeriodStartedPayload>()))
            {
                if (period.StartedPeriod <= rules.PeriodCount)
                {
                    Assert.Equal(rules.PeriodDurationMs, period.PeriodDurationMs);
                    Assert.False(period.IsOvertime);
                }
                else
                {
                    Assert.Equal(rules.OvertimeDurationMs, period.PeriodDurationMs);
                    Assert.True(period.IsOvertime);
                    found = true;
                }
            }
        }

        Assert.True(found, "Uzatma periyodu hic gorulmedi.");
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
        var baseSetup = M2TestData.NeutralMirror();
        var invalid = baseSetup with
        {
            Home = baseSetup.Home with
            {
                Lineup = new Domain.Teams.Lineup
                {
                    PlayerIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                },
            },
        };

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
        //
        // M4 notu: hucrem 3 aksiyonda bittigi icin hucrem basina yalnizca birkac
        // deneme olur. Testin anlamli kalabilmesi icin kucuk bir sut olasiligi
        // birakilir; yoksa skor hep 0-0 kalir ve mac beraberlikle uzatmaya girer
        // (bkz. DegenerateZeroScoreMatchIsAbortedByTheGuard).
        var baseConfig = M2TestData.Config();
        var config = M2TestData.Config(
            actions: baseConfig.Actions with { ShotCompletionProbability = 0.05 },
            fouls: baseConfig.Fouls with { FoulProbabilityPerAction = 0.05 });

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

            // Ihlalden sonra AYNI hucremde sut birakilmez. Saat sifirdayken
            // birakma gecersizdir (D42: releaseTime < expiryTime, esitlikte ihlal).
            var samePossessionShot = result.Events.Any(e =>
                e.Type == MatchEventType.ShotAttempt
                && e.PossessionId == violation.PossessionId
                && e.Sequence > violation.Sequence
                && e.Sequence < ended.Sequence);

            Assert.False(samePossessionShot);
        }
    }

    /// <summary>
    /// T10d: eşitlikte kazanan **uydurulmaz** ve sonsuz döngü oluşmaz. 08 §T10
    /// "guard → Aborted" der; motor da öyle yapar.
    ///
    /// <para>Bu test, hiç puan atılabilen bir fixture ile <b>gerçekten</b> 0-0
    /// beraberliğe düşülen yolu ölçer. Skor hep eşit olduğu için her periyot
    /// sonunda uzatma açılır; sonsuz döngüyü yalnız eylem guard'ı keser. Aborted
    /// sonuçta <c>IsTie</c> <b>false</b>'tır — 06 §8 gereği yarım kalan maç beraberlik
    /// sayılmaz ve skor geçersizdir.</para>
    /// </summary>
    [Fact]
    public void DegenerateZeroScoreMatchIsAbortedByTheGuardWithoutFalsifyingTheScore()
    {
        var baseConfig = M2TestData.Config();
        var config = M2TestData.Config(
            actions: baseConfig.Actions with { ShotCompletionProbability = 0.0 },
            fouls: baseConfig.Fouls with { FoulProbabilityPerAction = 0.0 });

        var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror());

        Assert.Equal(MatchStatus.Aborted, result.Status);
        Assert.NotNull(result.AbortReason);

        // Uydurma galibiyet yok: yarım kalan maç beraberlik değildir.
        Assert.False(result.IsTie);
        Assert.Equal(0, result.HomeScore);
        Assert.Equal(0, result.AwayScore);

        // Uzatma gerçekten açıldı (periyot sayısı normalin üstünde) ve guard
        // devreye girdi.
        Assert.True(result.PeriodsPlayed > config.Rules.PeriodCount);
        Assert.Contains("guard", result.AbortReason!, StringComparison.OrdinalIgnoreCase);

        // Maç sonu MatchEnded üretilmedi: yalnız MatchAborted.
        Assert.DoesNotContain(result.Events, e => e.Type == MatchEventType.MatchEnded);
        Assert.Contains(result.Events, e => e.Type == MatchEventType.MatchAborted);
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
