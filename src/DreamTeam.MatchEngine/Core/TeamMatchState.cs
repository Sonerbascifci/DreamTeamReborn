using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Takımın maç içi durumu. 04_DOMAIN_AND_DATA_MODEL.md'in <c>TeamMatchState</c>
/// kaydının M2'deki alt kümesi: yalnız kadro ve sahadaki beş. Faul, timeout, bench
/// sayacı ve substitution M3/M5'tir.
///
/// <see cref="Roster"/> kanonik sırada tutulur (bkz. <see cref="RosterOrdering"/>);
/// aday listeleri bu sırayla gezilir, böylece sonuç giriş sırasına bağlı olmaz.
/// </summary>
public sealed record TeamMatchState
{
    public required TeamSide Side { get; init; }

    public required Team Team { get; init; }

    /// <summary>Kanonik sıralanmış kadro.</summary>
    public required ImmutableArray<Player> Roster { get; init; }

    /// <summary>Sahadaki beş, lineup slot sırasında.</summary>
    public required ImmutableArray<Player> OnCourt { get; init; }
}
