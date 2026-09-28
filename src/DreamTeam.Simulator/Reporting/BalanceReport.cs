using System.Globalization;
using System.Text;
using DreamTeam.Simulator.Batch;

namespace DreamTeam.Simulator.Reporting;

/// <summary>
/// M6: the balance report. 08 §6's metrics, derived ONCE from summed raw counts.
///
/// <para><b>No NBA comparison appears here, by decision (D98c).</b> The engine has
/// no licensed season reference and 08 §7 forbids presenting an unselected season
/// average as a target. The thresholds are therefore internal-consistency checks:
/// is the engine symmetric, are the values finite, is any tactic dominant, does
/// strength pay off, does tempo order, does swapping sides change nothing.</para>
///
/// <para><b>A failed threshold is a FINDING, not a failure.</b> The report says
/// which check tripped and by how much; it does not claim the engine is broken and
/// it does not claim the engine is fine.</para>
/// </summary>
public sealed record BalanceReport
{
    public required string FixtureName { get; init; }

    public required long MatchCount { get; init; }

    public required string ConfigHash { get; init; }

    public required BalanceMetrics Home { get; init; }

    public required BalanceMetrics Away { get; init; }

    /// <summary>
    /// M6: 08 §6 "shot type dağılımı". 05 §7 her şut türü için ayrı bir hedef
    /// aralığı verir; toplu 2P/3P ortalaması bu aralıklara denetlenemez.
    /// </summary>
    public required ShotTypeTally HomeShots { get; init; }

    public required ShotTypeTally AwayShots { get; init; }

    public required ProportionEstimate HomeWinRate { get; init; }

    public required ProportionEstimate AbortRate { get; init; }

    public required ProportionEstimate OvertimeRate { get; init; }

    public required ProportionEstimate TieRate { get; init; }

    public required double AverageHomeScore { get; init; }

    public required double AverageAwayScore { get; init; }

    public required double ScoreStandardDeviation { get; init; }

    public required double AveragePossessionsPerTeam { get; init; }

    /// <summary>Both teams together, per 48 minutes of game time. Overtime normalised.</summary>
    public required double Pace48 { get; init; }

    public required double TotalPossessions { get; init; }

    public required double AveragePeriods { get; init; }

    public required IReadOnlyList<ThresholdResult> Thresholds { get; init; }

    public required IReadOnlyDictionary<string, int> AbortReasons { get; init; }

    public required string DiagnosticsReport { get; init; }

    public required IReadOnlyList<PlayerEnergyRow> PlayerEnergy { get; init; }

    /// <summary>True only when every threshold passed. Read it as a summary, not a verdict.</summary>
    public bool AllThresholdsPassed => Thresholds.All(result => result.Passed);

