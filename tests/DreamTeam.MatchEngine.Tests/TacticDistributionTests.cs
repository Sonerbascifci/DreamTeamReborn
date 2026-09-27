using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Tactics;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M4: 4 hücum taktiğinin normalize dağılımı (09 §64) ve policy değişiminin
/// aksiyon/şut karışımını etkilediğinin kanıtı.
/// </summary>
public class TacticDistributionTests
{
    private static OffensivePolicy Policy(EngineConfig? config = null) =>
        new(
            (config ?? M2TestData.Config()).Tactics,
            (config ?? M2TestData.Config()).ActionProfiles,
            new Ratings.PlayerRatingCalculator(
                (config ?? M2TestData.Config()).SelectionSpread));

    [Fact]
    public void EveryTacticDistributionSumsToOne()
    {
        var config = M2TestData.Config();
        var policy = Policy(config);

        foreach (var tactic in Enum.GetValues<OffensiveTactic>())
        {
            var resolved = policy.For(tactic);

            Assert.NotEmpty(resolved.Candidates);
            Assert.Equal(1.0, resolved.NormalizedWeights, 10);
            Assert.All(resolved.Candidates, candidate => Assert.True(candidate.Weight > 0.0));
        }
    }

    [Fact]
    public void AllTwelveTacticPairsAreSimulable()
    {
        // 4 hücum x 4 savunma = 16 kombinasyonun tamamı hatasız oynanabilmeli.
        var config = M2TestData.Config();
        var played = 0;

        foreach (var offense in Enum.GetValues<OffensiveTactic>())
        {
            foreach (var defense in Enum.GetValues<DefensiveTactic>())
            {
                var setup = M4TestData.Pair(offense, defense, offense, defense, seed: 31);
                var result = new MatchSimulation(config).Simulate(setup);

                Assert.Equal(MatchStatus.Completed, result.Status);
                Assert.True(result.Events.Length > 0);
                played += 1;
            }
        }

        Assert.Equal(16, played);
    }

    [Fact]
    public void ZeroWeightActionIsFilteredAndRemainderRenormalized()
    {
        // 05 §5: uygun olmayan aksiyon filtrelenirse kalanlar yeniden
        // normalize edilir. OffBallScreen agirligi 0.00 oldugu icin aday
        // listesine hic girmez.
        var policy = Policy();

        foreach (var tactic in Enum.GetValues<OffensiveTactic>())
        {
            var resolved = policy.For(tactic);

            Assert.DoesNotContain(
                resolved.Candidates,
                candidate => candidate.Profile.Action == OffensiveAction.OffBallScreen);
        }
    }

    [Fact]
    public void EveryZeroTotalWeightCaseThrows()
    {
        // 05 §5: "sıfır toplam ağırlık için açık fallback policy gerekir; sıfıra
        // bölme ve ilk listedeki oyuncuyu sürekli seçme hatası olmamalı."
        var config = M2TestData.Config();

        var empty = new TacticsModel
        {
            Offensive =
            [
                new OffensiveTacticProfile
                {
                    Tactic = OffensiveTactic.Balanced,
                    ShotBias = 0.0,
                    QualityBonus = 0.0,
                    Weights =
                    [
                        new ActionWeight(OffensiveAction.PickAndRoll, 0.0),
                        new ActionWeight(OffensiveAction.Drive, 0.0),
                    ],
                },
                .. config.Tactics.Offensive.Where(p => p.Tactic != OffensiveTactic.Balanced),
            ],
        };

        // Politika constructor'i TUM taktikleri birden cozer (normalizasyon
        // bir kez yapilir), bu yuzden hata constructor'da firlatilir.
        Assert.Throws<InvalidOperationException>(() => new OffensivePolicy(
            empty,
            config.ActionProfiles,
            new Ratings.PlayerRatingCalculator(config.SelectionSpread)));
    }

