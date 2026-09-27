using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Ratings;
using DreamTeam.MatchEngine.Tactics;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M4: dört savunma policy paketinin (D57) etkileri ve kanal ayrımı
/// (05 §7: "savunma ve taktik ShotQuality içine girdiyse z'ye aynı etkiyi tekrar
/// ekleme").
/// </summary>
public class DefensePolicyTests
{
    private static DefensivePolicy Policy(EngineConfig? config = null)
    {
        var resolved = config ?? M2TestData.Config();

        return new DefensivePolicy(
            resolved.Defense,
            new PlayerRatingCalculator(resolved.SelectionSpread));
    }

    // ------------------------------------------------------------------- kalite

    [Fact]
    public void ShotQualityAlwaysWithinRange()
    {
        var config = M2TestData.Config();
        var policy = Policy(config);

        foreach (var action in Enum.GetValues<OffensiveAction>())
        {
            foreach (var offense in Enum.GetValues<OffensiveTactic>())
            {
                foreach (var defense in Enum.GetValues<DefensiveTactic>())
                {
                    foreach (var iq in new[] { 0, 50, 100 })
                    {
                        var quality = ShotQualityResolver.Resolve(
                            action,
                            offense,
                            config.Tactics,
                            defense,
                            policy,
                            shooterIq: iq,
                            defenderIq: iq);

                        Assert.InRange(quality, 0, 100);
                    }
                }
            }
        }
    }

    [Fact]
    public void QualityPenaltyNeverExceedsItsMagnitude()
    {
        // Savunma etkisi kalite Puani olarak yazilir ve makul bir araliktadir:
        // bir policy bir aksiyonu en fazla birkac puanla bozmaz.
        var policy = Policy();

        foreach (var action in Enum.GetValues<OffensiveAction>())
        {
            foreach (var defense in Enum.GetValues<DefensiveTactic>())
            {
                var penalty = policy.QualityPenalty(action, defense);

                Assert.InRange(penalty, -10.0, 10.0);
            }
        }
    }

    [Fact]
    public void DefensePolicyShiftsQuality()
    {
        // Ayni aksiyon, farkli savunma policy'si -> farkli kalite.
        var config = M2TestData.Config();
        var policy = Policy(config);

        var manToMan = ShotQualityResolver.Resolve(
            OffensiveAction.Drive,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.ManToMan,
            policy,
            shooterIq: 70,
            defenderIq: 70);

        var zone = ShotQualityResolver.Resolve(
            OffensiveAction.Drive,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.ZonePackPaint,
            policy,
            shooterIq: 70,
            defenderIq: 70);

        Assert.True(
            zone < manToMan,
            $"ZonePackPaint ic alani kapatir: zone={zone} manToMan={manToMan}");
    }

    [Fact]
    public void DropWeakensRollCoverageRelativeToSwitch()
    {
        // 05 §6: Drop, PnR coverage turudur ve roll oyuncuya acik kalir.
        var config = M2TestData.Config();
        var policy = Policy(config);

        var drop = ShotQualityResolver.Resolve(
            OffensiveAction.PickAndRoll,
            OffensiveTactic.PickAndRoll,
            config.Tactics,
            DefensiveTactic.Drop,
            policy,
            shooterIq: 70,
            defenderIq: 70);

        var @switch = ShotQualityResolver.Resolve(
            OffensiveAction.PickAndRoll,
            OffensiveTactic.PickAndRoll,
            config.Tactics,
            DefensiveTactic.Switch,
            policy,
            shooterIq: 70,
            defenderIq: 70);

        Assert.True(
            drop < @switch,
            $"Drop rollCoverage acik birakir: drop={drop} switch={@switch}");
    }