    public string ToText()
    {
        var builder = new StringBuilder(4096);

        builder.Append("=== DENGE RAPORU (M6) ===\n");
        builder.Append("Fixture      : ").Append(FixtureName).Append('\n');
        builder.Append("ConfigHash   : ").Append(ConfigHash).Append('\n');
        builder.Append("Mac sayisi   : ").Append(MatchCount.ToString("N0", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("Eşik tabanı  : iç tutarlılık. NBA sezonuyla karşılaştırma YAPILMAZ (D98c).\n");
        builder.Append('\n');

        builder.Append("-- Sonuc --\n");
        builder.Append(HomeWinRate.ToReport("Ev kazanma orani")).Append('\n');
        builder.Append(AbortRate.ToReport("Aborted orani")).Append('\n');
        builder.Append(OvertimeRate.ToReport("Uzatma orani")).Append('\n');
        builder.Append(TieRate.ToReport("Beraberlik orani")).Append('\n');
        builder.Append("Ortalama skor  : Ev ").Append(Score(AverageHomeScore))
            .Append(" / Dep ").Append(Score(AverageAwayScore)).Append('\n');
        builder.Append("Fark StdSap   : ")
            .Append(ScoreStandardDeviation.ToString("F2", CultureInfo.InvariantCulture))
            .Append("   (ev-dep farki; toplam ve toplam karelerinden hesaplanir)")
            .Append('\n');
        builder.Append("Ortalama periyot: ").Append(AveragePeriods.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append('\n');

        builder.Append("-- Toplamlar (mac basina ortalama degil) --\n");
        builder.Append("Toplam possession : ").Append(Number(TotalPossessions)).Append('\n');
        builder.Append("Takim basina poss: ").Append(AveragePossessionsPerTeam.ToString("F1", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("Pace48           : ").Append(Pace48.ToString("F1", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append('\n');

        builder.Append("Takim      ").Append(Pad("Ev")).Append(Pad("Dep")).Append('\n');
        AppendRow(builder, "PTS", Home.PointsPerMatch, Away.PointsPerMatch);
        AppendRow(builder, "FG", Home.FieldGoalRate, Away.FieldGoalRate);
        AppendRow(builder, "2P oran", Home.TwoPointRate, Away.TwoPointRate);
        AppendRow(builder, "3P oran", Home.ThreePointRate, Away.ThreePointRate);
        AppendRow(builder, "3PA payi", Home.ThreeAttemptShare, Away.ThreeAttemptShare);
        AppendRow(builder, "FT oran", Home.FreeThrowRate, Away.FreeThrowRate);
        AppendRow(builder, "FT/poss", Home.FreeThrowsPerPossession, Away.FreeThrowsPerPossession);
        AppendRow(builder, "TOV/poss", Home.TurnoversPerPossession, Away.TurnoversPerPossession);
        AppendRow(builder, "FAUL/poss", Home.FoulsPerPossession, Away.FoulsPerPossession);
        AppendRow(builder, "OREB%", Home.OffensiveReboundRate, Away.OffensiveReboundRate);
        AppendRow(builder, "AST/poss", Home.AssistsPerPossession, Away.AssistsPerPossession);
        AppendRow(builder, "ORtg", Home.OffensiveRating, Away.OffensiveRating);
        builder.Append('\n');

        builder.Append("-- Sut turu kirilimi (05 S7 hedef araliklari; NBA verisi DEGIL) --\n");
        builder.Append("Tur           ").Append(Pad("Ev oran")).Append(Pad("Dep oran")).Append("Hedef (05 S7)").Append('\n');

        foreach (var row in HomeShots.Rows())
        {
            var home = row.Rate;
            var away = AwayShots.Rows().First(candidate => candidate.Name == row.Name).Rate;
            var (low, high) = SpecRange(row.Name);

            builder.Append(Pad(row.Name))
                .Append(Percent(home))
                .Append(Pad(Percent(away)))
                .Append((low * 100).ToString("F0", CultureInfo.InvariantCulture))
                .Append('-')
                .Append((high * 100).ToString("F0", CultureInfo.InvariantCulture))
                .Append('%')
                .Append("    ")
                .Append(home is null ? "n/a" : Within(home.Value, low, high) ? "GECTI" : "KALDI")
                .Append('\n');
        }

        builder.Append('\n');
        builder.Append("-- Ic tutarlilik esikleri (D98c) --\n");

        foreach (var result in Thresholds)
        {
            builder.Append(result.ToText()).Append('\n');
        }

        builder.Append('\n');

        if (AbortReasons.Count > 0)
        {
            builder.Append("-- Abort sebepleri (gizlenmez) --\n");

            foreach (var reason in AbortReasons.OrderByDescending(pair => pair.Value))
            {
                builder.Append("  ").Append(reason.Value.ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" x ").Append(reason.Key).Append('\n');
            }

            builder.Append('\n');
        }

        builder.Append("-- Diagnostics (D100) --\n");
        builder.Append(DiagnosticsReport);
        builder.Append('\n');

        builder.Append("-- Oyuncu enerji/dakika (ilk 6) --\n");

        foreach (var row in PlayerEnergy.Take(6))
        {
            builder.Append("  ").Append(row.Team).Append(' ')
                .Append(Pad(row.DisplayName))
                .Append("ort enerji ").Append(row.AverageEnergy.ToString("F1", CultureInfo.InvariantCulture))
                .Append("  ort dk ").Append(row.AverageMinutes.ToString("F2", CultureInfo.InvariantCulture))
                .Append("  min ").Append(row.MinEnergy.ToString(CultureInfo.InvariantCulture))
                .Append("  max ").Append(row.MaxEnergy.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, string label, double home, double away) =>
        builder.Append(Pad(label))
            .Append(Show(home))
            .Append("    ")
            .Append(Show(away))
            .Append('\n');

    /// <summary>
    /// A metric with no samples prints as "n/a", never as 0 and never as NaN. This
    /// is the same rule 08 §6 states, applied at the point of output.
    /// </summary>
    private static void AppendRow(StringBuilder builder, string label, double? home, double? away) =>
        builder.Append(Pad(label))
            .Append(home is null ? "n/a" : home.Value.ToString("F3", CultureInfo.InvariantCulture))
            .Append("    ")
            .Append(away is null ? "n/a" : away.Value.ToString("F3", CultureInfo.InvariantCulture))
            .Append('\n');

    private static string Show(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string Percent(double? value) =>
        value is null
            ? "n/a"
            : (value.Value * 100).ToString("F2", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// 05 §7's own starting targets. The document says explicitly: "NBA ortalaması
    /// iddiası olmadan" — without claiming NBA averages. So these are OUR spec's
    /// ranges, which is a legitimate calibration target under D98c, and are
    /// reported as such rather than as a league average.
    /// </summary>
    private static (double Low, double High) SpecRange(string shotType) => shotType switch
    {
        "AtRim" => (0.60, 0.68),
        "ClosePost" => (0.48, 0.58),
        "MidRange" => (0.38, 0.45),
        "ThreePoint" => (0.33, 0.39),
        _ => (0.0, 1.0),
    };

    private static bool Within(double rate, double low, double high) => rate >= low && rate <= high;

    private static string Pad(string value) => value.Length >= 11 ? value + " " : value.PadRight(11);

    private static string Number(double value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Score(double value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);
}

/// <summary>One team's derived metrics for a batch.</summary>
public sealed record BalanceMetrics
{
    public required double Matches { get; init; }

    public required double PointsPerMatch { get; init; }

    /// <summary>FGM / FGA over the whole batch. Zero denominator yields null, not Infinity.</summary>
    public required double? FieldGoalRate { get; init; }

    public required double? TwoPointRate { get; init; }

    public required double? ThreePointRate { get; init; }

    public required double? ThreeAttemptShare { get; init; }

    public required double? FreeThrowRate { get; init; }

    public required double FreeThrowsPerPossession { get; init; }

    public required double TurnoversPerPossession { get; init; }

    public required double FoulsPerPossession { get; init; }

    public required double AssistsPerPossession { get; init; }

    /// <summary>OREB / (OREB + opponent DREB), per 08 §6.</summary>
    public required double OffensiveReboundRate { get; init; }

    /// <summary>100 * points / possessions, using the defined possession count.</summary>
    public required double OffensiveRating { get; init; }
}

public readonly record struct PlayerEnergyRow(
    string Team,
    string DisplayName,
    double AverageEnergy,
    double AverageMinutes,
    int MinEnergy,
    int MaxEnergy);

/// <summary>
/// One internal-consistency check (D98c). <see cref="Detail"/> always states the
/// measured value so a reader never has to take the verdict on trust.
/// </summary>
public readonly record struct ThresholdResult(
    int Number,
    string Name,
    bool Passed,
    string Detail)
{
    public string ToText() =>
        $"  [{(Passed ? "GECTI" : "KALDI")}] #{Number} {Name,-34} {Detail}";
}
