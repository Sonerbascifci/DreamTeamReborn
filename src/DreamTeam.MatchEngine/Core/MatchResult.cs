using System.Collections.Immutable;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Projection;

namespace DreamTeam.MatchEngine.Core;

public enum MatchStatus
{
    Completed,
    Aborted,
}

/// <summary>
/// Maçın nihai çıktısı. 04'e göre tamamlanan maç ile iptal/yarım kalan maç ayrılır:
/// <see cref="Status"/> <c>Aborted</c> ise skor geçerli değildir ve <c>IsTie</c>
/// anlamsızdır.
///
/// <see cref="Events"/> tüm maç akışını taşır. Bu, M2 için kabul edilen basitliktir:
/// 08 §8, 100K deneylerde event'lerin bellekte biriktirilmemesini ister; bellek
/// sınırlı özet kipi M6'nın işidir.
/// </summary>
public sealed record MatchResult
{
    public required Guid MatchId { get; init; }

    public required MatchStatus Status { get; init; }

    public required int HomeScore { get; init; }

    public required int AwayScore { get; init; }

    /// <summary>M2'de uzatma yoktur; eşitlikte kazanan seçilmez (06 §8).</summary>
    public required bool IsTie { get; init; }

    public required int HomePossessions { get; init; }

    public required int AwayPossessions { get; init; }

    public required long ElapsedGameTimeMs { get; init; }

    public required int PeriodsPlayed { get; init; }

    public required ImmutableArray<TeamBoxScore> BoxScores { get; init; }

    public required ImmutableArray<PlayerBoxScore> PlayerBoxScores { get; init; }

    public required ImmutableArray<MatchEvent> Events { get; init; }

    /// <summary>Terminal durum değilse null.</summary>
    public string? AbortReason { get; init; }
}
