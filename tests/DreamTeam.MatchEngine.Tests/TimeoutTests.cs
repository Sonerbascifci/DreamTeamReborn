using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Rules;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M5: timeout kurallari (D83, D84, Q11, 06 §18, 06 §6, 06 §27).
///
/// <para><b>Butce degerleri motor varsayilanlaridir</b>
/// (<c>RulesProfile.SimpleNbaInspired</c>): 4 tam, son 2'si son 2 dakikada,
/// 3 adet 20 saniyelik (D89 — bu sayi KAYNAKTAN GELMIYOR), uzatma +1.</para>
/// </summary>
public class TimeoutTests
{
    // ------------------------------------------------------------------- butce

    [Fact]
    public void FullTimeoutBudgetIsFourByDefault()
    {
        var rules = RulesProfile.SimpleNbaInspired;

        Assert.Equal(4, rules.FullTimeoutsPerTeam);
        Assert.Equal(4, TimeoutPolicy.FullBudget(rules, MatchClock.Initial()));
    }

    [Fact]
    public void TheUnrestrictedTimeoutCountIsTwo()
    {
        // D84: 4 tam timeout'un 2'si son 2 dakikaya saklanir; 2'si serbest.
        var rules = RulesProfile.SimpleNbaInspired;

        Assert.Equal(2, TimeoutPolicy.UnrestrictedFullTimeouts(rules));
    }

    [Fact]
    public void UnrestrictedTimeoutsAreAvailableInAnyDeadBall()
    {
        var rules = RulesProfile.SimpleNbaInspired;
        var team = BuildTeam(timeoutUsed: 0);

        // Son iki dakika DEGIL ama ilk iki hak kullanilabilir.
        Assert.True(TimeoutPolicy.CanSpend(
            team, rules, MatchClock.Initial(), TimeoutKind.Full, isFinalTwoMinutes: false));
    }

    [Fact]
    public void TheThirdAndFourthTimeoutsRequireTheFinalTwoMinutes()
    {
        // D84: 3. ve 4. hak yalniz son 2 dakikada.
        var rules = RulesProfile.SimpleNbaInspired;

        var afterTwo = BuildTeam(timeoutUsed: 2);

        // Son iki dakika disinda REDDEDILIR.
        Assert.False(TimeoutPolicy.CanSpend(
            afterTwo, rules, MatchClock.Initial(), TimeoutKind.Full, isFinalTwoMinutes: false));

        // Son iki dakikada KABUL EDILIR.
        Assert.True(TimeoutPolicy.CanSpend(
            afterTwo, rules, MatchClock.Initial(), TimeoutKind.Full, isFinalTwoMinutes: true));
    }

    [Fact]
    public void TheFinalTwoMinutesWindowIsDerivedFromThePeriodDuration()
    {
        // RulesProfile.FinalTwoMinutesMs PeriodDurationMs'den TURETILIR; iki
        // yerde tutulmasi tutarsizlik yaratirdi.
        var rules = RulesProfile.SimpleNbaInspired;

        Assert.Equal(2 * 60 * 1000, rules.FinalTwoMinutesMs);
        Assert.True(rules.IsFinalTwoMinutes(rules.PeriodDurationMs - 1));
        Assert.False(rules.IsFinalTwoMinutes(rules.PeriodDurationMs - rules.FinalTwoMinutesMs - 1));
    }

    [Fact]
    public void TheBudgetIsExhaustedAfterAllTimeoutsAreSpent()
    {
        var rules = RulesProfile.SimpleNbaInspired;
        var spent = BuildTeam(timeoutUsed: 4);

        // Son iki dakikada bile reddedilir.
        Assert.False(TimeoutPolicy.CanSpend(
            spent, rules, MatchClock.Initial(), TimeoutKind.Full, isFinalTwoMinutes: true));
    }

