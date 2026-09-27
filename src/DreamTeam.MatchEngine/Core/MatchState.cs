using System.Collections.Immutable;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Maçın tek otoritesi. Değişmezdir: <c>MatchEngine.Advance</c> yeni bir örnek
/// döner, mevcut olanı değiştirmez. Tek yürütücü kuralı budur.
///
/// Aynı <see cref="MatchState"/> örneğini yalnız <c>MatchEngine</c> değiştirir;
/// iki eşzamanlı ilerletici bu modelde mümkün değildir.
/// </summary>
public sealed record MatchState
{
    public required Config.EngineConfig Config { get; init; }

    public required Core.MatchSetup Setup { get; init; }

    public required MatchClock Clock { get; init; }

    public required MatchPhase Phase { get; init; }

    public required TeamMatchState Home { get; init; }

    public required TeamMatchState Away { get; init; }

    public required int HomeScore { get; init; }

    public required int AwayScore { get; init; }

    /// <summary>Aktif hücum. Dead-ball, periyot arası ve terminal durumlarda null.</summary>
    public required PossessionState? Possession { get; init; }

    /// <summary>
    /// M3: bırakılmış şut. M2'de şut senkron çözülürdü; artık iki adıma bölünür
    /// ve bu alan ara durumu taşır. M5'te serileştirilecektir.
    /// </summary>
    public required PendingShot? PendingShot { get; init; }

    /// <summary>M3: devam eden serbest atış serisi. M5'te serileştirilecektir.</summary>
    public required PendingFreeThrowSeries? PendingFrees { get; init; }

    /// <summary>
    /// M5: uygulanmamış yönetici komutları ve idempotency kaydı.
    ///
    /// <para><b>Neden <c>MatchState</c>'in parçası?</b> Uygulanamayan bir komut
    /// bir sonraki mantıksal sınıra kadar korunmalıdır. Kuyruk yalnız
    /// <c>Advance</c>'in parametresi olsaydı kaybolur ve replay bozulurdu. Bu
    /// alan M5'te serileştirilir (T16).</para>
    ///
    /// <para><b>Komutlar RNG tüketmez</b>; bu alan determinizm sözleşmesini
    /// bozmaz, yalnız hangi komutun ne zaman uygulanacağını belirler.</para>
    /// </summary>
    public required Commands.CommandQueue CommandQueue { get; init; }

    /// <summary>Bir sonraki event sequence'ı. 1'den başlar.</summary>
    public required long NextSequence { get; init; }

    /// <summary>Bir sonraki action kimliği. 1'den başlar, maç boyunca artar.</summary>
    public required long NextActionId { get; init; }

    /// <summary>Bir sonraki shot kimliği. 1'den başlar, maç boyunca artar.</summary>
    public required long NextShotId { get; init; }

    /// <summary>Bir sonraki turnover kimliği. 1'den başlar, maç boyunca artar.</summary>
    public required long NextTurnoverId { get; init; }

    /// <summary>M3: bir sonraki faul kimliği.</summary>
    public required long NextFoulId { get; init; }

    /// <summary>M3: bir sonraki serbest atış seri kimliği.</summary>
    public required long NextFTSeriesId { get; init; }

    /// <summary>Maç boyunca oynanan toplam aksiyon sayısı. Guard sayacı.</summary>
    public required int TotalActionCount { get; init; }

    /// <summary>Başlatılmış toplam possession sayısı.</summary>
    public required int PossessionCount { get; init; }

    /// <summary>Devam için RNG state'i. Seed değil, mevcut durumdur (05 §14).</summary>
    public required IRandomSource Random { get; init; }

    public TeamMatchState Team(TeamSide side) => side == TeamSide.Home ? Home : Away;

    public int Score(TeamSide side) => side == TeamSide.Home ? HomeScore : AwayScore;

    public bool IsTerminal => Phase is MatchPhase.Completed or MatchPhase.Aborted;

    /// <summary>Canlı oyun süresi tükettiğinde saati ilerleten kısa yol.</summary>
    public MatchState Consume(long liveMs) => this with { Clock = Clock.ConsumeLiveTime(liveMs) };
}
