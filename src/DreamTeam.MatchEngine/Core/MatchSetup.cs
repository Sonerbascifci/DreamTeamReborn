using DreamTeam.Domain.Teams;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Bir maçın değişmez girdi snapshot'ı. Oluşturulduktan sonra alanları
/// değiştirilemez; koleksiyon alanları dışarıdan gelen listelerin kopyasıdır.
///
/// Bu sürümde maç akışı, tactics ve pace bilgisi yoktur; bunlar M2'de
/// <c>TeamMatchSetup</c> ile birlikte eklenir. M1'in kapsamı yalnız kimlik,
/// kadro, lineup ve deterministik kimliktir.
/// </summary>
public sealed record MatchSetup
{
    public required Guid MatchId { get; init; }

    public required Team Home { get; init; }

    public required Team Away { get; init; }

    public required Lineup HomeLineup { get; init; }

    public required Lineup AwayLineup { get; init; }

    public required ulong Seed { get; init; }

    public required EngineIdentity Engine { get; init; }
}
