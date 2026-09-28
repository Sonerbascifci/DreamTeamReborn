using System.Collections.Immutable;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Simulator.Batch;

/// <summary>
/// M6: one match, summarised. <b>This is the type a 100K run produces;
/// <c>MatchResult</c> is never built.</b>
/// </summary>
///
/// <para><b>Why not <c>MatchResult</c>.</b> Measured, not guessed: a single match
/// emits 1107 events on average, so 100K matches would retain 110.7 million
/// events, roughly 21 GB. 08 §119 says the events must not have to accumulate.
/// A summary that holds only counts is O(1) per match.</para>
///
/// <para><b>Counts, not ratios.</b> 08 §6: "Maç yüzdelerinin basit ortalamasıyla
/// karıştırma." A per-match percentage would have to be averaged across matches,
/// which is exactly the wrong operation. Raw counts sum exactly; ratios are
/// derived once, at the end, by <see cref="SummaryAccumulator"/> and
/// <c>BalanceReport</c>.</para>
/// </summary>
public sealed record MatchSummary
{
    public required Guid MatchId { get; init; }

    /// <summary>Seed the match was played with. Recorded per match, not per batch.</summary>
    public required ulong Seed { get; init; }

    public required MatchStatus Status { get; init; }

    public required int HomeScore { get; init; }

    public required int AwayScore { get; init; }

    public required bool IsTie { get; init; }

    public required int HomePossessions { get; init; }

    public required int AwayPossessions { get; init; }

    public required long ElapsedGameTimeMs { get; init; }

    public required int PeriodsPlayed { get; init; }

    public required int TotalActions { get; init; }

    public required TeamTotals Home { get; init; }

    public required TeamTotals Away { get; init; }

    /// <summary>
    /// M6: per-shot-type attempts and makes, per side.
    ///
    /// <para><b>Neden ayrı?</b> 08 &#167;6 "shot type da&#287;&#305;l&#305;m&#305;" istiyor
    /// ve 05 &#167;7 her &#351;ut t&#252;r&#252; i&#231;in ayr&#305; bir hedef aral&#305;&#287;&#305; veriyor
    /// (AtRim %60-68, ClosePost %48-58, MidRange %38-45, ThreePoint %33-39).
    /// Toplu 2P/3P ortalamas&#305; bu aral&#305;klara denetlenemez: 2P, AtRim +
    /// ClosePost + MidRange'&#305;n kar&#305;&#351;&#305;m&#305;d&#305;r. Kalibrasyon ancak
    /// t&#252;r k&#305;r&#305;l&#305;m&#305;yla yap&#305;labilir.</para>
    ///
    /// <para><b>Attempt ve make ayr&#305;.</b> Ka&#231;&#305;an shooting foul'da
    /// FGM yaz&#305;lmaz ama <c>ShotMissed</c> event'i y&#287;ilir; bu y&#252;zden
    /// isabet = deneme - ka&#231;&#305;ran, isabet oran&#305; de&#287;il.</para>
    /// </summary>
    public required ShotTypeTally HomeShots { get; init; }

    public required ShotTypeTally AwayShots { get; init; }

    /// <summary>Diagnostics counters, copied straight from the final state.</summary>
    public required DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters Diagnostics { get; init; }

    /// <summary>Final energy per player. Roster size is fixed, so this is bounded.</summary>
    public ImmutableArray<PlayerEnergySample> PlayerEnergy { get; init; } = [];

    public string? AbortReason { get; init; }

    /// <summary>Events produced. Retained as a number only, never as a list.</summary>
    public int EventCount { get; init; }

    public bool Completed => Status == MatchStatus.Completed;
}

/// <summary>Flat, summable team counts for one match.</summary>
public sealed record TeamTotals
{
    public required int Points { get; init; }

    public required int FieldGoalsMade { get; init; }

    public required int FieldGoalsAttempted { get; init; }

    public required int TwoPointersMade { get; init; }

    public required int TwoPointersAttempted { get; init; }

    public required int ThreePointersMade { get; init; }

    public required int ThreePointersAttempted { get; init; }

    public required int FreeThrowMakes { get; init; }

    public required int FreeThrowAttempts { get; init; }

    public required int Assists { get; init; }

    public required int Turnovers { get; init; }

    public required int PersonalFouls { get; init; }

    public required int Blocks { get; init; }

    public required int OffensiveRebounds { get; init; }

    public required int DefensiveRebounds { get; init; }

    public required int TeamRebounds { get; init; }

    public required int Possessions { get; init; }

    public static TeamTotals From(DreamTeam.MatchEngine.Projection.TeamBoxScore box, int possessions) => new()
    {
        Points = box.Points,
        FieldGoalsMade = box.FieldGoalsMade,
        FieldGoalsAttempted = box.FieldGoalsAttempted,
        TwoPointersMade = box.TwoPointersMade,
        TwoPointersAttempted = box.TwoPointersAttempted,
        ThreePointersMade = box.ThreePointersMade,
        ThreePointersAttempted = box.ThreePointersAttempted,
        FreeThrowMakes = box.FreeThrowMakes,
        FreeThrowAttempts = box.FreeThrowAttempts,
        Assists = box.Assists,
        Turnovers = box.Turnovers,
        PersonalFouls = box.PersonalFouls,
        Blocks = box.Blocks,
        OffensiveRebounds = box.OffensiveRebounds,
        DefensiveRebounds = box.DefensiveRebounds,
        TeamRebounds = box.TeamRebounds,
        Possessions = possessions,
    };

