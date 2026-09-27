using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>Saat modeli testleri. H02 ve 06 §2.</summary>
public class ClockTests
{
    private static MatchClock Clock(
        int period = 1,
        long gameClockMs = 720_000,
        long shotClockMs = 24_000,
        long elapsedMs = 0) => new()
    {
        Period = period,
        GameClockMs = gameClockMs,
        ShotClockMs = shotClockMs,
        ElapsedGameTimeMs = elapsedMs,
    };

    [Fact]
    public void ConsumingLiveTimeAdvancesAllThreeClocksByTheSameAmount()
    {
        var result = Clock(elapsedMs: 100_000).ConsumeLiveTime(8_000);

        Assert.Equal(712_000, result.GameClockMs);
        Assert.Equal(16_000, result.ShotClockMs);
        Assert.Equal(108_000, result.ElapsedGameTimeMs);
    }

    [Fact]
    public void ConsumingMoreThanTheGameClockCountsOnlyWhatWasActuallyPlayed()
    {
        // 08 §82 "Extremes": sayaç negatif olmaz, oynanmayan süre sayılmaz.
        var result = Clock(gameClockMs: 3_000).ConsumeLiveTime(8_000);

        Assert.Equal(0, result.GameClockMs);
        Assert.Equal(21_000, result.ShotClockMs);
        Assert.Equal(3_000, result.ElapsedGameTimeMs);
        Assert.True(result.IsGameTimeExhausted);
    }

    [Fact]
    public void ShotClockNeverGoesNegative()
    {
        var result = Clock(shotClockMs: 500).ConsumeLiveTime(8_000);

        Assert.Equal(0, result.ShotClockMs);
        Assert.True(result.IsShotClockExhausted);
    }

    [Fact]
    public void ConsumingZeroTimeIsANoOp()
    {
        var clock = Clock(elapsedMs: 50_000);
        var result = clock.ConsumeLiveTime(0);

        Assert.Equal(clock, result);
    }

    [Fact]
    public void ConsumingNegativeTimeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Clock().ConsumeLiveTime(-1));
    }

    [Fact]
    public void BeginPeriodResetsBothClocksButPreservesTotalElapsed()
    {
        // Periyot başı toplam oynanan süreyi sıfırlamaz; sıfırlarsa raporlanan
        // süre yalnız son periyodu gösterir.
        var result = Clock(gameClockMs: 0, shotClockMs: 0, elapsedMs: 2_880_000)
            .BeginPeriod(period: 2, periodDurationMs: 720_000, shotClockMs: 24_000);

        Assert.Equal(2, result.Period);
        Assert.Equal(720_000, result.GameClockMs);
        Assert.Equal(24_000, result.ShotClockMs);
        Assert.Equal(2_880_000, result.ElapsedGameTimeMs);
    }

    [Fact]
    public void InitialClockHasNoTime()
    {
        var clock = MatchClock.Initial();

        Assert.Equal(0, clock.Period);
        Assert.Equal(0, clock.GameClockMs);
        Assert.Equal(0, clock.ShotClockMs);
        Assert.Equal(0, clock.ElapsedGameTimeMs);
    }

    [Fact]
    public void EngineGameClockIsMonotonicAndEndsAtZero()
    {
        var setup = M2TestData.NeutralMirror(seed: 99);
        var engine = new MatchSimulation(M2TestData.Config());
        var state = engine.Create(setup);

        var previousElapsed = -1L;
        var guard = 0;

        while (!state.IsTerminal && guard++ < 100_000)
        {
            Assert.True(
                state.Clock.ElapsedGameTimeMs >= previousElapsed,
                "Oynanan süre geriye gidemez.");

            previousElapsed = state.Clock.ElapsedGameTimeMs;
            state = engine.Advance(state).State;
        }

        Assert.True(state.IsTerminal, "Maç sonlanmadı.");
        Assert.Equal(0, state.Clock.GameClockMs);
        Assert.Equal(
            (long)M2TestData.Config().Rules.PeriodCount * M2TestData.Config().Rules.PeriodDurationMs,
            state.Clock.ElapsedGameTimeMs);
    }
}
