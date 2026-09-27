using System.Collections.Immutable;

namespace DreamTeam.Domain.Teams;

/// <summary>
/// Sahadaki ilk beş oyuncunun kimlikleri. Sıra, slot sırasını ifade eder ve
/// determinizm için anlamlıdır; bu nedenle koleksiyon dışarıdan değiştirilemez.
/// </summary>
public sealed record Lineup
{
    public required ImmutableArray<Guid> PlayerIds { get; init; }
}
