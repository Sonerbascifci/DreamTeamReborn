using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// Saf fonksiyon testleri. 08 §1: "formül/sınır doğruluğu".
/// Bunlar test double değildir; üretim algoritmasının kendisini sınarlar.
/// </summary>
public class ShotMathTests
{
    [Fact]
    public void MidRatingIgnoresSkillScaleEntirely()
    {
        // skill = 0 oldugunda pMake, taban olasiligin sigmoid donusumudur;
        // olcek ne olursa olsun etki etmez.
        foreach (var scale in new[] { -5.0, -0.6, 0.0, 0.6, 5.0 })
        {
            Assert.Equal(0.5, ShotMath.MakeProbability(0.5, 50, scale), 12);
        }
    }

    [Fact]
    public void LowAndHighRatingsAreSymmetricAroundHalfBaseProbability()
    {
        // skill = (rating - 50) / 50 simetrik oldugu icin taban olasilik 0.5
        // iken 30 ve 70 rating'i birbirine tam zit isabet olasiliklarini verir:
        // sigmoid(z) + sigmoid(-z) = 1.
        var low = ShotMath.MakeProbability(0.5, 30, 0.6);
        var high = ShotMath.MakeProbability(0.5, 70, 0.6);

        Assert.Equal(low, 1.0 - high, 12);
    }

    [Fact]
    public void ShiftedBaseProbabilityIsNotSymmetricAndShouldNotBe()
    {
        // Taban 0.36 iken logit(0.36) sifir olmadigi icin simetri beklemek
        // yanlistir. Bu, testin neyi KANITLAMADIGINI da kayda gecer.
        var low = ShotMath.MakeProbability(0.36, 30, 0.6);
        var high = ShotMath.MakeProbability(0.36, 70, 0.6);

        Assert.NotEqual(low, 1.0 - high, 6);
        Assert.True(high > low);
    }

    [Fact]
    public void MakeProbabilityIncreasesMonotonicallyWithSkill()
    {
        var previous = 0.0;

        for (var rating = 0; rating <= 100; rating += 5)
        {
            var probability = ShotMath.MakeProbability(0.36, rating, 0.6);

            Assert.True(probability > previous, $"Rating {rating} olasılığı artırmadı.");
            previous = probability;
        }
    }

    [Fact]
    public void MakeProbabilityDecreasesMonotonicallyWithSkillScale()
    {
        var atZeroScale = ShotMath.MakeProbability(0.36, 90, skillScale: 0.0);
        var atHighScale = ShotMath.MakeProbability(0.36, 90, skillScale: 4.0);

        Assert.True(atHighScale > atZeroScale);
        Assert.InRange(atHighScale, 0.0, 1.0);
    }

    [Fact]
    public void ExtremeRatingsNeverProduceNaNOrOutOfRange()
    {
        // 08 §82: uç ratinglerde NaN veya 1'in dışına çıkan olasılık olmamalı.
        foreach (var baseProbability in new[] { 0.01, 0.36, 0.53, 0.64, 0.99 })
        {
            foreach (var rating in new[] { int.MinValue, -1, 0, 50, 100, 101, int.MaxValue })
            {
                foreach (var scale in new[] { -10.0, -0.6, 0.0, 0.6, 10.0 })
                {
                    var probability = ShotMath.MakeProbability(baseProbability, rating, scale);

                    Assert.False(double.IsNaN(probability), "NaN üretildi.");
                    Assert.InRange(probability, 0.0, 1.0);
                }
            }
        }
    }

    [Fact]
    public void LogitRejectsProbabilitiesOutsideOpenUnitInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShotMath.Logit(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShotMath.Logit(1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShotMath.Logit(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShotMath.Logit(1.1));
    }

    [Fact]
    public void NonFiniteSkillScaleIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ShotMath.MakeProbability(0.5, 50, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ShotMath.MakeProbability(0.5, 50, double.PositiveInfinity));
    }

    [Fact]
    public void NormalizeSkillIsSymmetricAroundFifty()
    {
        Assert.Equal(0.0, ShotMath.NormalizeSkill(50), 12);
        Assert.Equal(-1.0, ShotMath.NormalizeSkill(0), 12);
        Assert.Equal(1.0, ShotMath.NormalizeSkill(100), 12);
        Assert.Equal(ShotMath.NormalizeSkill(70), -ShotMath.NormalizeSkill(30), 12);
    }

    [Theory]
    [InlineData(ShotType.AtRim, 2)]
    [InlineData(ShotType.ClosePost, 2)]
    [InlineData(ShotType.MidRange, 2)]
    [InlineData(ShotType.ThreePoint, 3)]
    public void ShotPointsMatchShotType(ShotType shotType, int expectedPoints) =>
        Assert.Equal(expectedPoints, ShotResolver.PointsFor(shotType));
}

public class WeightedSelectorTests
{
    private static IReadOnlyList<string> Candidates { get; } = ["a", "b", "c"];

    [Fact]
    public void ZeroWeightsAreRejectedInsteadOfSilentlyPickingTheFirstCandidate()
    {
        // 05 §5: sıfıra bölme ve "ilk listeki oyuncuyu sürekli seçme" hatası olmamalı.
        var random = new SeededRandom(1);

        Assert.Throws<InvalidOperationException>(
            () => WeightedSelector.Select(Candidates, _ => 0.0, random));
        Assert.Throws<InvalidOperationException>(
            () => WeightedSelector.Select(Array.Empty<string>(), _ => 1.0, random));
    }

