using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Takımın maç içi durumu. 04_DOMAIN_AND_DATA_MODEL.md'in <c>TeamMatchState</c>
/// kaydının M3'e kadar olan alt kümesi.
///
/// <see cref="Roster"/> kanonik sırada tutulur (bkz. <see cref="RosterOrdering"/>);
/// aday listeleri bu sırayla gezilir, böylece sonuç giriş sırasına bağlı olmaz.
///
/// M3'te eklenenler: <see cref="Fouls"/> (faul sayaçları) ve
/// <see cref="FoulOutPlayerIds"/> (sahadan çıkan oyuncular). Substitution
/// pencereleri ve bench yönetimi M5'in işidir.
/// </summary>
public sealed record TeamMatchState
{
    public required TeamSide Side { get; init; }

    public required Team Team { get; init; }

    /// <summary>Kanonik sıralanmış kadro.</summary>
    public required ImmutableArray<Player> Roster { get; init; }

    /// <summary>Sahadaki beş, lineup slot sırasında.</summary>
    public required ImmutableArray<Player> OnCourt { get; init; }

    /// <summary>M3: faul sayaçları.</summary>
    public required FoulCounters Fouls { get; init; }

    /// <summary>
    /// M3: faulden çıkmış oyuncular. Sahada kalamaz ve yedek seçiminde elenir.
    /// Sıra kararlıdır (çıkış sırası), yalnız üyelik sorgusu yapılır.
    /// </summary>
    public required ImmutableArray<Guid> FoulOutPlayerIds { get; init; }

    public TeamMatchState WithOnCourt(ImmutableArray<Player> onCourt) => this with { OnCourt = onCourt };

    public TeamMatchState WithFouls(FoulCounters fouls) => this with { Fouls = fouls };

    public TeamMatchState MarkFoulOut(Guid playerId) =>
        this with
        {
            FoulOutPlayerIds = FoulOutPlayerIds.Contains(playerId)
                ? FoulOutPlayerIds
                : [.. FoulOutPlayerIds, playerId],
        };
}