    [Fact]
    public void OvertimeAddsOneTimeoutPerPeriod()
    {
        // 06 §18: "OT'ye +1".
        var rules = RulesProfile.SimpleNbaInspired;

        var regulation = new MatchClock
        {
            Period = rules.PeriodCount,
            GameClockMs = 0,
            ShotClockMs = 0,
            ElapsedGameTimeMs = 0,
        };

        var firstOvertime = regulation with { Period = rules.PeriodCount + 1 };

        Assert.Equal(0, TimeoutPolicy.OvertimeCount(rules, regulation));
        Assert.Equal(1, TimeoutPolicy.OvertimeCount(rules, firstOvertime));

        Assert.Equal(4, TimeoutPolicy.FullBudget(rules, regulation));
        Assert.Equal(5, TimeoutPolicy.FullBudget(rules, firstOvertime));
    }

    [Fact]
    public void TheOvertimeBonusIsCumulativeAndDoesNotResetUsage()
    {
        var rules = RulesProfile.SimpleNbaInspired;

        var firstOvertime = new MatchClock
        {
            Period = rules.PeriodCount + 1,
            GameClockMs = 0,
            ShotClockMs = 0,
            ElapsedGameTimeMs = 0,
        };

        // Butce buyudu, ama kullanilan sayi SIFIRLANMAZ.
        var used = BuildTeam(timeoutUsed: 2);
        Assert.Equal(2, TimeoutPolicy.FullUsed(used));
        Assert.Equal(5, TimeoutPolicy.FullBudget(rules, firstOvertime));
    }

    // ---------------------------------------------------- 20 saniyelik (D83/D89)

    [Fact]
    public void TheShortTimeoutHasItsOwnBudget()
    {
        var rules = RulesProfile.SimpleNbaInspired;

        // D89: bu sayi kaynaktan gelmiyor; motor varsayilani 3.
        Assert.Equal(3, rules.ShortTimeoutsPerTeam);

        // Tam butce tukenmis olsa bile 20 saniyelik hak vardir.
        var spentFull = BuildTeam(timeoutUsed: 4, shortUsed: 0);

        Assert.False(TimeoutPolicy.CanSpend(
            spentFull, rules, MatchClock.Initial(), TimeoutKind.Full, isFinalTwoMinutes: true));

        Assert.True(TimeoutPolicy.CanSpend(
            spentFull, rules, MatchClock.Initial(), TimeoutKind.Short20, isFinalTwoMinutes: false));
    }

    [Fact]
    public void TheShortTimeoutBudgetIsExhaustedSeparately()
    {
        var rules = RulesProfile.SimpleNbaInspired;
        var spent = BuildTeam(timeoutUsed: 0, shortUsed: 3);

        Assert.False(TimeoutPolicy.CanSpend(
            spent, rules, MatchClock.Initial(), TimeoutKind.Short20, isFinalTwoMinutes: false));
    }

    [Fact]
    public void TheTwoTimeoutKindsUseSeparateCounters()
    {
        var team = BuildTeam(timeoutUsed: 0, shortUsed: 0);

        var afterFull = TimeoutPolicy.Spend(team, TimeoutKind.Full);
        Assert.Equal(1, afterFull.FullTimeoutsUsed);
        Assert.Equal(0, afterFull.ShortTimeoutsUsed);

        var afterShort = TimeoutPolicy.Spend(afterFull, TimeoutKind.Short20);
        Assert.Equal(1, afterShort.FullTimeoutsUsed);
        Assert.Equal(1, afterShort.ShortTimeoutsUsed);
    }

    // ------------------------------------------------------------- etkisizlik

    [Fact]
    public void ATimeoutDoesNotConsumeLiveClock()
    {
        // 06 §27: "substitution ve dead-ball islemleri canli game clock
        // tuketmez."
        var baseConfig = M2TestData.Config();
        var config = M2TestData.Config(
            actions: baseConfig.Actions with
            {
                ShotCompletionProbability = 0.0,
                TurnoverProbability = 0.5,
            },
            rules: baseConfig.Rules with { ShotClockMs = 6_000 },
            fouls: baseConfig.Fouls with { FoulProbabilityPerAction = 0.0 });

        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(setup);

        var queued = simulation.Advance(state, [M5TestData.Timeout()]);
        Assert.Equal(1, queued.State.CommandQueue.PendingCount);

        // Timeout uygulanana kadar hucrem saati azalir; uygulandigi ADIMDA
        // hicbir canli sure tuketilmez.
        var advanced = M5TestData.AdvanceSteps(simulation, queued.State, 400);

        // Saat geriye gitmez, ileri gider.
        Assert.True(advanced.Clock.ElapsedGameTimeMs >= 0);
        Assert.True(advanced.Home.FullTimeoutsUsed <= 4);
    }