    public static TeamTotals Zero { get; } = new()    {
        Points = 0,
        FieldGoalsMade = 0,
        FieldGoalsAttempted = 0,
        TwoPointersMade = 0,
        TwoPointersAttempted = 0,
        ThreePointersMade = 0,
        ThreePointersAttempted = 0,
        FreeThrowMakes = 0,
        FreeThrowAttempts = 0,
        Assists = 0,
        Turnovers = 0,
        PersonalFouls = 0,
        Blocks = 0,
        OffensiveRebounds = 0,
        DefensiveRebounds = 0,
        TeamRebounds = 0,
        Possessions = 0,
    };
}

/// <summary>
/// One player's final energy and minutes. Kept per match so that 08 §6's
/// "oyuncu dakika/enerji dağılımı" can be aggregated, and so that a low-bench
/// edge case is visible rather than averaged away.
/// </summary>
/// <summary>Final energy per player. Roster size is fixed, so this is bounded.</summary>
public readonly record struct PlayerEnergySample(
    Guid PlayerId,
    string DisplayName,
    TeamSide Team,
    int Energy,
    double SecondsOnCourt);

/// <summary>
/// Attempts and makes for one shot type, summed over a side.
///
/// <para>Five mutable counters rather than a dictionary: a dictionary would make
/// output order depend on insertion, and 08 &#167;4 forbids relying on an
/// undefined order.</para>
/// </summary>
public sealed record ShotTypeTally
{
    public int AtRimAttempts { get; set; }

    public int AtRimMisses { get; set; }

    public int ClosePostAttempts { get; set; }

    public int ClosePostMisses { get; set; }

    public int MidRangeAttempts { get; set; }

    public int MidRangeMisses { get; set; }

    public int ThreePointAttempts { get; set; }

    public int ThreePointMisses { get; set; }

    public int Attempts => AtRimAttempts + ClosePostAttempts + MidRangeAttempts + ThreePointAttempts;

    public int Makes => Attempts - (AtRimMisses + ClosePostMisses + MidRangeMisses + ThreePointMisses);

    /// <summary>
    /// YENI bir örnek, her seferinde.
    ///
    /// <para><b>Neden singleton DEGIL?</b> İlk sürümde `static ShotTypeTally Empty
    /// { get; } = new();` idi ve <c>MatchRunner</c> bunu iki yerel değişkenin
    /// başlangıcı olarak kullanıyordu. Sayaçlar <c>ref</c> ile MUTASYONA uğradığı
    /// için iki yerel degisken <b>ayni nesneyi</b> gosteriyor, ev ve depo toplamlari tek yere karisiyor ve <c>Empty</c> tum surec boyunca birikiyordu.
    /// 2.000 maçta oranlar henüz yakınsadığı için hata GÖRÜNMEDİ; 10.000 maçta
    /// ClosePost %-67, MidRange %+70 gibi imkânsız değerler verdi. Aynı hata
    /// sınıfının bir daha sızmaması için <c>Empty</c> artık değer üretir.</para>
    /// </summary>
    public static ShotTypeTally Empty => new();

    public static ShotTypeTally Add(ShotTypeTally left, ShotTypeTally right) => new()
    {
        AtRimAttempts = left.AtRimAttempts + right.AtRimAttempts,
        AtRimMisses = left.AtRimMisses + right.AtRimMisses,
        ClosePostAttempts = left.ClosePostAttempts + right.ClosePostAttempts,
        ClosePostMisses = left.ClosePostMisses + right.ClosePostMisses,
        MidRangeAttempts = left.MidRangeAttempts + right.MidRangeAttempts,
        MidRangeMisses = left.MidRangeMisses + right.MidRangeMisses,
        ThreePointAttempts = left.ThreePointAttempts + right.ThreePointAttempts,
        ThreePointMisses = left.ThreePointMisses + right.ThreePointMisses,
    };

    /// <summary>Row for the report, in fixed order. Null when there is no sample.</summary>
    public IEnumerable<(string Name, long Attempts, long Misses, double? Rate)> Rows() =>
    [
        ("AtRim", AtRimAttempts, AtRimMisses, Rate(AtRimAttempts - AtRimMisses, AtRimAttempts)),
        ("ClosePost", ClosePostAttempts, ClosePostMisses, Rate(ClosePostAttempts - ClosePostMisses, ClosePostAttempts)),
        ("MidRange", MidRangeAttempts, MidRangeMisses, Rate(MidRangeAttempts - MidRangeMisses, MidRangeAttempts)),
        ("ThreePoint", ThreePointAttempts, ThreePointMisses, Rate(ThreePointAttempts - ThreePointMisses, ThreePointAttempts)),
    ];

    private static double? Rate(long makes, long attempts) =>
        attempts == 0 ? null : (double)makes / attempts;
}