    [Fact]
    public void EveryTacticInConfigHasExactlyOneProfile()
    {
        var config = M2TestData.Config();
        var tactics = config.Tactics.Offensive.Select(p => p.Tactic).ToList();

        Assert.Equal(Enum.GetValues<OffensiveTactic>().Length, tactics.Count);
        Assert.Equal(tactics.Count, tactics.Distinct().Count());
    }

    [Fact]
    public void BalancedDistributionMatchesTheSpecExampleExactly()
    {
        // D62: 05 §5 PickAndRoll ornegini birebir alan dağılim M3'unkuyle ayni
        // olmalidir. Sapma, "tarafsiz" bir vektor uydurmak anlamina gelirdi.
        var policy = Policy();
        var weights = policy.For(OffensiveTactic.Balanced).Candidates
            .ToDictionary(c => c.Profile.Action, c => c.Weight);

        Assert.Equal(0.45, weights[OffensiveAction.PickAndRoll], 10);
        Assert.Equal(0.15, weights[OffensiveAction.Drive], 10);
        Assert.Equal(0.15, weights[OffensiveAction.SpotUp], 10);
        Assert.Equal(0.10, weights[OffensiveAction.Isolation], 10);
        Assert.Equal(0.10, weights[OffensiveAction.Cut], 10);
        Assert.Equal(0.05, weights[OffensiveAction.PostUp], 10);
    }

    [Fact]
    public void InsidePostShiftsWeightTowardPostUpAndAwayFromSpotUp()
    {
        var policy = Policy();
        var balanced = Weights(policy, OffensiveTactic.Balanced);
        var inside = Weights(policy, OffensiveTactic.InsidePost);

        Assert.True(inside[OffensiveAction.PostUp] > balanced[OffensiveAction.PostUp]);
        Assert.True(inside[OffensiveAction.SpotUp] < balanced[OffensiveAction.SpotUp]);
    }

    [Fact]
    public void PerimeterMotionShiftsWeightTowardOffBallActions()
    {
        var policy = Policy();
        var balanced = Weights(policy, OffensiveTactic.Balanced);
        var motion = Weights(policy, OffensiveTactic.PerimeterMotion);

        Assert.True(motion[OffensiveAction.Cut] > balanced[OffensiveAction.Cut]);
        Assert.True(motion[OffensiveAction.SpotUp] > balanced[OffensiveAction.SpotUp]);
    }

    // ---------------------------------------- M4'ün asil kabul kriteri

    [Fact]
    public void ControlledTacticChangeShiftsActionMix()
    {
        // 09 §64: "aynı fixture'da controlled policy değişimi beklenen aksiyon
        // karışımını etkiliyor". Tek macin sonuc yonu zorunlu DEGIL; karisimin
        // kaymasi esastir.
        var insidePost = ActionMix(OffensiveTactic.InsidePost, seed: 555);
        var perimeter = ActionMix(OffensiveTactic.PerimeterMotion, seed: 555);

        var postShare = insidePost.GetValueOrDefault(OffensiveAction.PostUp, 0);
        var perimeterPostShare = perimeter.GetValueOrDefault(OffensiveAction.PostUp, 0);

        Assert.True(
            postShare > perimeterPostShare,
            $"InsidePost PostUp payi={postShare:F4}, PerimeterMotion={perimeterPostShare:F4}");

        var insideSpotUp = insidePost.GetValueOrDefault(OffensiveAction.SpotUp, 0);
        var perimeterSpotUp = perimeter.GetValueOrDefault(OffensiveAction.SpotUp, 0);

        Assert.True(
            perimeterSpotUp > insideSpotUp,
            $"PerimeterMotion SpotUp payi={perimeterSpotUp:F4}, InsidePost={insideSpotUp:F4}");
    }

