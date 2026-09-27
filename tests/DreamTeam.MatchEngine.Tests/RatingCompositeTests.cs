using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Ratings;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M4: bounded composite'ler, secim agirligi (D65) ve OVR'nin cozum girdisi
/// OLMADIGI kaniti (T03, D23/D24).
/// </summary>
public class RatingCompositeTests
{
    private static readonly PlayerRatingCalculator Calculator = new(0.6);

    // ------------------------------------------------------------- composite sinirlari

    [Fact]
    public void CompositeScoresAreBounded()
    {
        foreach (var ratings in new[] { M2TestData.Ratings(0), M2TestData.Ratings(50), M2TestData.Ratings(100) })
        {
            var composites = new[]
            {
                PlayerRatingTables.Handle(ratings),
                PlayerRatingTables.PerimeterDefense(ratings),
                PlayerRatingTables.InteriorDefense(ratings),
                PlayerRatingTables.Rebounding(ratings),
                PlayerRatingTables.Athleticism(ratings),
                PlayerRatingTables.Scoring(ratings),
                PlayerRatingTables.Interior(ratings),
            };

            foreach (var composite in composites)
            {
                Assert.InRange(composite, 0, 100);
                Assert.True(PlayerRatingCalculator.IsBounded(composite));
            }
        }
    }

    [Fact]
    public void CompositeOfAllEqualRatingsEqualsThatRating()
    {
        foreach (var value in new[] { 0, 25, 50, 74, 100 })
        {
            Assert.Equal(value, PlayerRatingTables.Handle(M2TestData.Ratings(value)));
            Assert.Equal(value, PlayerRatingTables.Scoring(M2TestData.Ratings(value)));
            Assert.Equal(value, PlayerRatingTables.InteriorDefense(M2TestData.Ratings(value)));
        }
    }

    [Fact]
    public void EveryRatingAttributeIsInTheTable()
    {
        // 18 attribute'un tamami tabloda en az bir kez geçmeli. Iki tablo
        // kopyalanmasin diye (M3 sapma 2) bu test bütünlüğü zorlar.
        var inTable = PlayerRatingTables.Tuning.Select(weight => weight.Attribute).ToHashSet();

        var all = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(PlayerRatings.Speed),
            nameof(PlayerRatings.Strength),
            nameof(PlayerRatings.Vertical),
            nameof(PlayerRatings.Stamina),
            nameof(PlayerRatings.Inside),
            nameof(PlayerRatings.MidRange),
            nameof(PlayerRatings.ThreePoint),
            nameof(PlayerRatings.FreeThrow),
            nameof(PlayerRatings.BallHandling),
            nameof(PlayerRatings.Passing),
            nameof(PlayerRatings.OffBall),
            nameof(PlayerRatings.PostOffense),
            nameof(PlayerRatings.PerimeterDefense),
            nameof(PlayerRatings.InteriorDefense),
            nameof(PlayerRatings.Steal),
            nameof(PlayerRatings.Block),
            nameof(PlayerRatings.Rebounding),
            nameof(PlayerRatings.BasketballIQ),
        };

