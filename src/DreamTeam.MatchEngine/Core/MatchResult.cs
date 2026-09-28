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

    /// <summary>
    /// M4: maç sonu oyuncu enerji ve sahada kalma süresi (08 §88 "oyuncu
    /// dakika/enerji dağılımı"). T12c bunun toplamını doğrular: 5 oyuncu × geçen
    /// süre. Aborted maçlarda boştur — skor gibi yarım kalan maçta anlamsızdır.
    /// </summary>
    public ImmutableArray<PlayerEnergyReport> PlayerEnergy { get; init; } = [];

    /// <summary>
    /// M4: takım OVR'leri. <b>Gösterim amaçlıdır, çözüm girdisi değildir</b>
    /// (D23/D24, T03). Motor hiçbir yerde bu değerleri okumaz.
    /// </summary>
    public int? HomeOverall { get; init; }

    public int? AwayOverall { get; init; }

    // ------------------------------------------------ M5: timeout özeti (D84)

    /// <summary>M5 (D84): kullanılmış tam timeout sayısı.</summary>
    public int HomeTimeoutsUsed { get; init; }

    /// <summary>M5 (D84): kullanılmış tam timeout sayısı.</summary>
    public int AwayTimeoutsUsed { get; init; }

    /// <summary>M5 (D83, D89): kullanılmış 20 saniyelik timeout sayısı.</summary>
    public int HomeShortTimeoutsUsed { get; init; }

    /// <summary>M5 (D83, D89): kullanılmış 20 saniyelik timeout sayısı.</summary>
    public int AwayShortTimeoutsUsed { get; init; }

    /// <summary>M5 (D84): uzatma bonusu dâhil tam timeout bütçesi.</summary>
    public int HomeTimeoutBudget { get; init; }

    /// <summary>M5 (D84): uzatma bonusu dâhil tam timeout bütçesi.</summary>
    public int AwayTimeoutBudget { get; init; }

    /// <summary>M5 (D89): 20 saniyelik timeout bütçesi.</summary>
    public int HomeShortTimeoutBudget { get; init; }

    /// <summary>M5 (D89): 20 saniyelik timeout bütçesi.</summary>
    public int AwayShortTimeoutBudget { get; init; }

    /// <summary>Terminal durum değilse null.</summary>
    public string? AbortReason { get; init; }

    // ------------------------------------------------ M6: diagnostics (D100)

    /// <summary>
    /// M6 (D100): kural sayaçları. <b>Rapor içindir; motor hiçbir yerde
    /// okumaz.</b> M6 denge raporunun "hangi kural kaç kez tetiklendi"
    /// bölümü buradan gelir.
    /// </summary>
    public DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters Diagnostics { get; init; } =
        DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters.Empty;
}

/// <param name="PlayerId">Oyuncu kimliği.</param>
/// <param name="DisplayName">Rapor çıktısı için.</param>
/// <param name="Team">Taraf.</param>
/// <param name="Energy">Maç sonu kalan enerji, 0-100 (görüntüleme için yuvarlanmış).</param>
/// <param name="SecondsOnCourt">Sahada geçen canlı süre, saniye (kesirli).</param>
public readonly record struct PlayerEnergyReport(
    Guid PlayerId,
    string DisplayName,
    TeamSide Team,
    int Energy,
    double SecondsOnCourt);