    [Fact]
    public void ControlledTacticChangeShiftsShotTypeMix()
    {
        // InsidePost -> ClosePost artar, ThreePoint azalir. ShotBias da ayni
        // yonde calisir (kucuk, ikinci katman).
        var insidePost = ShotTypeMix(OffensiveTactic.InsidePost, seed: 777);
        var perimeter = ShotTypeMix(OffensiveTactic.PerimeterMotion, seed: 777);

        Assert.True(
            insidePost.GetValueOrDefault(ShotType.ClosePost, 0)
                > perimeter.GetValueOrDefault(ShotType.ClosePost, 0),
            "InsidePost ClosePost payi daha yuksek olmali.");

        Assert.True(
            perimeter.GetValueOrDefault(ShotType.ThreePoint, 0)
                > insidePost.GetValueOrDefault(ShotType.ThreePoint, 0),
            "PerimeterMotion ThreePoint payi daha yuksek olmali.");
    }

    [Fact]
    public void TacticChoiceIsObservableInTheEventStream()
    {
        // D67: politika etkisi event'ten gozlenebilir olmali.
        var config = M2TestData.Config();
        var result = new MatchSimulation(config).Simulate(
            M4TestData.Pair(OffensiveTactic.InsidePost, DefensiveTactic.ManToMan,
                OffensiveTactic.InsidePost, DefensiveTactic.ManToMan, seed: 808));

        var shotTypes = result.Events
            .Where(e => e.Type == MatchEventType.ShotAttempt)
            .Select(e => e.PayloadAs<ShotAttemptPayload>().ShotType)
            .ToList();

        Assert.NotEmpty(shotTypes);

        // Butun aksiyonlar kayitli oldugu icin taktik degisimi PBP'den okunabilir.
        var actions = result.Events
            .Where(e => e.Type == MatchEventType.ActionCompleted)
            .Select(e => e.PayloadAs<ActionCompletedPayload>().Action)
            .ToList();

        Assert.NotEmpty(actions);
        Assert.Contains(OffensiveAction.PostUp, actions);
    }

    private static Dictionary<OffensiveAction, double> Weights(
        OffensivePolicy policy,
        OffensiveTactic tactic) =>
        policy.For(tactic).Candidates
            .ToDictionary(c => c.Profile.Action, c => c.Weight);

    private static Dictionary<OffensiveAction, double> ActionMix(OffensiveTactic tactic, ulong seed)
    {
        var config = M2TestData.Config();
        var result = new MatchSimulation(config).Simulate(
            M4TestData.Pair(tactic, DefensiveTactic.ManToMan, tactic, DefensiveTactic.ManToMan, seed));

        var counts = new Dictionary<OffensiveAction, int>();

        foreach (var action in result.Events
                     .Where(e => e.Type == MatchEventType.ActionCompleted)
                     .Select(e => e.PayloadAs<ActionCompletedPayload>().Action))
        {
            counts[action] = counts.GetValueOrDefault(action) + 1;
        }

        Assert.NotEmpty(counts);

        var total = counts.Values.Sum();

        return counts.ToDictionary(pair => pair.Key, pair => (double)pair.Value / total);
    }

    private static Dictionary<ShotType, double> ShotTypeMix(OffensiveTactic tactic, ulong seed)
    {
        var config = M2TestData.Config();
        var result = new MatchSimulation(config).Simulate(
            M4TestData.Pair(tactic, DefensiveTactic.ManToMan, tactic, DefensiveTactic.ManToMan, seed));

        var counts = new Dictionary<ShotType, int>();

        foreach (var shotType in result.Events
                     .Where(e => e.Type == MatchEventType.ShotAttempt)
                     .Select(e => e.PayloadAs<ShotAttemptPayload>().ShotType))
        {
            counts[shotType] = counts.GetValueOrDefault(shotType) + 1;
        }

        Assert.NotEmpty(counts);

        var total = counts.Values.Sum();

        return counts.ToDictionary(pair => pair.Key, pair => (double)pair.Value / total);
    }
}
