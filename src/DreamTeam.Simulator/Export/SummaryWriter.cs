using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator.Reporting;

namespace DreamTeam.Simulator.Export;

/// <summary>
/// M6: writes the three artefacts a batch produces. Zero packages:
/// <c>System.Text.Json</c> needs no NuGet reference (M5 D88, measured).
///
/// <para><b>What goes to disk.</b> The balance report (JSON), the same numbers as
/// a flat CSV, and the determinism manifest (JSON). Per-match event streams are NOT
/// written for a 100K run: that is the 21 GB problem. <c>--events</c> writes them
/// for a single-match run only.</para>
///
/// <para><b>Consistency is the point.</b> T18 asks that the CSV and JSON agree and
/// that both match the event-derived totals. The two writers read the same
/// <see cref="BalanceReport"/>, so a divergence can only be a formatting bug, and
/// <c>CsvAndJsonSummariesAgree</c> tests for exactly that.</para>
/// </summary>
public static class SummaryWriter
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
    };

    static SummaryWriter()
    {
        // Enum-as-name, same reason as the balance document (M5 D88).
        Json.Converters.Add(new JsonStringEnumConverter());
    }

    public static void WriteAll(
        string outputDirectory,
        BalanceReport report,
        ExperimentManifest manifest,
        string baseName = "summary")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(manifest);

        Directory.CreateDirectory(outputDirectory);

        File.WriteAllText(
            Path.Combine(outputDirectory, $"{baseName}.json"),
            JsonSerializer.Serialize(report, Json) + "\n");

        File.WriteAllText(
            Path.Combine(outputDirectory, $"{baseName}.csv"),
            ToCsv(report));

        File.WriteAllText(
            Path.Combine(outputDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, Json) + "\n");

        File.WriteAllText(
            Path.Combine(outputDirectory, "manifest.txt"),
            manifest.ToText());

        File.WriteAllText(
            Path.Combine(outputDirectory, "report.txt"),
            report.ToText());
    }

    /// <summary>
    /// Flat key/value CSV. Long format (one metric per row) rather than wide, so a
    /// spreadsheet can pivot it and so a new metric never shifts a column.
    /// </summary>
    public static string ToCsv(BalanceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder(2048);

        builder.Append("section,metric,value\n");

        Row(builder, "run", "fixture", report.FixtureName);
        Row(builder, "run", "config_hash", report.ConfigHash);
        Row(builder, "run", "match_count", Num(report.MatchCount));
        Row(builder, "run", "compared_against_real_season_data", "false");

        Row(builder, "result", "home_win_rate", Pct(report.HomeWinRate.Point));
        Row(builder, "result", "home_win_rate_ci95_low", Pct(report.HomeWinRate.Lower));
        Row(builder, "result", "home_win_rate_ci95_high", Pct(report.HomeWinRate.Upper));
        Row(builder, "result", "abort_rate", Pct(report.AbortRate.Point));
        Row(builder, "result", "overtime_rate", Pct(report.OvertimeRate.Point));
        Row(builder, "result", "tie_rate", Pct(report.TieRate.Point));
        Row(builder, "result", "avg_home_score", F(report.AverageHomeScore));
        Row(builder, "result", "avg_away_score", F(report.AverageAwayScore));
        Row(builder, "result", "avg_periods", F(report.AveragePeriods));
        Row(builder, "result", "pace48", F(report.Pace48));
        Row(builder, "result", "total_possessions", Num(report.TotalPossessions));
        Row(builder, "result", "avg_possessions_per_team", F(report.AveragePossessionsPerTeam));

        AppendSide(builder, "home", report.Home);
        AppendSide(builder, "away", report.Away);

        foreach (var threshold in report.Thresholds)
        {
            Row(builder, "threshold", $"#{threshold.Number} {threshold.Name}", threshold.Passed ? "PASS" : "FAIL");
            Row(builder, "threshold_detail", $"#{threshold.Number}", threshold.Detail);
        }

        foreach (var reason in report.AbortReasons.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Row(builder, "abort_reason", reason.Key, Num(reason.Value));
        }

        foreach (var line in report.DiagnosticsReport.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = line.Split('=');

            if (split.Length == 2)
            {
                Row(builder, "diagnostic", split[0], split[1]);
            }
        }

        foreach (var player in report.PlayerEnergy)
        {
            Row(builder, "player_energy", $"{player.Team} {player.DisplayName}",
                $"{F(player.AverageEnergy)}|{F(player.AverageMinutes)}|{player.MinEnergy}|{player.MaxEnergy}");
        }

        return builder.ToString();
    }

    private static void AppendSide(StringBuilder builder, string side, BalanceMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        Row(builder, side, "points_per_match", F(metrics.PointsPerMatch));
        Row(builder, side, "fg_rate", Nullable(metrics.FieldGoalRate));
        Row(builder, side, "two_point_rate", Nullable(metrics.TwoPointRate));
        Row(builder, side, "three_point_rate", Nullable(metrics.ThreePointRate));
        Row(builder, side, "three_attempt_share", Nullable(metrics.ThreeAttemptShare));
        Row(builder, side, "ft_rate", Nullable(metrics.FreeThrowRate));
        Row(builder, side, "ft_per_possession", F(metrics.FreeThrowsPerPossession));
        Row(builder, side, "tov_per_possession", F(metrics.TurnoversPerPossession));
        Row(builder, side, "fouls_per_possession", F(metrics.FoulsPerPossession));
        Row(builder, side, "ast_per_possession", F(metrics.AssistsPerPossession));
        Row(builder, side, "oreb_rate", F(metrics.OffensiveReboundRate));
        Row(builder, side, "ortg", F(metrics.OffensiveRating));
    }

    private static void Row(StringBuilder builder, string section, string metric, string value) =>
        builder.Append(section).Append(',').Append(Escape(metric)).Append(',').Append(Escape(value)).Append('\n');

    /// <summary>
    /// A metric with no samples is written as an empty cell, never as 0 and never as
    /// NaN. An empty cell says "not measured"; 0 says "measured, and it was zero".
    /// </summary>
    private static string Nullable(double? value) => value is null ? string.Empty : F(value.Value);

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string F(double value) => value.ToString("F6", CultureInfo.InvariantCulture);

    private static string Pct(double value) => value.ToString("F6", CultureInfo.InvariantCulture);

    private static string Num(double value) => ((long)value).ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Writes a single match's full event stream as JSON. Only for a single-match run:
/// the point of the batch mode is that events do NOT accumulate.
/// </summary>
public static class EventExporter
{
    public static void Write(string path, Guid matchId, ulong seed, string configHash, MatchEngine.Events.MatchEvent[] events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(events);

        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new
        {
            matchId,
            seed,
            configHash,
            eventCount = events.Length,
            events,
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, SummaryWriter.Json) + "\n");
    }
}
