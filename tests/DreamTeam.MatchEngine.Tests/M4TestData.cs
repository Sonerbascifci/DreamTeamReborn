using System.Collections.Immutable;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M4 testleri icin taktik, tempo ve enerji fixture'lari.
///
/// Buradaki degerler bir kurali "tek basina gozlemlemek" icin secilmistir;
/// DENGE KATSAYISI DEGILDIR. Motorun gercek varsayilanlari
/// <c>EngineConfig.Baseline</c> icinde kalir ve kalibre edilmemistir.
/// </summary>
internal static class M4TestData
{
    /// <summary>Savunma etkilerini tamamen kapatan config.</summary>
    public static EngineConfig NoDefense(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Defense = config.Defense with
            {
                BlockBase = 0.0,
                BlockFromInteriorDefense = 0.0,
                PressureBase = 0.0,
                PressureFromPerimeterDefense = 0.0,
                FoulFromAggression = 0.0,
            },
        };
    }

    /// <summary>Enerji ve yorgunluk etkisini kapatan config.</summary>
    public static EngineConfig NoFatigue(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fatigue = config.Fatigue with
            {
                // Egrisi duz: yorgunluk hicbir sey degistirmez.
                PerformanceCurve = [new EnergyAnchor(100, 1.00), new EnergyAnchor(0, 1.00)],
                BaselineDrainPerSecond = 0.0,
                BaselineRecoveryPerSecond = 0.0,
                BreakRecoveryPerSecond = 0.0,
                FatigueLogitScale = 0.0,
            },
        };
    }

    /// <summary>Sadece kalite farkini gozlemlemek icin: tum shot tabanlari ayni.</summary>
    public static EngineConfig QualityOnly(EngineConfig? source = null)
    {
        var config = NoFatigue(source);

        return config with
        {
            Shot = config.Shot with
            {
                AtRimBase = 0.40,
                ClosePostBase = 0.40,
                MidRangeBase = 0.40,
                ThreePointBase = 0.40,
            },
        };
    }

    /// <summary>Tum tempo degerlerinin ayni oldugu config (izolasyon icin).</summary>
    public static EngineConfig FlatPace(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Pace = config.Pace with
            {
                Tunings =
                [
                    new PaceTuning(Pace.Slow, 1.0, 1.0),
                    new PaceTuning(Pace.Normal, 1.0, 1.0),
                    new PaceTuning(Pace.Fast, 1.0, 1.0),
                ],
            },
        };
    }

    /// <summary>
    /// Duz, hizli hucrem ureten config: kisa sureye bolunmus ama bozulmamis bir
    /// mac. Enerji ve tempo testleri icin.
    /// </summary>
    public static EngineConfig ShortPeriods(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Rules = config.Rules with
            {
                PeriodDurationMs = 3 * 60 * 1000,
                OvertimeDurationMs = 2 * 60 * 1000,
            },
        };
    }

    /// <summary>
    /// D69 ve D69'i izole eden config: faul %100, savunma baskısı ve disiplin sıfır,
    /// yorgunluk kapalı. Böylece sadece faul sayacı değişir.
    /// </summary>
    public static EngineConfig FoulOnly(EngineConfig? source = null)
    {
        var config = NoFatigue(source);

        return config with
        {
            Fouls = config.Fouls with
            {
                FoulProbabilityPerAction = 1.0,
                OffensiveFoulShare = 0.0,
            },
            Defense = config.Defense with
            {
                BlockBase = 0.0,
                BlockFromInteriorDefense = 0.0,
                PressureBase = 0.0,
                PressureFromPerimeterDefense = 0.0,
                FoulFromAggression = 0.0,
            },
        };
    }

    /// <summary>
    /// T16 kombinasyonu: ev <paramref name="home"/>, deplasman
    /// <paramref name="away"/>. Sadece taktik degistirir, tempo sabit kalir.
    /// </summary>
    /// <summary>
    /// Yalniz DEPLASMAN'in savunma attribute'lerini yukseltir. Hucrem taraflari
    /// birebir ayni kalir; boylece savunma baskisinin top kaybi uzerindeki etkisi
    /// karisik bir "daha guclu takim" sinyali olmadan olculur.
    /// </summary>
    public static MatchSetup AwayDefenseBonus(ulong seed, int bonus)
    {
        var setup = M2TestData.NeutralMirror(seed);
        var roster = setup.Away.Team.Roster;

        var rebuilt = ImmutableArray.CreateBuilder<Domain.Players.Player>(roster.Length);

        foreach (var player in roster)
        {
            rebuilt.Add(player with
            {
                Ratings = player.Ratings with
                {
                    PerimeterDefense = player.Ratings.PerimeterDefense + bonus,
                    InteriorDefense = player.Ratings.InteriorDefense + bonus,
                    Steal = player.Ratings.Steal + bonus,
                    Block = player.Ratings.Block + bonus,
                    Rebounding = player.Ratings.Rebounding + bonus,
                },
            });
        }

        return setup with
        {
            Away = setup.Away with
            {
                Team = setup.Away.Team with { Roster = rebuilt.ToImmutable() },
            },
        };
    }

    /// <summary>
    /// Ev <paramref name="home"/>, deplasman <paramref name="away"/>.
    /// Sadece taktik degistirir, tempo sabit kalir.
    /// </summary>
    public static MatchSetup Pair(
        OffensiveTactic home,
        DefensiveTactic homeDefense,
        OffensiveTactic away,
        DefensiveTactic awayDefense,
        ulong seed = 4242) =>
        M2TestData.WithTactics(seed, home, homeDefense, Pace.Normal, away, awayDefense, Pace.Normal);
}