    [Fact]
    public void NegativeOrNonFiniteWeightsAreRejected()
    {
        var random = new SeededRandom(1);

        Assert.Throws<InvalidOperationException>(
            () => WeightedSelector.Select(Candidates, _ => -1.0, random));
        Assert.Throws<InvalidOperationException>(
            () => WeightedSelector.Select(Candidates, _ => double.NaN, random));
        Assert.Throws<InvalidOperationException>(
            () => WeightedSelector.Select(Candidates, _ => double.PositiveInfinity, random));
    }

    [Fact]
    public void SingleCandidateIsAlwaysChosen()
    {
        var random = new SeededRandom(7);

        for (var draw = 0; draw < 100; draw++)
        {
            Assert.Equal("a", WeightedSelector.Select(["a"], _ => 1.0, random));
        }
    }

    [Fact]
    public void EveryCandidateIsReachableIncludingTheLast()
    {
        var random = new SeededRandom(2026);
        var seen = new HashSet<string>();

        for (var draw = 0; draw < 500; draw++)
        {
            seen.Add(WeightedSelector.Select(Candidates, _ => 1.0, random));
        }

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void ZeroWeightCandidateIsNeverChosen()
    {
        var random = new SeededRandom(2026);

        for (var draw = 0; draw < 2_000; draw++)
        {
            var chosen = WeightedSelector.Select(
                Candidates,
                candidate => candidate == "b" ? 0.0 : 1.0,
                random);

            Assert.NotEqual("b", chosen);
        }
    }

    [Fact]
    public void HeavierWeightIsPickedMoreOften()
    {
        // Gevşek istatistiksel kontrol: 10.000 çekilişte ağırlığı 9 olan aday
        // belirgin biçimde daha sık seçilmelidir. Bu test dağılımı yoklar, sabitlemez.
        var random = new SeededRandom(31);
        var heavyCount = 0;

        for (var draw = 0; draw < 10_000; draw++)
        {
            if (WeightedSelector.Select(Candidates, c => c == "a" ? 9.0 : 1.0, random) == "a")
            {
                heavyCount += 1;
            }
        }

        // ~8182 beklenir; geniş bant kabul edilir.
        Assert.InRange(heavyCount, 7_500, 8_800);
    }
}

public class ConfigHashTests
{
    [Fact]
    public void SameConfigAlwaysProducesTheSameHash()
    {
        Assert.Equal(
            M2TestData.Config().ComputeConfigHash(),
            M2TestData.Config().ComputeConfigHash());
    }

    [Fact]
    public void DifferentConfigProducesDifferentHash()
    {
        var slowerActions = M2TestData.Config().Actions with { SetupActionMs = 9_000 };

        Assert.NotEqual(
            M2TestData.Config().ComputeConfigHash(),
            M2TestData.Config(actions: slowerActions).ComputeConfigHash());
    }

    [Fact]
    public void EveryMeaningfulConfigFieldIsCoveredByTheHash()
    {
        // Kanonik metinde unutulan bir alan, config değiştiği halde aynı hash'i
        // üretir ve replay kimliği sessizce bozulur.
        var baseline = M2TestData.Config();
        var baselineHash = baseline.ComputeConfigHash();

        var variants = new Dictionary<string, EngineConfig>
        {
            ["periodCount"] = M2TestData.Config(rules: baseline.Rules with { PeriodCount = 5 }),
            ["periodDuration"] = M2TestData.Config(rules: baseline.Rules with { PeriodDurationMs = 10 * 60 * 1000 }),
            ["shotClock"] = M2TestData.Config(rules: baseline.Rules with { ShotClockMs = 20_000 }),
            ["overtime"] = M2TestData.Config(rules: baseline.Rules with { OvertimeDurationMs = 100_000 }),
            ["shotBase"] = M2TestData.Config(shot: baseline.Shot with { ThreePointBase = 0.40 }),
            ["skillScale"] = M2TestData.Config(shot: baseline.Shot with { SkillScale = 0.9 }),
            ["setupActionMs"] = M2TestData.Config(actions: baseline.Actions with { SetupActionMs = 7_000 }),
            ["shotFlightMs"] = M2TestData.Config(actions: baseline.Actions with { ShotFlightMs = 2_000 }),
            ["shotCompletion"] = M2TestData.Config(actions: baseline.Actions with { ShotCompletionProbability = 0.8 }),
            ["turnover"] = M2TestData.Config(actions: baseline.Actions with { TurnoverProbability = 0.09 }),
            ["offensiveRebound"] = M2TestData.Config(actions: baseline.Actions with { OffensiveReboundProbability = 0.3 }),
            ["maxActions"] = M2TestData.Config(maxActionsPerMatch: 1),
        };

        foreach (var (field, variant) in variants)
        {
            Assert.NotEqual(baselineHash, variant.ComputeConfigHash());
            Assert.False(string.IsNullOrWhiteSpace(field));
        }
    }

    [Fact]
    public void ActionProfileWeightsAreCoveredByTheHash()
    {
        var baseline = M2TestData.Config();
        var profiles = baseline.ActionProfiles;

        var changed = profiles.ToArray();
        changed[0] = changed[0] with { Weight = changed[0].Weight / 2.0 };

        Assert.NotEqual(
            baseline.ComputeConfigHash(),
            M2TestData.Config(actionProfiles: [.. changed]).ComputeConfigHash());
    }

    [Fact]
    public void HashIsShortAndHex()
    {
        var hash = M2TestData.Config().ComputeConfigHash();

        Assert.Equal(16, hash.Length);
        Assert.All(hash, character => char.IsAsciiHexDigitLower(character));
    }
}
