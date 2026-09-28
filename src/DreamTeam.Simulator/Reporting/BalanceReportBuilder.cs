using System.Globalization;
using DreamTeam.Simulator.Batch;

namespace DreamTeam.Simulator.Reporting;

/// <summary>
/// M6: turns a <see cref="SummaryAccumulator"/> into a <see cref="BalanceReport"/>.
/// All ratios are computed here, once, from summed counts.
///
/// <para><b>Zero denominators yield null, never a number.</b> 08 §6 is explicit
/// that a zero sample must not be reported as 0% or as Infinity. Nullable
/// <c>double?</c> in <see cref="BalanceMetrics"/> is how that is enforced at the
/// type level rather than by convention.</para>
/// </summary>
public static class BalanceReportBuilder
{
    /// <summary>Regulation minutes. Overtime must not inflate Pace48 (08 §6).</summary>
    public const double RegulationMinutes = 48.0;

    public static BalanceReport Build(
        string fixtureName,
        SummaryAccumulator summary,
        string configHash,
        IReadOnlyList<ThresholdResult>? extraThresholds = null)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var matches = (double)summary.MatchCount;
        var home = summary.Home;
        var away = summary.Away;
        var gameMinutes = summary.TotalElapsedGameTimeMs / 60_000.0;

        var homeWinRate = WilsonInterval.For(summary.HomeWins, summary.MatchCount);
        var abortRate = WilsonInterval.For(summary.AbortedCount, summary.MatchCount);
        var overtimeRate = WilsonInterval.For(summary.OvertimeMatchCount, summary.MatchCount);
        var tieRate = WilsonInterval.For(summary.TieCount, summary.MatchCount);

        var report = new BalanceReport
        {
            FixtureName = fixtureName,
            MatchCount = summary.MatchCount,
            ConfigHash = configHash,
            HomeWinRate = homeWinRate,
            AbortRate = abortRate,
            OvertimeRate = overtimeRate,
            TieRate = tieRate,
            AverageHomeScore = matches == 0 ? 0 : home.Points / matches,
            AverageAwayScore = matches == 0 ? 0 : away.Points / matches,
            ScoreStandardDeviation = summary.MarginStandardDeviation,
            AveragePossessionsPerTeam = matches == 0 ? 0 : (home.Possessions + (double)away.Possessions) / (2 * matches),
            Pace48 = Pace(summary, gameMinutes),
            TotalPossessions = summary.TotalPossessions,
            AveragePeriods = matches == 0 ? 0 : summary.TotalPeriodsPlayed / matches,
            HomeShots = summary.HomeShots,
            AwayShots = summary.AwayShots,
            Home = Metrics(home, away, matches),
            Away = Metrics(away, home, matches),
            Thresholds = [.. Thresholds(summary, homeWinRate, abortRate), .. extraThresholds ?? []],
            AbortReasons = summary.AbortReasons,
            DiagnosticsReport = summary.Diagnostics.ToReport(),
            PlayerEnergy =
            [
                .. summary.PlayersInReportOrder()
                    .Select(player => new PlayerEnergyRow(
                        player.Team.ToString(),
                        player.DisplayName,
                        player.AverageEnergy,
                        player.AverageMinutes,
                        player.MinEnergy,
                        player.MaxEnergy))
            ],
        };

