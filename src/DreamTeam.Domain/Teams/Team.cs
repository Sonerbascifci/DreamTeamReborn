using System.Collections.Immutable;
using DreamTeam.Domain.Players;

namespace DreamTeam.Domain.Teams;

/// <summary>
/// Bir takım ve oyuncu kadrosu. <see cref="Roster"/> dışarıdan gelen bir koleksiyonun
/// kopyasıdır; kaynak koleksiyon sonradan değiştirilirse bu snapshot etkilenmez.
/// </summary>
public sealed record Team
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required ImmutableArray<Player> Roster { get; init; }
}
