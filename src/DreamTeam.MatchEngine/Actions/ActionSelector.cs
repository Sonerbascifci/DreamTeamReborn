using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Actions;

/// <summary>Seçilen aksiyonun oyuncuya bağlanmış hâli.</summary>
public sealed record ActionSelection
{
    public required OffensiveAction Action { get; init; }

    public required ShotType ShotType { get; init; }

    /// <summary>Aksiyonu yürüten oyuncu.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>Bu oyuncu için okunan birincil beceri attribute'ü (0-100).</summary>
    public required int SkillRating { get; init; }

    public required ActionProfile Profile { get; init; }

    /// <summary>M4: dağılımı belirleyen taktik. Normalizasyon izlenebilirliği için taşınır.</summary>
    public required OffensiveTactic Tactic { get; init; }
}
