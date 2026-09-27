using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Fatigue;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M4: tempo kanalları (D59) ve enerji/stamina (T12, D58/D63/D64).
///
/// Tempo iki kanaldan geçer: aksiyon süresi ve enerji drain'i. Top kaybı
/// etkilenmez — bu, 05 §131'in "her durumda top kaybı yaratmak zorunda değildir"
/// uyarısının karşılığıdır ve iki ayrı testle sabitlenir.
/// </summary>
public class PaceAndEnergyTests
{
    // ------------------------------------------------------------------ tempo

    [Fact]
    public void PaceChangesPossessionCount()
    {
        var config = M4TestData.NoFatigue();
        var slow = Possessions(config, Pace.Slow, seed: 31_337);
        var normal = Possessions(config, Pace.Normal, seed: 31_337);
        var fast = Possessions(config, Pace.Fast, seed: 31_337);

        Assert.True(fast > normal, $"Fast={fast} Normal={normal}");
        Assert.True(normal > slow, $"Normal={normal} Slow={slow}");
    }

    [Fact]
    public void PaceChangesEnergyDrain()
    {
        // Hızlı tempo: aynı oyuncu süresi içinde daha çok aksiyon yapar ve
        // drain çarpanı da yüksektir.
        var fast = AverageFinalEnergy(M4TestData.ShortPeriods(), Pace.Fast, seed: 31_337);
        var normal = AverageFinalEnergy(M4TestData.ShortPeriods(), Pace.Normal, seed: 31_337);
        var slow = AverageFinalEnergy(M4TestData.ShortPeriods(), Pace.Slow, seed: 31_337);

        Assert.True(fast < normal, $"Fast={fast:F2} Normal={normal:F2}");
        Assert.True(normal < slow, $"Normal={normal:F2} Slow={slow:F2}");
    }

    [Fact]
    public void PaceDoesNotAlterActionOrShotMix()
    {
        // D59: tempo yalniz sure ve enerji uzerinden gorunur. Aksiyon ve sut
        // karisimi tempo degistirilince degismemelidir.
        var config = M4TestData.FlatPace(M4TestData.NoFatigue());
        var slow = ActionCounts(config, Pace.Slow, seed: 5_151);
        var fast = ActionCounts(config, Pace.Fast, seed: 5_151);

        // Mutlak sayilar karsilastirilir: tempo yalniz sureyi degistirdigi icin
        // hucrem adedi degisir ama AKSYON KARSIMI degismez.
        Assert.Equal(
            Count(slow, OffensiveAction.PickAndRoll),
            Count(fast, OffensiveAction.PickAndRoll));
        Assert.Equal(
            Count(slow, OffensiveAction.PostUp),
            Count(fast, OffensiveAction.PostUp));
        Assert.Equal(Count(slow, OffensiveAction.Cut), Count(fast, OffensiveAction.Cut));
    }

    private static int Count(Dictionary<OffensiveAction, int> counts, OffensiveAction action) =>
        counts.TryGetValue(action, out var value) ? value : 0;

    [Fact]
    public void PaceDoesNotAlterTurnoverMechanics()
    {
        // D59'in ikinci yarisi: tempo top kaybi riskini DEGISTIRMEZ. Savunma
        // baski kanali tek basina etkili kalir.
        var policy = new Tactics.DefensivePolicy(
            M2TestData.Config().Defense,
            new Ratings.PlayerRatingCalculator(0.6));

        var calm = policy.TurnoverPressure(
            Ratings.PlayerRatingTables.PerimeterDefense(M2TestData.Ratings(50)));
        var strong = policy.TurnoverPressure(
            Ratings.PlayerRatingTables.PerimeterDefense(M2TestData.Ratings(90)));

        Assert.True(strong > calm);
    }

    [Fact]
    public void EveryPaceTuningIsPresentAndOrdered()
    {
        var model = PaceModel.Baseline;

        foreach (var pace in Enum.GetValues<Pace>())
        {
            var tuning = model.TuningFor(pace);

            Assert.True(tuning.SetupActionMultiplier > 0.0);
            Assert.True(tuning.EnergyDrainMultiplier > 0.0);
        }

        // D59: hizli tempo sureyi kisaltir ve yorgunluk maliyetini artirir.
        Assert.True(model.TuningFor(Pace.Slow).SetupActionMultiplier
            > model.TuningFor(Pace.Normal).SetupActionMultiplier);
        Assert.True(model.TuningFor(Pace.Fast).SetupActionMultiplier
            < model.TuningFor(Pace.Normal).SetupActionMultiplier);
        Assert.True(model.TuningFor(Pace.Fast).EnergyDrainMultiplier
            > model.TuningFor(Pace.Slow).EnergyDrainMultiplier);
    }

