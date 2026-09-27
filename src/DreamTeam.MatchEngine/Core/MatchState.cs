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

    /// <summary>Bir sonraki event sequence'ı. 1'den başlar.</summary>
    public required long NextSequence { get; init; }

    /// <summary>Bir sonraki action kimliği. 1'den başlar, maç boyunca artar.</summary>
    public required long NextActionId { get; init; }

    /// <summary>Bir sonraki shot kimliği. 1'den başlar, maç boyunca artar.</summary>
    public required long NextShotId { get; init; }

    /// <summary>Bir sonraki turnover kimliği. 1'den başlar, maç boyunca artar.</summary>
    public required long NextTurnoverId { get; init; }

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