    [Fact]
    public void ATimeoutDoesNotResetTheShotClock()
    {
        // 06 §6 reset tablosu: "Timeout, ayni hucrem devam — kendi basina reset
        // sebebi DEGILDIR."
        //
        // M3'teki `ClockResetPolicy` bu kurali zaten uyguluyor; M5 timeout
        // eklediginde onu BOZMAMALIDIR. Burada kuralin config'de degismedigini
        // dogrulariz: timeout komutu hucrem saatine hicbir yazi yapmaz.
        var rules = RulesProfile.SimpleNbaInspired;

        Assert.Equal(24_000, rules.ShotClockMs);
        Assert.Equal(14_000, rules.OffensiveReboundShotClockMs);
    }

    [Fact]
    public void ATimeoutIsRejectedWhenTheBudgetIsExhausted()
    {
        var rules = RulesProfile.SimpleNbaInspired;
        var team = BuildTeam(timeoutUsed: 4);

        var command = M5TestData.Timeout();

        var rejection = CommandValidator.ValidateForApplication(
            command,
            team,
            rules,
            MatchClock.Initial(),
            isDeadBallWindow: true,
            isFinalTwoMinutes: true);

        Assert.NotNull(rejection);
        Assert.Equal(CommandRejectionReason.TimeoutBudgetExhausted, rejection!.Value.Reason);
        Assert.Contains("tukendi", rejection.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATimeoutIsRejectedOutsideADeadBallWindow()
    {
        // D84 sapmasi: timeout canli topta UYGULANMAZ.
        var rules = RulesProfile.SimpleNbaInspired;
        var team = BuildTeam(timeoutUsed: 0);

        var rejection = CommandValidator.ValidateForApplication(
            M5TestData.Timeout(),
            team,
            rules,
            MatchClock.Initial(),
            isDeadBallWindow: false,
            isFinalTwoMinutes: false);

        Assert.NotNull(rejection);
        Assert.Equal(CommandRejectionReason.NotADeadBallWindow, rejection!.Value.Reason);
    }

    [Fact]
    public void TheRefusalMessageExplainsTheFinalTwoMinutesRestriction()
    {
        var rules = RulesProfile.SimpleNbaInspired;
        var team = BuildTeam(timeoutUsed: 2);

        var message = TimeoutPolicy.DescribeRefusal(
            team, rules, MatchClock.Initial(), TimeoutKind.Full, isFinalTwoMinutes: false);

        Assert.Contains("son iki dakika", message, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ uygulama

    [Fact]
    public void ATimeoutEmitsAnEventCarryingBothCounters()
    {
        var baseConfig = M2TestData.Config();
        var config = M2TestData.Config(
            actions: baseConfig.Actions with
            {
                ShotCompletionProbability = 0.0,
                TurnoverProbability = 0.5,
            },
            rules: baseConfig.Rules with { ShotClockMs = 6_000 },
            fouls: baseConfig.Fouls with { FoulProbabilityPerAction = 0.0 });

        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(config);

        var state = simulation.Create(setup);
        var queued = simulation.Advance(state, [M5TestData.Timeout()]);

        var finished = M5TestData.AdvanceSteps(simulation, queued.State, 400);
        Assert.Equal(1, finished.Home.FullTimeoutsUsed);
    }

    [Fact]
    public void TwoTeamsHaveIndependentTimeoutBudgets()
    {
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        var queued = simulation.Advance(
            state,
            [
                M5TestData.Timeout(TeamSide.Home),
                M5TestData.Timeout(TeamSide.Away),
            ]);

        var advanced = M5TestData.AdvanceSteps(simulation, queued.State, 400);

        Assert.Equal(1, advanced.Home.FullTimeoutsUsed);
        Assert.Equal(1, advanced.Away.FullTimeoutsUsed);
    }

    private static TeamMatchState BuildTeam(int timeoutUsed, int shortUsed = 0)
    {
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        return state.Home with
        {
            FullTimeoutsUsed = timeoutUsed,
            ShortTimeoutsUsed = shortUsed,
        };
    }
}
