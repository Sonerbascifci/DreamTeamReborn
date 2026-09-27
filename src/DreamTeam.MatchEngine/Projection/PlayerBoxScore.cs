using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Projection;

/// <summary>
/// Olaylardan üretilen oyuncu istatistik görünümü. 04_DOMAIN_AND_DATA_MODEL.md:
/// bu tip kalıcı bir tablo değil, event akışının bir projeksiyonudur.
///
/// Serbest atış sayaçları M2'de yoktur: FT kuralı M3'te gelir. <c>Points</c>
/// yalnız 2 ve 3 sayılı isabetlerden gelir ve
/// <c>Points == 2 * TwoPointersMade + 3 * ThreePointersMade</c> değişmez.
/// </summary>
public sealed record PlayerBoxScore
{
    public required Guid PlayerId { get; init; }

    public required string DisplayName { get; init; }

    public required Position Position { get; init; }

    public required TeamSide Team { get; init; }

    public int FieldGoalsMade { get; init; }

    public int FieldGoalsAttempted { get; init; }

    public int TwoPointersMade { get; init; }

    public int TwoPointersAttempted { get; init; }

    public int ThreePointersMade { get; init; }

    public int ThreePointersAttempted { get; init; }

    public int Assists { get; init; }

    public int Turnovers { get; init; }

    /// <summary>M3: kisisel faul sayisi. FGA sayaci uzerinde etkisi yoktur.</summary>
    public int PersonalFouls { get; init; }

    /// <summary>M3: atilan serbest atis.</summary>
    public int FreeThrowAttempts { get; init; }

    /// <summary>M3: isabetli serbest atis.</summary>
    public int FreeThrowMakes { get; init; }

    /// <summary>M3: blok.</summary>
    public int Blocks { get; init; }

    public int OffensiveRebounds { get; init; }

    public int DefensiveRebounds { get; init; }

    /// <summary>Takım ribaundu. Oyuncuya atfedilmez (08 §2).</summary>
    public int TeamRebounds { get; init; }

    public int Points { get; init; }

    public int Rebounds => OffensiveRebounds + DefensiveRebounds;

    /// <summary>Toplam isabet yüzdesi. Sıfır denemede null (08 §6 tanımı).</summary>
    public double? FieldGoalPercentage =>
        FieldGoalsAttempted == 0 ? null : (double)FieldGoalsMade / FieldGoalsAttempted;
}

/// <summary>Takım toplamı. Oyuncu toplamlarıyla birebir tutarlı olmalıdır.</summary>
public sealed record TeamBoxScore
{
    public required TeamSide Team { get; init; }

    public required string TeamName { get; init; }

    public int FieldGoalsMade { get; init; }

    public int FieldGoalsAttempted { get; init; }

    public int TwoPointersMade { get; init; }

    public int TwoPointersAttempted { get; init; }

    public int ThreePointersMade { get; init; }

    public int ThreePointersAttempted { get; init; }

    public int Assists { get; init; }

    public int Turnovers { get; init; }

    /// <summary>M3: kisisel faul sayisi. FGA sayaci uzerinde etkisi yoktur.</summary>
    public int PersonalFouls { get; init; }

    /// <summary>M3: atilan serbest atis.</summary>
    public int FreeThrowAttempts { get; init; }

    /// <summary>M3: isabetli serbest atis.</summary>
    public int FreeThrowMakes { get; init; }

    /// <summary>M3: blok.</summary>
    public int Blocks { get; init; }

    public int OffensiveRebounds { get; init; }

    public int DefensiveRebounds { get; init; }

    public int TeamRebounds { get; init; }

    public int Points { get; init; }

    /// <summary>Bu takımın başlattığı possession sayısı.</summary>
    public int Possessions { get; init; }

    public int Rebounds => OffensiveRebounds + DefensiveRebounds;

    public double? FieldGoalPercentage =>
        FieldGoalsAttempted == 0 ? null : (double)FieldGoalsMade / FieldGoalsAttempted;
}