    [Fact]
    public void UnknownPaceThrowsInsteadOfSilentlyUsingNormal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PaceModel.Baseline.TuningFor((Pace)99));
    }

    // ------------------------------------------------------------ T12: enerji

    [Fact]
    public void EnergyDrainIsBoundedAndNeverNegative()
    {
        // T12a: enerji her zaman [0,100] araliginda kalir ve hicbir adimda
        // negatif olmaz.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(M2TestData.NeutralMirror(4_242));
        var guard = 0;

        while (!state.IsTerminal && guard++ < 200_000)
        {
            state = simulation.Advance(state).State;

            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                foreach (var playerState in state.Team(side).PlayerStates)
                {
                    Assert.InRange(playerState.Energy, 0.0, FatigueModel.MaxEnergy);
                    Assert.False(double.IsNaN(playerState.Energy));
                    Assert.False(double.IsInfinity(playerState.Energy));
                }
            }
        }

        Assert.True(state.IsTerminal);
    }

    [Fact]
    public void BenchRecoveryNeverExceedsCap()
    {
        // T12b: yedekte toparlanma 100'u gecmez.
        var roster = M2TestData.NeutralMirror(1).Home.Team.Roster;
        var onCourt = OnCourtOf(M2TestData.NeutralMirror(1), TeamSide.Home);

        var states = TeamMatchState.InitialStates(
            M2TestData.NeutralMirror(1).Home.Team.Roster,
            M2TestData.Config().Fatigue);

        var advanced = FatigueCalculator.Advance(
            M2TestData.Config().Fatigue,
            states,
            M2TestData.NeutralMirror(1).Home.Team.Roster,
            onCourt,
            liveMilliseconds: 3_600_000,
            paceEnergyMultiplier: 1.0);

        foreach (var playerState in advanced)
        {
            Assert.InRange(playerState.Energy, 0.0, FatigueModel.MaxEnergy);
        }
    }

    [Fact]
    public void MinuteAccountingSumsCorrectly()
    {
        // T12c: toplam oynanan sure = 5 oyuncu x gecen canli sure. Periyot
        // arasi toparlanma bu sayaci ARTIRMAZ.
        var config = M4TestData.ShortPeriods();
        var result = new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(9_090));

        Assert.Equal(MatchStatus.Completed, result.Status);
        Assert.NotEmpty(result.PlayerEnergy);

        foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
        {
            var totalSeconds = result.PlayerEnergy
                .Where(report => report.Team == side)
                .Sum(report => report.SecondsOnCourt);

            Assert.Equal(
                5.0 * result.ElapsedGameTimeMs / 1000.0,
                totalSeconds,
                6);
        }
    }

    [Fact]
    public void FreeThrowsConsumeNoEnergy()
    {
        // T12d: serbest atis canli oyun suresi tuketmez (06 §2), dolayisiyla
        // ne enerji ne saniye degisir.
        var config = M4TestData.FoulOnly(M4TestData.ShortPeriods());
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(M2TestData.NeutralMirror(1_111));
        var guard = 0;
        var observed = 0;

        while (!state.IsTerminal && guard++ < 200_000)
        {
            var before = state;
            var step = simulation.Advance(state);
            state = step.State;

            if (!step.Events.Any(e => e.Type == MatchEventType.FreeThrowAttempt))
            {
                continue;
            }

            observed += 1;

            Assert.Equal(before.Clock.ElapsedGameTimeMs, state.Clock.ElapsedGameTimeMs);

            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                for (var slot = 0; slot < before.Team(side).PlayerStates.Length; slot++)
                {
                    var beforeState = before.Team(side).PlayerStates[slot];
                    var afterState = state.Team(side).PlayerStates[slot];

                    Assert.Equal(beforeState.Energy, afterState.Energy, 10);
                    Assert.Equal(beforeState.SecondsOnCourt, afterState.SecondsOnCourt, 10);
                }
            }
        }

        Assert.True(observed > 0, "Serbest atis gozlemlenmedi.");
    }

    [Fact]
    public void AllTenPlayersUpdateOnEveryLiveInterval()
    {
        // T12f: enerji guncellemesi yalniz aktif oyuncuya yapilmaz (05 §164).
        // Sahadaki bes drain alir, yedek bes recovery alir.
        //
        // ONEMLI: mac basinda herkes 100'de oldugu icin yedek oyuncularin
        // toparlanmasi KIRPILIR ve hicbir sey degismez. Test bu yuzden hepsi
        // 60'tan baslar; aksi halde "on oyuncu guncelleniyor" onermesi oynanmayan
        // bir durumda yanlis dogrulanirdi.
        var setup = M2TestData.NeutralMirror(2);
        var roster = setup.Home.Team.Roster;
        var onCourt = OnCourtOf(setup, TeamSide.Home);

        var model = M2TestData.Config().Fatigue;
        var states = TeamMatchState.InitialStates(roster, model)
            .Select(state => state with { Energy = 60.0 })
            .ToImmutableArray();

        var after = FatigueCalculator.Advance(model, states, roster, onCourt, 60_000, 1.0);

        var changed = 0;

        for (var index = 0; index < after.Length; index++)
        {
            if (Math.Abs(after[index].Energy - states[index].Energy) > 1e-9)
            {
                changed += 1;
            }
        }

        Assert.Equal(10, changed);

        // Yedekler yukselir, sahadakiler duser.
        foreach (var playerState in after)
        {
            var wasOnCourt = onCourt.Any(player => player.Id == playerState.PlayerId);

            Assert.True(
                wasOnCourt
                    ? playerState.Energy < 60.0
                    : playerState.Energy > 60.0);
        }
    }

    [Fact]
    public void OnCourtPlayersAccumulateSecondsAndBenchPlayersDoNot()
    {
        var setup = M2TestData.NeutralMirror(3);
        var roster = setup.Home.Team.Roster;
        var onCourt = OnCourtOf(setup, TeamSide.Home);

        var model = M2TestData.Config().Fatigue;
        var states = TeamMatchState.InitialStates(roster, model);
        var after = FatigueCalculator.Advance(model, states, roster, onCourt, 30_000, 1.0);

        foreach (var playerState in after)
        {
            if (onCourt.Any(player => player.Id == playerState.PlayerId))
            {
                Assert.Equal(30.0, playerState.SecondsOnCourt, 6);
            }
            else
            {
                Assert.Equal(0.0, playerState.SecondsOnCourt, 6);
            }
        }
    }

    [Fact]
    public void PeriodBreakRecoveryDoesNotCountAsCourtTime()
    {
        var roster = M2TestData.NeutralMirror(4).Home.Team.Roster;
        var model = M2TestData.Config().Fatigue;
        var states = TeamMatchState.InitialStates(roster, model);

        var after = FatigueCalculator.RecoverDuringBreak(model, states, 720_000);

        Assert.All(after, state => Assert.Equal(0.0, state.SecondsOnCourt, 6));
        Assert.All(after, state => Assert.True(state.Energy > FatigueModel.MaxEnergy - 1.0));
    }

    // -------------------------------------------------------- performans egrisi

    [Fact]
    public void PerformanceCurveIsMonotonicAndBounded()
    {
        var model = FatigueModel.Baseline;

        var previous = double.MaxValue;

        for (var energy = 100; energy >= 0; energy -= 5)
        {
            var multiplier = model.PerformanceMultiplier(energy);

            Assert.InRange(multiplier, 0.0, 1.0);
            Assert.True(
                multiplier <= previous,
                $"Enerji azaldikça çarpan artmamalı: energy={energy} {multiplier} > {previous}");

            previous = multiplier;
        }
    }

    [Fact]
    public void PerformanceCurveInterpolatesBetweenAnchors()
    {
        // Regresyon: ilk uygulama 100'un altindaki HER enerji icin en ust kancayi
        // donduruyordu, yani 80 uzeri yorgunluk kanalı olüydü. Ara degerler
        // burada ANCAKLA sabitlenir.
        var model = FatigueModel.Baseline;

        Assert.Equal(1.00, model.PerformanceMultiplier(100.0), 6);
        Assert.Equal(0.99, model.PerformanceMultiplier(80.0), 6);
        Assert.Equal(0.97, model.PerformanceMultiplier(60.0), 6);
        Assert.Equal(0.92, model.PerformanceMultiplier(40.0), 6);
        Assert.Equal(0.84, model.PerformanceMultiplier(25.0), 6);
        Assert.Equal(0.72, model.PerformanceMultiplier(10.0), 6);
        Assert.Equal(0.65, model.PerformanceMultiplier(0.0), 6);

        // 100 ve 80 arasi: 90 -> 0.995, 70 -> 0.98
        Assert.Equal(0.995, model.PerformanceMultiplier(90.0), 6);
        Assert.Equal(0.980, model.PerformanceMultiplier(70.0), 6);

        // 80 ve 60 arasi: 70 -> 0.98 (yukaridaki ile ayni nokta)
        Assert.Equal(0.98, model.PerformanceMultiplier(70.0), 6);

        // 10 ve 0 arasi: 5 -> 0.685
        Assert.Equal(0.685, model.PerformanceMultiplier(5.0), 6);
    }

    [Fact]
    public void EnergyZeroIsAFloorNotACollapse()
    {
        // D63: sifir bir nokta degil bir TABANDIR.
        var model = FatigueModel.Baseline;

        Assert.True(model.PerformanceMultiplier(0) > 0.5);
        Assert.True(model.PerformanceMultiplier(0) < model.PerformanceMultiplier(50));

        // Tabanda carpanda surekli azalir; ani bir cokus olmaz.
        Assert.True(model.PerformanceMultiplier(5) > model.PerformanceMultiplier(0));
        Assert.True(model.PerformanceMultiplier(10) > model.PerformanceMultiplier(5));
    }

    [Fact]
    public void FatigueLoadIsZeroWhenRestedAndBoundedWhenExhausted()
    {
        var model = FatigueModel.Baseline;

        Assert.Equal(0.0, FatigueCalculator.FatigueLoad(model, 100.0), 10);
        Assert.InRange(FatigueCalculator.FatigueLoad(model, 0.0), 0.0, 1.0);

        // Tam yorgunluk: load = 1 - carpandir.
        Assert.Equal(
            1.0 - model.PerformanceMultiplier(0.0),
            FatigueCalculator.FatigueLoad(model, 0.0),
            10);
    }

    [Fact]
    public void StaminaHigherMeansSlowerDrain()
    {
        var model = FatigueModel.Baseline;
        var low = M2TestData.Ratings(20) with { Stamina = 20 };
        var high = M2TestData.Ratings(20) with { Stamina = 90 };

        Assert.True(
            FatigueCalculator.DrainPerSecond(model, high, 1.0)
                < FatigueCalculator.DrainPerSecond(model, low, 1.0));

        Assert.True(
            FatigueCalculator.RecoveryPerSecond(model, high)
                > FatigueCalculator.RecoveryPerSecond(model, low));
    }

    [Fact]
    public void MinimumEnergyStillProducesNoScoreAdvantageOverFullEnergyAtTheLogit()
    {
        // D64: yorgunluk yalniz suta girer. Yorgun ve dinlenmis arasindaki fark
        // z olceginde gorunur olmali.
        var model = FatigueModel.Baseline;

        var rested = ShotMath.MakeProbability(
            0.45, 74, 60, FatigueCalculator.FatigueLoad(model, 100.0), 0.6, 0.9, model.FatigueLogitScale);
        var tired = ShotMath.MakeProbability(
            0.45, 74, 60, FatigueCalculator.FatigueLoad(model, 0.0), 0.6, 0.9, model.FatigueLogitScale);

        Assert.True(tired < rested);
        Assert.InRange(rested - tired, 0.0, 0.2);
    }

    // -------------------------------------------------------------- yardimcilar

    private static ImmutableArray<Player> OnCourtOf(MatchSetup setup, TeamSide side)
    {
        var roster = side == TeamSide.Home ? setup.Home.Team.Roster : setup.Away.Team.Roster;
        var ids = side == TeamSide.Home ? setup.Home.Lineup.PlayerIds : setup.Away.Lineup.PlayerIds;

        return [.. ids.Select(id => roster.First(player => player.Id == id))];
    }

    private static int Possessions(EngineConfig config, Pace pace, ulong seed)
    {
        var setup = M2TestData.WithTactics(
            seed,
            OffensiveTactic.Balanced,
            DefensiveTactic.ManToMan,
            pace);

        var result = new MatchSimulation(config).Simulate(setup);

        return result.HomePossessions + result.AwayPossessions;
    }

    private static double AverageFinalEnergy(EngineConfig config, Pace pace, ulong seed)
    {
        var setup = M2TestData.WithTactics(
            seed,
            OffensiveTactic.Balanced,
            DefensiveTactic.ManToMan,
            pace);

        var result = new MatchSimulation(config).Simulate(setup);

        Assert.NotEmpty(result.PlayerEnergy);

        // Yalniz SAHADA oynayanlarin enerjisi raporlanir: yedekler hep 100'de
        // kalir ve ortalamayi yapay olarak yukseltir.
        var onCourt = setup.Home.Lineup.PlayerIds
            .Concat(setup.Away.Lineup.PlayerIds)
            .ToHashSet();

        var starters = result.PlayerEnergy
            .Where(report => onCourt.Contains(report.PlayerId))
            .ToList();

        Assert.NotEmpty(starters);

        return starters.Average(report => report.Energy);
    }

    private static Dictionary<OffensiveAction, int> ActionCounts(
        EngineConfig config,
        Pace pace,
        ulong seed)
    {
        var setup = M2TestData.WithTactics(
            seed,
            OffensiveTactic.Balanced,
            DefensiveTactic.ManToMan,
            pace);

        var result = new MatchSimulation(config).Simulate(setup);
        var counts = new Dictionary<OffensiveAction, int>();

        foreach (var action in result.Events
                     .Where(e => e.Type == MatchEventType.ActionCompleted)
                     .Select(e => e.PayloadAs<ActionCompletedPayload>().Action))
        {
            counts[action] = counts.GetValueOrDefault(action) + 1;
        }

        Assert.NotEmpty(counts);

        return counts;
    }
}
