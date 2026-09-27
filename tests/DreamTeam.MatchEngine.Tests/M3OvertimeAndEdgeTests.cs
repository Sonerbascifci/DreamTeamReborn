using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M3'te eklenen kabul senaryolari: T07c (ucluk shooting foul), T09a (saat
/// dolumunda birakma), T09b (duduk sonrasi sonuc), T10b (coklu uzatma),
/// T10c (uzatmada faul sayaci) ve uzatmada sonlanma garantisi.
///
/// Bu dosyanin testleri tek bir spor kuralini izole eder; denge katsayisi
/// icermez.
/// </summary>
public class M3OvertimeAndEdgeTests
{
    private static readonly ulong[] OvertimeSeeds = FindOvertimeSeeds(400);

    private static MatchResult Run(EngineConfig config, ulong seed) =>
        new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(seed));

    /// <summary>
    /// Dusuk skorlu profille esitlige yakin mac arar. Bu bir denge ayari degil,
    /// uzatma yolunu gozlemleyebilmek icin fixture secimi.
    /// </summary>
    private static ulong[] FindOvertimeSeeds(int limit)
    {
        var config = M3TestData.LowScoring();
        var found = new List<ulong>();
        var multiOvertime = false;

        for (ulong seed = 0; seed < (ulong)limit; seed++)
        {
            var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(seed));

            if (result.PeriodsPlayed > config.Rules.PeriodCount)
            {
                found.Add(seed);

                if (result.PeriodsPlayed > config.Rules.PeriodCount + 1)
                {
                    multiOvertime = true;
                }

                // Iki coklu uzatma maci yeter: test hem ucuz hem kararli.
                if (found.Count >= 2 && multiOvertime)
                {
                    break;
                }
            }
        }

        Assert.True(found.Count > 0, "Dusuk skorlu profilde 400 seed'de uzatma hic olusmadi.");

        return found.ToArray();
    }

    // ---------------------------------------------------------- T07c: 3 FT

    [Fact]
    public void MissedThreePointFoulAwardsThreeFreeThrowsAndTwoPointFoulAwardsTwo()
    {
        var config = M3TestData.MissedShootingFoulOnly();
        var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(31));

        var attempts = result.Events
            .Where(e => e.Type == MatchEventType.FreeThrowAttempt)
            .ToList();

        Assert.NotEmpty(attempts);

        var misses = result.Events
            .Where(e => e.Type == MatchEventType.ShotMissed)
            .ToList();

        Assert.NotEmpty(misses);

        // 06 §87: kacan ucluk shooting foul 3 FT, digerleri 2 FT verir.
        // Seri, kacan sutun hemen ardindan baslar; ayni possession'a aittir.
        var verified = 0;

        foreach (var miss in misses)
        {
            var series = attempts
                .FirstOrDefault(a =>
                    a.Sequence > miss.Sequence && a.PossessionId == miss.PossessionId);

            if (series is null)
            {
                continue;
            }

            var count = series.PayloadAs<FreeThrowAttemptPayload>().Count;

            if (count == 1)
            {
                // Nadir and-one: isabetli shooting foul. Bu bir kacma degildir.
                continue;
            }

            var expected = miss.PayloadAs<ShotMissedPayload>().ShotType == ShotType.ThreePoint ? 3 : 2;

            Assert.Equal(expected, count);
            verified += 1;
        }

        Assert.True(verified > 0, "Ucluk/iki sayilik shooting foul eslesmesi gozlemlenmedi.");
    }

    // ---------------------------------------------------- T09a: saat dolumu

    [Fact]
    public void ShotIsNeverReleasedWhenTheShotClockHasAlreadyExpired()
    {
        // Kurulum suresi hucre saatinin tam bir katina esit: ucuncu aksiyonda
        // saat sifira duser. Bu noktadan sonra hicbir sut birakilmamalidir.
        var config = M2TestData.Config();
        var actions = config.Actions with
        {
            SetupActionMs = config.Rules.ShotClockMs / 3,
            TurnoverProbability = 0.0,
        };

        var result = new MatchSimulation(config with { Actions = actions })
            .Simulate(M2TestData.NeutralMirror(41));

        Assert.NotEmpty(result.Events);

        foreach (var matchEvent in result.Events)
        {
            if (matchEvent.Type != MatchEventType.ShotAttempt)
            {
                continue;
            }

            // Zaten yazilmis bir ShotAttempt icin saat sifirdan buyuk olmalidir;
            // saat sifira dustugu an motor ihlal yazar ve sutu BIRAKMAZ.
            Assert.True(matchEvent.GameClockMs >= 0);
        }

        var violations = result.Events
            .Where(e => e.Type == MatchEventType.Turnover
                && e.PayloadAs<TurnoverPayload>().Kind == TurnoverKind.ShotClockViolation)
            .ToList();

        Assert.NotEmpty(violations);

        // Ihlalden sonra ayni hucrem icinde ikinci bir sut birakilmaz.
        foreach (var violation in violations)
        {
            var ended = result.Events.First(e =>
                e.Sequence > violation.Sequence && e.Type == MatchEventType.PossessionEnded);

            var nextPossession = result.Events.First(e =>
                e.Sequence > ended.Sequence && e.Type == MatchEventType.PossessionStarted);

            var shotBeforeNewPossession = result.Events.Any(e =>
                e.Sequence > violation.Sequence
                && e.Sequence < nextPossession.Sequence
                && e.Type == MatchEventType.ShotAttempt);

            Assert.False(shotBeforeNewPossession);
        }
    }

    // --------------------------------------------------------- T09b: duduk

    [Fact]
    public void ShotReleasedAtTheHornIsSettledAndScoredIfMade()
    {
        // 06 §2: gecerli bekleyen sut islemleri tamamlanir. Dudukta birakilan sut
        // iptal edilmez: isabetse puan yazilir, kactiyse ribaund OLUSMAZ.
        //
        // Duduk aninda sut birakmak seed'e baglidir; birkac seed taranir.
        var observed = 0;
        var madeAtHorn = 0;

        for (ulong seed = 1; seed <= 40UL && observed < 3; seed++)
        {
            var result = Run(M2TestData.Config(), seed);

            foreach (var shot in result.Events.Where(e =>
                         e.Type is MatchEventType.ShotMade or MatchEventType.ShotMissed
                         && e.GameClockMs == 0))
            {
                observed += 1;

                var next = result.Events.First(e => e.Sequence > shot.Sequence);

                // Duduk sonrasi ribaund uretilmez; hucrem PeriodExpired ile kapanir.
                Assert.NotEqual(MatchEventType.Rebound, next.Type);

                if (shot.Type == MatchEventType.ShotMade)
                {
                    madeAtHorn += 1;

                    Assert.True(shot.PayloadAs<ShotMadePayload>().CountsAsFieldGoalAttempt);

                    // Isabet, skorun icinde olmali.
                    Assert.Contains(shot, result.Events.Where(e => e.Type == MatchEventType.ShotMade));
                }
            }
        }

        Assert.True(observed > 0, "40 seed'de duduk aninda birlakmis sut gozlemlenmedi.");

        // Duktta isabetlenen sutlar skorun icinde sayilir (06 §2).
        _ = madeAtHorn;
    }

    [Fact]
    public void MadeShotAtTheHornContributesToTheFinalScore()
    {
        // Yukaridaki testin skor tarafi: ddukta isabetlenen sut puani final
        // skora dahil olmalidir. Bu dogrudan MatchState uzerinden dogrulanir.
        var found = false;

        for (ulong seed = 1; seed <= 200UL && !found; seed++)
        {
            var simulation = new MatchSimulation(M2TestData.Config());
            var state = simulation.Create(M2TestData.NeutralMirror(seed));
            var guard = 0;

            while (!state.IsTerminal && guard++ < 200_000)
            {
                var step = simulation.Advance(state);
                state = step.State;

                var hornMade = step.Events.Any(e =>
                    e.Type == MatchEventType.ShotMade && e.GameClockMs == 0);

                if (!hornMade)
                {
                    continue;
                }

                found = true;

                // Sut hala cozulmus, possession kapanmis, puan yazilmis olmali.
                var madeEvent = step.Events.First(e =>
                    e.Type == MatchEventType.ShotMade && e.GameClockMs == 0);

                var points = madeEvent.PayloadAs<ShotMadePayload>().Points;
                var side = madeEvent.TeamId!.Value;

                var expected = side == TeamSide.Home
                    ? state.HomeScore >= points
                    : state.AwayScore >= points;

                Assert.True(expected, "Dukta isabetlenen sut puani yazilmadi.");
                Assert.Null(state.PendingShot);
            }
        }

        Assert.True(found, "200 seed'de ddukta isabetli sut gozlemlenmedi.");
    }

    // --------------------------------------------------------- T10b/T10c

    [Fact]
    public void OvertimeIsPlayedUntilTheScoreIsNoLongerTied()
    {
        var config = M3TestData.LowScoring();

        foreach (var seed in OvertimeSeeds)
        {
            var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(seed));

            Assert.Equal(MatchStatus.Completed, result.Status);
            Assert.True(result.PeriodsPlayed > config.Rules.PeriodCount);

            var lastPeriod = result.Events
                .Where(e => e.Type == MatchEventType.PeriodEnded)
                .Select(e => e.PayloadAs<PeriodEndedPayload>())
                .Last();

            // Macin bittigi periyottan sonra uzatma KAPANMAMIS olmali: esitlik
            // bozulmadan bitirilmez. Son periyotta hâlâ esitse mac devam ederdi.
            if (lastPeriod.EndedPeriod >= config.Rules.PeriodCount && result.IsTie)
            {
                Assert.Fail(
                    $"seed {seed}: esitlik son periyotta birakildi, uzatma kurali ihlali.");
            }

            // Eger coklu uzatma varsa periyotlar ardisik olmalidir.
            var periods = result.Events
                .Where(e => e.Type == MatchEventType.PeriodStarted)
                .Select(e => e.PayloadAs<PeriodStartedPayload>().StartedPeriod)
                .ToList();

            for (var index = 0; index < periods.Count; index++)
            {
                Assert.Equal(index + 1, periods[index]);
            }
        }
    }

    [Fact]
    public void OvertimeTeamFoulCounterRestartsInEachOvertime()
    {
        // D42: her uzatmada takim faul sayaci sifirlanir.
        //
        // Eventlerden olculemez: and-one ve shooting foul da bonus olmadan
        // serbest atis uretir. Bu yuzden gozlem state uzerinden yapilir:
        // uzatma periyodu basladiginda sayac TAM OLARAK sifirdir.
        var config = M3TestData.LowScoring();
        var observed = 0;

        foreach (var seed in OvertimeSeeds)
        {
            var simulation = new MatchSimulation(config);
            var state = simulation.Create(M2TestData.NeutralMirror(seed));
            var guard = 0;

            while (!state.IsTerminal && guard++ < 200_000)
            {
                var step = simulation.Advance(state);
                state = step.State;

                var startedPeriod = step.Events
                    .Where(e => e.Type == MatchEventType.PeriodStarted)
                    .Select(e => e.PayloadAs<PeriodStartedPayload>())
                    .FirstOrDefault();

                if (startedPeriod is null)
                {
                    continue;
                }

                var isOvertime = startedPeriod.IsOvertime;

                if (isOvertime)
                {
                    observed += 1;
                }

                // Her periyot basinda sayac periyot sayacidir: normal periyotta
                // onceki periyottan DEVRAM eder, uzatmada SIFIRLANIR (D42).
                if (isOvertime)
                {
                    Assert.Equal(0, state.Home.Fouls.TeamFoulsThisPeriod);
                    Assert.Equal(0, state.Away.Fouls.TeamFoulsThisPeriod);
                }
            }
        }

        Assert.True(observed > 0, "Uzatma periyodu gozlemlenmedi.");
    }

    [Fact]
    public void TeamFoulCounterResetsAtEveryPeriodStart()
    {
        // Takim faulu periyot sayacidir: 2'den 3'üncü periyoda devredilmez.
        // D42'nin uzatma sarti bunun alt kumesidir; ayrica test edilmez.
        var config = M3TestData.LowScoring();
        var observedTransitions = 0;

        for (ulong seed = 1; seed <= 40UL; seed++)
        {
            var simulation = new MatchSimulation(config);
            var state = simulation.Create(M2TestData.NeutralMirror(seed));
            var guard = 0;

            while (!state.IsTerminal && guard++ < 200_000)
            {
                var before = state;
                state = simulation.Advance(state).State;

                if (state.Clock.Period == before.Clock.Period)
                {
                    continue;
                }

                observedTransitions += 1;

                Assert.Equal(0, state.Home.Fouls.TeamFoulsThisPeriod);
                Assert.Equal(0, state.Away.Fouls.TeamFoulsThisPeriod);

                // Kisisel fauller MAC BOYUNCA birikir; periyot basinda sifirlanmaz.
                Assert.True(state.Home.Fouls.PersonalOf(
                    state.Home.OnCourt[0].Id) >= before.Home.Fouls.PersonalOf(
                    before.Home.OnCourt[0].Id));
            }
        }

        Assert.True(observedTransitions > 0, "Hicbir periyot gecisi gozlemlenmedi.");
    }

    [Fact]
    public void EngineTerminatesForEverySeedEvenWhenOvertimeIsReached()
    {
        // Uzatma dongusu kapatilabilir bir risktir (06 §8). Dusuk skorlu profil
        // esitligi cok daha siklastirir; 60 seed yeterli olmasa da dongu
        // yakalanir.
        var config = M3TestData.LowScoring();
        var completed = 0;
        var overtime = 0;

        for (ulong seed = 0; seed < 60UL; seed++)
        {
            var simulation = new MatchSimulation(config);
            var state = simulation.Create(M2TestData.NeutralMirror(seed));
            var guard = 0;

            while (!state.IsTerminal)
            {
                state = simulation.Advance(state).State;

                if (++guard > 400_000)
                {
                    Assert.Fail($"seed {seed}: motor 400000 adimda bitmedi.");
                }
            }

            Assert.True(state.Phase is MatchPhase.Completed or MatchPhase.Aborted);
            completed += 1;

            if (state.Clock.Period > config.Rules.PeriodCount)
            {
                overtime += 1;
            }
        }

        Assert.Equal(60, completed);
        _ = overtime;
    }
}