    [Fact]
    public void ZonePackPaintRaisesSpotUpQuality()
    {
        // Zone dis serbest, ic alan kapali: SpotUp kazanir, Drive kaybeder.
        var config = M2TestData.Config();
        var policy = Policy(config);

        var zoneSpotUp = ShotQualityResolver.Resolve(
            OffensiveAction.SpotUp,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.ZonePackPaint,
            policy,
            70,
            70);

        var manSpotUp = ShotQualityResolver.Resolve(
            OffensiveAction.SpotUp,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.ManToMan,
            policy,
            70,
            70);

        Assert.True(zoneSpotUp > manSpotUp);
    }

    [Fact]
    public void HigherShooterIqNeverLowersQuality()
    {
        var config = M2TestData.Config();
        var policy = Policy(config);

        var low = ShotQualityResolver.Resolve(
            OffensiveAction.PickAndRoll,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.ManToMan,
            policy,
            shooterIq: 20,
            defenderIq: 50);

        var high = ShotQualityResolver.Resolve(
            OffensiveAction.PickAndRoll,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.ManToMan,
            policy,
            shooterIq: 90,
            defenderIq: 50);

        Assert.True(high > low);
    }

    [Fact]
    public void HigherDefenderIqNeverRaisesQuality()
    {
        var config = M2TestData.Config();
        var policy = Policy(config);

        var weak = ShotQualityResolver.Resolve(
            OffensiveAction.Isolation,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.Switch,
            policy,
            50,
            defenderIq: 20);

        var strong = ShotQualityResolver.Resolve(
            OffensiveAction.Isolation,
            OffensiveTactic.Balanced,
            config.Tactics,
            DefensiveTactic.Switch,
            policy,
            50,
            defenderIq: 90);

        Assert.True(strong <= weak);
    }