        return report;
    }

    /// <summary>
    /// 08 §6: Pace48 = 48 * possessions / minutes played, with overtime removed.
    /// Dividing by actual minutes is what removes it; there is no separate
    /// normalisation step to forget.
    ///
    /// <para><b>TEK TAKIM possessions'ı.</b> <c>TotalPossessions</c> iki takımın
    /// toplamıdır. Onu doğrudan kullanan ilk sürüm Pace48'i iki katına çıkardı
    /// ve 2.000 maçlık gerçek koşuda 211.9 ölçtü — basketbolda tempo değil. Takım
    /// başına 106.3 doğru değerdir. Payda ikiye bölünür.</para>
    /// </summary>
    public static double Pace(SummaryAccumulator summary, double gameMinutes) =>
        gameMinutes <= 0 ? 0 : RegulationMinutes * summary.TotalPossessions / (2 * gameMinutes);

    /// <summary>A ratio, or null when the denominator is zero. Never NaN, never Infinity.</summary>
    public static double? Ratio(double numerator, double denominator) =>
        denominator == 0 ? null : numerator / denominator;

    public static BalanceMetrics Metrics(TeamTotals team, TeamTotals opponent, double matches)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(opponent);

        var possessions = team.Possessions;
        var completedPossessions = possessions;

        return new BalanceMetrics
        {
            Matches = matches,
            PointsPerMatch = matches == 0 ? 0 : team.Points / matches,
            FieldGoalRate = Ratio(team.FieldGoalsMade, team.FieldGoalsAttempted),
            TwoPointRate = Ratio(team.TwoPointersMade, team.TwoPointersAttempted),
            ThreePointRate = Ratio(team.ThreePointersMade, team.ThreePointersAttempted),
            ThreeAttemptShare = Ratio(team.ThreePointersAttempted, team.FieldGoalsAttempted),
            FreeThrowRate = Ratio(team.FreeThrowMakes, team.FreeThrowAttempts),
            FreeThrowsPerPossession = completedPossessions == 0
                ? 0
                : (double)team.FreeThrowAttempts / completedPossessions,
            TurnoversPerPossession = completedPossessions == 0
                ? 0
                : (double)team.Turnovers / completedPossessions,
            FoulsPerPossession = completedPossessions == 0
                ? 0
                : (double)team.PersonalFouls / completedPossessions,
            AssistsPerPossession = completedPossessions == 0
                ? 0
                : (double)team.Assists / completedPossessions,
            OffensiveReboundRate = Ratio(team.OffensiveRebounds, team.OffensiveRebounds + (double)opponent.DefensiveRebounds) ?? 0,
            OffensiveRating = completedPossessions == 0
                ? 0
                : 100.0 * team.Points / completedPossessions,
        };
    }

    /// <summary>
    /// The six internal-consistency thresholds of D98c. A trip is an ALARM that
    /// says "look here", never a diagnosis.
    /// </summary>
    private static IReadOnlyList<ThresholdResult> Thresholds(
        SummaryAccumulator summary,
        ProportionEstimate homeWinRate,
        ProportionEstimate abortRate)
    {
        var results = new List<ThresholdResult>(6);

        results.Add(new ThresholdResult(
            1,
            "Mirror simetrisi",
            homeWinRate.HasSamples && !homeWinRate.DeviatesFromHalf,
            homeWinRate.HasSamples
                ? $"ev kazanma {Pct(homeWinRate.Point)}, %95 [{Pct(homeWinRate.Lower)}, {Pct(homeWinRate.Upper)}]"
                : "ornek yok"));

        results.Add(new ThresholdResult(
            2,
            "Uc deger guvenligi (abort=0)",
            abortRate.Successes == 0,
            abortRate.Successes == 0
                ? "aborted 0"
                : $"aborted {abortRate.Successes} (gizlenmedi)"));

        var pace = Pace(summary, summary.TotalElapsedGameTimeMs / 60_000.0);

        results.Add(new ThresholdResult(
            3,
            "Pace48 makul aralik (60-130)",
            pace > 60 && pace < 130,
            $"Pace48 = {pace.ToString("F1", CultureInfo.InvariantCulture)}"));

        var ortg = summary.Home.Possessions == 0
            ? 0
            : 100.0 * summary.Home.Points / summary.Home.Possessions;
        results.Add(new ThresholdResult(
            4,
            "ORtg iki tarafta benzer (<5 fark)",
            Math.Abs(ortg - AwayRating(summary)) < 5,
            $"ev {ortg.ToString("F1", CultureInfo.InvariantCulture)} vs "
            + $"dep {AwayRating(summary).ToString("F1", CultureInfo.InvariantCulture)}"));

        var fgp = summary.Home.FieldGoalsAttempted == 0
            ? (double?)null
            : (double)summary.Home.FieldGoalsMade / summary.Home.FieldGoalsAttempted;
        results.Add(new ThresholdResult(
            5,
            "FGP 0-1 araliginda",
            fgp is null || (fgp >= 0 && fgp <= 1),
            fgp is null ? "ornek yok" : $"ev FGP = {fgp.Value.ToString("P3", CultureInfo.InvariantCulture)}"));

        results.Add(new ThresholdResult(
            6,
            "NaN/Infinity yok",
            !HasNonFinite(summary),
            HasNonFinite(summary) ? "sonlu olmayan deger VAR" : "tum degerler sonlu"));

        return results;
    }

    private static double AwayRating(SummaryAccumulator summary) =>
        summary.Away.Possessions == 0
            ? 0
            : 100.0 * summary.Away.Points / summary.Away.Possessions;

    private static bool HasNonFinite(SummaryAccumulator summary)
    {
        var pace = Pace(summary, summary.TotalElapsedGameTimeMs / 60_000.0);
        var candidates = new[]
        {
            pace,
            100.0 * summary.Home.Points / Math.Max(1, summary.Home.Possessions),
            100.0 * summary.Away.Points / Math.Max(1, summary.Away.Possessions),
        };

        return candidates.Any(value => double.IsNaN(value) || double.IsInfinity(value));
    }

    private static string Pct(double value) => (value * 100).ToString("F2", CultureInfo.InvariantCulture);
}