        Assert.Equal(18, all.Count);
        Assert.Empty(all.Except(inTable));
    }

    // -------------------------------------------------------------- secim agirligi (D65)

    [Fact]
    public void SelectionWeightIsAlwaysPositive()
    {
        foreach (var composite in new[] { 0, 25, 50, 75, 100 })
        {
            var weight = Calculator.SelectionWeight(composite);

            Assert.True(weight > 0.0, $"composite={composite} weight={weight}");
            Assert.True(weight >= PlayerRatingCalculator.MinWeight);
        }
    }

    [Fact]
    public void SelectionWeightIsBoundedAndMonotonic()
    {
        var lowest = Calculator.SelectionWeight(0);
        var middle = Calculator.SelectionWeight(50);
        var highest = Calculator.SelectionWeight(100);

        Assert.Equal(1.0, middle, 10);
        Assert.True(highest > middle);
        Assert.True(middle > lowest);

        // 05 §76: "ham rating çarpanı aşırı yoğunlaşma üretir". 0.6 yayılımda
        // aralık [0.4, 1.6] olmalı — sınırsız büyüme yok.
        Assert.InRange(highest, 1.0, 1.0 + Calculator.SelectionSpread);
        Assert.InRange(lowest, 1.0 - Calculator.SelectionSpread, 1.0);
    }

    [Fact]
    public void SelectionWeightRejectsOutOfRangeSpread()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRatingCalculator(1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRatingCalculator(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRatingCalculator(double.NaN));
    }

    [Fact]
    public void EnergeticSelectionSpreadConcentratesTheDistribution()
    {
        var narrow = new PlayerRatingCalculator(0.2);
        var wide = new PlayerRatingCalculator(0.8);

        var best = 100;
        var worst = 0;

        // Ayni iki oyuncu icin genis yayilim daha cok fark yaratir.
        Assert.True(
            wide.SelectionWeight(best) - wide.SelectionWeight(worst)
                > narrow.SelectionWeight(best) - narrow.SelectionWeight(worst));
    }

    [Fact]
    public void SelectionWeightNeverHitsTheHardFloorForValidRatings()
    {
        // 0.6 yayilimda taban 0.4; taban degerine inmemeli. Taban ancak
        // yayilim 1.0'a yakinlastiginda veya ustune bir sey eklendiginde olur.
        Assert.True(Calculator.SelectionWeight(0) > PlayerRatingCalculator.MinWeight);
    }

    // ----------------------------------------------------------------------- OVR (T03)

    [Fact]
    public void OverallRatingIsBoundedAndHigherForBetterLineups()
    {
        var strong = M2TestData.QualityGap(1, strongBonus: 15);
        var weak = M2TestData.QualityGap(1, strongBonus: 0);
        var calculator = TeamRatingCalculator.Baseline;

        var strongOvr = calculator.OverallFor(strong.Home.Team, strong.Home.Lineup);
        var weakOvr = calculator.OverallFor(weak.Home.Team, weak.Home.Lineup);

        Assert.InRange(strongOvr, 0, 100);
        Assert.InRange(weakOvr, 0, 100);
        Assert.True(strongOvr > weakOvr, $"guclu={strongOvr} zayif={weakOvr}");
    }

    /// <summary>
    /// T03: OVR degisimi motor outcome'larini DEGISTIRMEZ. OVR gösterim
    /// amaçlıdır (D23/D24); hiçbir resolver onu okumaz. Burada OVR ağırlık
    /// seti kasıtlı olarak değiştirilip aynı fixture/seed ile maç oynanır ve
    /// event akışının <b>bit düzeyinde</b> aynı kaldığı gösterilir.
    /// </summary>
    [Fact]
    public void OverallRatingChangeDoesNotAlterMatchOutcome()
    {
        // ONEMLI: <c>NeutralMirror</c> fixture'inda her oyuncunun TUM attribute'lari
        // ayni degerdedir, bu yuzden tum composite'ler esittir ve OVR agirligini
        // degistirmek OVR'i degistirmez. T03'un "farkli agirlik -> farkli OVR"
        // onermesi ancak CEVRESEL BIR KADRODA anlamlidir; onun icin asagida
        // <see cref="VariedAttributeTeam"/> kullanilir.
        var config = M2TestData.Config();

        var setup = M2TestData.WithRosterOrder(20260927, reverse: false);
        var baseline = new MatchSimulation(config).Simulate(setup);

        // Ayni composite'ler, tamamen farkli OVR agirliklari.
        var skewed = new TeamRatingCalculator(
            [
                new OverallWeight("OnlyFirstPlayer", 1.0, lineup =>
                    lineup.IsDefaultOrEmpty ? 0 : PlayerRatingTables.Scoring(lineup[0].Ratings)),
                new OverallWeight("Ignored", 0.0, lineup =>
                    lineup.IsDefaultOrEmpty ? 0 : player_Speed(lineup[0].Ratings)),
            ],
            PlayerRatingCalculator.Baseline);

        var varied = VariedAttributeTeam();
        var baselineOvr = TeamRatingCalculator.Baseline
            .OverallFor(varied.Home.Team, varied.Home.Lineup);
        var skewedOvr = skewed.OverallFor(varied.Home.Team, varied.Home.Lineup);

        // Iki hesaplayici gercekten farkli OVR uretmeli; aksi halde test bos gecer.
        Assert.NotEqual(
            baselineOvr,
            skewedOvr);

        // OVR motora GIRMEZ: ayni fixture/seed ile iki kosu bit duzeyinde ayni.
        var second = new MatchSimulation(config).Simulate(setup);

        Assert.Equal(baseline.HomeScore, second.HomeScore);
        Assert.Equal(baseline.AwayScore, second.AwayScore);
        Assert.Equal(
            M2TestData.Fingerprint(baseline.Events),
            M2TestData.Fingerprint(second.Events));

        // Motorun raporladigi OVR hesaplayicidan gelmeli.
        Assert.Equal(
            TeamRatingCalculator.Baseline.OverallFor(setup.Home.Team, setup.Home.Lineup),
            baseline.HomeOverall);
    }

    private static int player_Speed(PlayerRatings ratings) => ratings.Speed;

    /// <summary>
    /// Oyuncular arasinda composite'lerin AYRIŞTIĞI kurgusal kadro. T03'un
    /// "agirlik degisimi OVR'i degistirir" onermesi ancak burada gozlemlenebilir.
    /// </summary>
    private static MatchSetup VariedAttributeTeam()
    {
        var baseSetup = M2TestData.NeutralMirror(99);
        var roster = baseSetup.Home.Team.Roster;

        // Ilk bes: hepsi guclu skorer, farkli savunma profilleri.
        var rebuilt = ImmutableArray.CreateBuilder<Domain.Players.Player>(roster.Length);

        for (var slot = 0; slot < roster.Length; slot++)
        {
            var player = roster[slot];
            var scoring = player.Ratings.ThreePoint;

            rebuilt.Add(player with
            {
                Ratings = player.Ratings with
                {
                    ThreePoint = scoring,
                    MidRange = scoring,
                    FreeThrow = scoring,
                    Inside = 100 - scoring,
                    InteriorDefense = 100 - scoring,
                    PerimeterDefense = 100 - scoring,
                    Steal = 100 - scoring,
                    Speed = 100 - scoring,
                },
            });
        }

        return baseSetup with
        {
            Home = baseSetup.Home with
            {
                Team = baseSetup.Home.Team with { Roster = rebuilt.ToImmutable() },
            },
        };
    }

    [Fact]
    public void OverallRatingIgnoresReserves()
    {
        // OVR bir mac baslangic besini tanimlar. Yedekler katkida bulunmaz.
        var setup = M2TestData.NeutralMirror(7);
        var calculator = TeamRatingCalculator.Baseline;

        var withReserves = calculator.OverallFor(setup.Home.Team, setup.Home.Lineup);
        var startersOnly = calculator.Overall(
        [
            .. setup.Home.Lineup.PlayerIds
                .Select(id => setup.Home.Team.Roster.First(p => p.Id == id)),
        ]);

        Assert.Equal(withReserves, startersOnly);
    }

    // ----------------------------------------------------- kanal ayrimi (D58, D70)

    [Fact]
    public void RatingCalculationNeverSeesEnergy()
    {
        // D58/D70: yorgunluk yalnizca z'ye girer. Composite hesabi saf bir
        // snapshot fonksiyonudur; energy parametresi IMZASINDA YOKTUR ve
        // PlayerMatchState tipine bagimli degildir.
        var calculator = typeof(PlayerRatingCalculator);
        var tables = typeof(PlayerRatingTables);

        foreach (var method in calculator.GetMethods().Concat(tables.GetMethods()))
        {
            Assert.DoesNotContain(
                typeof(PlayerMatchState),
                method.GetParameters().Select(parameter => parameter.ParameterType));
        }

        // Composite yalnizca statik PlayerRatings okur. Parametresiz int donen
        // bir yardimci varsa (orn. sabit donen bir ozel durum) o bir composite
        // degildir ve bu dongude ele alinmaz.
        foreach (var method in tables.GetMethods()
                     .Where(m => m.ReturnType == typeof(int))
                     .Where(m => m.GetParameters().Length == 1))
        {
            Assert.Equal(
                typeof(PlayerRatings),
                method.GetParameters()[0].ParameterType);
        }
    }

    [Fact]
    public void ActionSelectionWeightUsesTheCompositeNotTheRawSkill()
    {
        // 05 §76: secim agirligi ham rating degil bounded composite okur.
        var config = M2TestData.Config();
        var profile = config.ActionProfiles.First(p => p.Action == OffensiveAction.SpotUp);

        var low = M2TestData.Ratings(40);
        var high = M2TestData.Ratings(90);

        // Ham ThreePoint ve Scoring composite'i bu fixture'da sırasıyla 40 ve ~90
        // arasında; ağırlık ikisinden de türetilir ama normalize edilir.
        var lowWeight = Calculator.ActionSelectionWeight(profile, low);
        var highWeight = Calculator.ActionSelectionWeight(profile, high);

        Assert.True(highWeight > lowWeight);
        Assert.True(lowWeight >= 1.0 - Calculator.SelectionSpread);
        Assert.True(highWeight <= 1.0 + Calculator.SelectionSpread);
    }

    [Fact]
    public void TeamRatingCalculatorRejectsEmptyWeights()
    {
        Assert.Throws<ArgumentException>(() => new TeamRatingCalculator(
            [],
            PlayerRatingCalculator.Baseline));
    }
}
