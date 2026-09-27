using System.Collections.Immutable;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M3 testleri icin ozel senaryo konfigurasyonlari.
///
/// Buradaki degerler bir "belirli bir kurali tek basina gozlemlemek" icin
/// secilmistir; DENGE KATSAYISI DEGILDIR. Motorun gercek varsayilanlari
/// <c>EngineConfig.Baseline</c> icinde kalir ve kalibre edilmemistir.
/// </summary>
internal static class M3TestData
{
    public static EngineConfig OnlyFreeThrows(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Actions = config.Actions with
            {
                ShotCompletionProbability = 0.0,
                TurnoverProbability = 0.0,
                SetupActionMs = 1_000,
            },
            Fouls = config.Fouls with
            {
                FoulProbabilityPerAction = 1.0,
                OffensiveFoulShare = 0.0,
            },
        };
    }

    public static EngineConfig AlwaysFirstFreeThrowMade(EngineConfig? source = null)
    {
        var config = OnlyFreeThrows(source);

        return config with
        {
            FreeThrows = config.FreeThrows with { BaseMakeProbability = 1.0 },
        };
    }

    /// <summary>Her aksiyon bir shooting faul + isabet: yalnizca and-one serileri.</summary>
    public static EngineConfig AndOneOnly(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Actions = config.Actions with
            {
                ShotCompletionProbability = 1.0,
                TurnoverProbability = 0.0,
            },
            Fouls = config.Fouls with
            {
                FoulProbabilityPerAction = 1.0,
                OffensiveFoulShare = 0.0,
                ShootingFoulShare = 1.0,
            },
            Shot = config.Shot with
            {
                BlockProbability = 0.0,
                AtRimBase = 0.999,
                ClosePostBase = 0.999,
                MidRangeBase = 0.999,
                ThreePointBase = 0.999,
                SkillScale = 0.0,
            },
        };
    }

    /// <summary>Her aksiyon bir shooting faul + kacan sut: yalnizca 2 veya 3 FT serileri.</summary>
    public static EngineConfig MissedShootingFoulOnly(EngineConfig? source = null)
    {
        var config = AndOneOnly(source);

        return config with
        {
            Shot = config.Shot with
            {
                AtRimBase = 0.001,
                ClosePostBase = 0.001,
                MidRangeBase = 0.001,
                ThreePointBase = 0.001,
            },
        };
    }

    public static EngineConfig NoFouls(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
        };
    }

    public static EngineConfig AllBlocks(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
            Shot = config.Shot with { BlockProbability = 1.0 },
        };
    }

    public static EngineConfig AlwaysRimContact(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
            Shot = config.Shot with { RimContactProbability = 1.0 },
        };
    }

    public static EngineConfig NeverRimContact(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
            Shot = config.Shot with { RimContactProbability = 0.0 },
        };
    }

    public static EngineConfig AlwaysOffensiveRebound(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
            Actions = config.Actions with { OffensiveReboundProbability = 1.0 },
        };
    }

    public static EngineConfig AlwaysDefensiveRebound(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
            Actions = config.Actions with { OffensiveReboundProbability = 0.0 },
        };
    }

    /// <summary>
    /// Dusuk skorlu profil: esitlik ve uzatma yolu orneklenebilir olur. Bu bir
    /// denge ayari degil, yalnizca bir kurali gozlemleyebilmek icin fixture.
    /// </summary>
    public static EngineConfig LowScoring(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Shot = config.Shot with
            {
                AtRimBase = 0.28,
                ClosePostBase = 0.24,
                MidRangeBase = 0.20,
                ThreePointBase = 0.18,
                BlockProbability = 0.0,
            },
        };
    }

    /// <summary>
    /// Kadroda yedek olmayan mac: foul-out sonrasi "yasal yedek yok" terminal
    /// yolunu (D43) izole eder.
    /// </summary>
    public static MatchSetup NoLegalSubstitute(ulong seed = 42)
    {
        var setup = M2TestData.NeutralMirror(seed);

        return setup with
        {
            Home = setup.Home with { Roster = [.. setup.Home.Roster.Take(5)] },
            Away = setup.Away with { Roster = [.. setup.Away.Roster.Take(5)] },
        };
    }

    public static ImmutableArray<Domain.Players.Player> RosterOf(MatchSetup setup, TeamSide side) =>
        side == TeamSide.Home ? setup.Home.Roster : setup.Away.Roster;
}