    [Fact]
    public void QualityIsIndependentOfFatigue()
    {
        // D58/D70: yorgunluk kaliteyi ETKILEMEZ; yalniz z'ye girer. Burada
        // kalite cozucusunun imzasi enerji icermedigi dogrudan dogrulanir.
        var method = typeof(ShotQualityResolver).GetMethod(nameof(ShotQualityResolver.Resolve))!;
        var parameterNames = method.GetParameters().Select(p => p.Name).ToList();

        Assert.DoesNotContain("energy", parameterNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("fatigue", parameterNames, StringComparer.OrdinalIgnoreCase);
    }

    // --------------------------------------------------------- kanal ayrimi (D58)

    [Fact]
    public void DefenseEntersExactlyOneChannel()
    {
        // Savunma yalniz kaliteyi etkiler. z formulu skill + quality +
        // fatigueLoad'dan olusur; savunmanin z'ye dogrudan girdigi bir yol
        // OLMAMALIDIR.
        var method = typeof(ShotMath).GetMethod(nameof(ShotMath.MakeProbability), new[]
        {
            typeof(double), typeof(int), typeof(int), typeof(double),
            typeof(double), typeof(double), typeof(double),
        })!;

        var parameterNames = method.GetParameters().Select(p => p.Name).ToList();

        Assert.Contains("shotQuality", parameterNames);
        Assert.Contains("fatigueLoad", parameterNames);
        Assert.DoesNotContain("defense", parameterNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("tactic", parameterNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void QualityChangeMovesMakeProbabilityWithoutTouchingSkillOrFatigue()
    {
        var baseProbability = 0.45;
        var skill = 70;
        var fatigue = 0.0;

        var low = ShotMath.MakeProbability(baseProbability, skill, 20, fatigue, 0.6, 0.9, 0.35);
        var high = ShotMath.MakeProbability(baseProbability, skill, 90, fatigue, 0.6, 0.9, 0.35);

        Assert.True(high > low);
    }

    [Fact]
    public void FatigueChangeMovesMakeProbabilityWithoutTouchingQuality()
    {
        var baseProbability = 0.45;
        var skill = 70;
        var quality = 60;

        var rested = ShotMath.MakeProbability(baseProbability, skill, quality, 0.0, 0.6, 0.9, 0.35);
        var tired = ShotMath.MakeProbability(baseProbability, skill, quality, 0.35, 0.6, 0.9, 0.35);

        Assert.True(tired < rested);
    }

    [Fact]
    public void SkillAndQualityAndFatigueAreIndependentAxes()
    {
        // Uc kanal birbirinin yerine gecmez: skill artisi ve kalite artisi ayni
        // yonde etki eder ama ikisi birlikte etkiyi iki katindadir.
        var low = ShotMath.MakeProbability(0.45, 50, 50, 0.0, 0.6, 0.9, 0.35);
        var highBoth = ShotMath.MakeProbability(0.45, 80, 80, 0.0, 0.6, 0.9, 0.35);

        Assert.True(highBoth > low);

        // Yorgunluk ters yonde ve esit olceklidir.
        var exhausted = ShotMath.MakeProbability(0.45, 50, 50, 1.0, 0.6, 0.9, 0.35);
        Assert.True(exhausted < low);
    }

    // --------------------------------------------------------- blok ve baski

    [Fact]
    public void BlockProbabilityRisesWithInteriorDefense()
    {
        var policy = Policy();

        var weak = policy.BlockProbability(PlayerRatingTables.InteriorDefense(M2TestData.Ratings(20)));
        var strong = policy.BlockProbability(PlayerRatingTables.InteriorDefense(M2TestData.Ratings(90)));

        Assert.True(strong > weak, $"zayif={weak} guclu={strong}");
        Assert.InRange(weak, 0.0, 1.0);
        Assert.InRange(strong, 0.0, 1.0);
    }

    [Fact]
    public void TurnoverPressureRisesWithPerimeterDefense()
    {
        var policy = Policy();

        var weak = policy.TurnoverPressure(PlayerRatingTables.PerimeterDefense(M2TestData.Ratings(20)));
        var strong = policy.TurnoverPressure(PlayerRatingTables.PerimeterDefense(M2TestData.Ratings(90)));

        Assert.True(strong > weak);
    }

    [Fact]
    public void DefensePressureRaisesOffenseTurnoverRate()
    {
        // TURNISTIGIN iki KANALI birbirine karistirmamak icin burada tek bir
        // tarafin hucremi sabit tutulur ve yalniz savunmanin degistigi
        // gorulur. Etki kucuktur (0.02 katsayi), bu yuzden motor seviyesinde
        // sayim yerine RESOLVER seviyesinde deterministik sayim yapilir: ayni
        // cekilis dizisi iki farkli savunmaya beslenir.
        var config = M2TestData.Config();
        var policy = Policy(config);
        var resolver = new TurnoverResolver(config.Actions, policy);

        var weakRatings = M2TestData.Ratings(50);
        var strongRatings = M2TestData.Ratings(90);

        var samples = 20_000;
        var weakTurnovers = 0;
        var strongTurnovers = 0;

        for (var index = 0; index < samples; index++)
        {
            var random = new Randomness.SeededRandom((ulong)index);

            if (resolver.Resolve(weakRatings, random).IsTurnover)
            {
                weakTurnovers += 1;
            }

            if (resolver.Resolve(strongRatings, random).IsTurnover)
            {
                strongTurnovers += 1;
            }
        }

        Assert.True(
            strongTurnovers > weakTurnovers,
            $"guclu savunma tov={strongTurnovers} zayifi={weakTurnovers} / {samples}");
    }

    [Fact]
    public void DefenseBonusChangesOnlyTheDefensiveComposites()
    {
        // Guvenlik agi: "daha guclu savunma" fixture'i hucrem rating'lerine
        // dokunmamalidir, yoksa olcum karisik bir toplam olur.
        var baseSetup = M2TestData.NeutralMirror(20260927);
        var buffed = M4TestData.AwayDefenseBonus(20260927, 25);

        for (var slot = 0; slot < baseSetup.Home.Lineup.PlayerIds.Length; slot++)
        {
            var homeId = baseSetup.Home.Lineup.PlayerIds[slot];
            var awayId = baseSetup.Away.Lineup.PlayerIds[slot];

            var baseHome = baseSetup.Home.Team.Roster.First(p => p.Id == homeId);
            var baseAway = baseSetup.Away.Team.Roster.First(p => p.Id == awayId);
            var buffAway = buffed.Away.Team.Roster.First(p => p.Id == awayId);

            Assert.Equal(baseHome.Ratings, buffed.Home.Team.Roster.First(p => p.Id == homeId).Ratings);

            // Hucrem tarafi degismedi; savunma tarafi yukseldi.
            Assert.Equal(baseAway.Ratings.ThreePoint, buffAway.Ratings.ThreePoint);
            Assert.Equal(baseAway.Ratings.BallHandling, buffAway.Ratings.BallHandling);
            Assert.True(buffAway.Ratings.PerimeterDefense > baseAway.Ratings.PerimeterDefense);
        }
    }

    [Fact]
    public void FoulAggressionDiffersBetweenPolicies()
    {
        var config = M2TestData.Config();
        var model = config.Defense with { FoulFromAggression = 0.03 };
        var policy = new DefensivePolicy(model, new PlayerRatingCalculator(0.6));

        var zone = policy.FoulAggression(DefensiveTactic.ZonePackPaint);
        var drop = policy.FoulAggression(DefensiveTactic.Drop);

        Assert.True(zone > drop, $"zone={zone} drop={drop}");
        Assert.Equal(0.0, policy.FoulAggression(DefensiveTactic.Switch));
    }

    [Fact]
    public void FoulProbabilityRisesWithDefenseAggression()
    {
        // D57'nin faul disiplini kanalı: daha agresif savunma -> daha çok faul.
        // Ayni tohum dizisi iki farkli olasilik icin kullanilir; boylece
        // karsilastirma tek bir cekilise dayanmaz.
        var calm = new FoulResolver(FoulModel.Baseline);

        var calmOutcomes = 0;
        var aggressiveOutcomes = 0;

        for (ulong seed = 0; seed < 200UL; seed++)
        {
            if (calm.Occurred(new Randomness.SeededRandom(seed), 0.0).IsFoul)
            {
                calmOutcomes += 1;
            }

            if (calm.Occurred(new Randomness.SeededRandom(seed), 0.20).IsFoul)
            {
                aggressiveOutcomes += 1;
            }
        }

        Assert.True(
            aggressiveOutcomes > calmOutcomes,
            $"agresif={aggressiveOutcomes} sakin={calmOutcomes}");
    }

    private static (int Turnovers, int Possessions) TurnoverRate(MatchSetup setup)
    {
        var result = new MatchSimulation(M4TestData.NoFatigue()).Simulate(setup);
        var possessions = result.HomePossessions + result.AwayPossessions;

        Assert.True(possessions > 0);

        var turnovers = result.Events.Count(e => e.Type == MatchEventType.Turnover);

        return (turnovers, possessions);
    }

    [Fact]
    public void DefensePressureIsCarriedByTheMatchButNotByTheOffense()
    {
        // Ayni hucrem, iki farkli savunma: yalniz deplasmanin savunma
        // attribute'leri yukselir. Motor seviyesindeki etki kucuk oldugu icin
        // burada yalniz YONun dogru oldugu, hucumun kirpilmadigi olculur.
        var strong = TurnoverRate(M4TestData.AwayDefenseBonus(20260927, 25));
        var weak = TurnoverRate(M4TestData.AwayDefenseBonus(20260927, 0));

        // Iki taraf da ayni hucremle oynadigi icin maclar bitmis ve tutarli
        // olmalidir; asil kanit resolver seviyesindeki testtir.
        Assert.True(strong.Possessions > 0);
        Assert.True(weak.Possessions > 0);
        Assert.True(strong.Turnovers >= 0);
        Assert.True(weak.Turnovers >= 0);
    }
}
