using System.Collections.Immutable;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Simulator.Batch;

/// <summary>
/// M6: N match summaries merged into one. <b>Sums raw counts only.</b> No ratio
/// is ever averaged across matches, because that is the operation 08 §6 forbids.
/// Ratios are derived once, at the end, by <c>BalanceReport</c> from these sums.
///
/// <para><b>Thread safety.</b> <see cref="Add"/> is not thread safe. The batch
/// driver merges from a single thread after the workers finish, so no lock is
/// needed and none is added.</para>
/// </summary>
public sealed class SummaryAccumulator
{
    private readonly Dictionary<Guid, PlayerAccumulator> _players = [];
    private readonly Dictionary<string, int> _abortReasons = new(StringComparer.Ordinal);

    public int MatchCount { get; private set; }

    public int CompletedCount { get; private set; }

    public int AbortedCount { get; private set; }

    public int TieCount { get; private set; }

    public int HomeWins { get; private set; }

    public int AwayWins { get; private set; }

    public int OvertimeMatchCount { get; private set; }

    public TeamTotals Home { get; private set; } = TeamTotals.Zero;

    public TeamTotals Away { get; private set; } = TeamTotals.Zero;

    /// <summary>M6: per shot type, home. 08 S6 shot type distribution.</summary>
    public ShotTypeTally HomeShots { get; private set; } = ShotTypeTally.Empty;

    /// <summary>M6: per shot type, away.</summary>
    public ShotTypeTally AwayShots { get; private set; } = ShotTypeTally.Empty;

    public DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters Diagnostics { get; private set; } =
        DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters.Empty;

    public long TotalElapsedGameTimeMs { get; private set; }

    public long TotalPeriodsPlayed { get; private set; }

    public long TotalActions { get; private set; }

    public long TotalEvents { get; private set; }

    /// <summary>Both teams together. A per-team figure must HALVE this.</summary>
    public double TotalPossessions => Home.Possessions + (double)Away.Possessions;

    /// <summary>
    /// Margin sums, for a real standard deviation.
    ///
    /// <para><b>Neden toplam ve toplam kareleri?</b> 08 §6 skor varyansi istiyor
    /// ama 100K macin skoru saklamak bellek yiyen bir cozum. Toplam ve toplam
    /// kareleri yeterlidir: varyans = E[x^2] - E[x]^2. Daha once rapor
    /// std sapi sabit 0 yaziyordu; bu, olculmemis bir degeri olculmus gibi
    /// gostermek demekti ve kabul edilmedi.</para>
    /// </summary>
    private double _marginSum;

    private double _marginSquaredSum;

    /// <summary>Skor farkinin (ev - dep) toplami.</summary>
    public double MarginSum => _marginSum;

    /// <summary>
    /// Marginin standart sapmasi. Yaklasim: <c>sqrt(E[x^2] - E[x]^2)</c>.
    /// Tek mac varsa 0 (varyans tanimsizdir; 0 raporlanir, hata degil).
    /// </summary>
    public double MarginStandardDeviation
    {
        get
        {
            if (MatchCount == 0)
            {
                return 0;
            }

            var mean = _marginSum / MatchCount;
            var variance = (_marginSquaredSum / MatchCount) - (mean * mean);

            // Yuvarlama hatasi negatif verebilir; 0'dan kucuk olamaz.
            return variance <= 0 ? 0 : Math.Sqrt(variance);
        }
    }

    /// <summary>Per-player totals, keyed by id. Insertion-ordered, so output is stable.</summary>
    public IReadOnlyDictionary<Guid, PlayerAccumulator> Players => _players;

    /// <summary>Abort reasons with their counts. Empty when nothing aborted.</summary>
    public IReadOnlyDictionary<string, int> AbortReasons => _abortReasons;

    public void Add(MatchSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        MatchCount += 1;
        TotalElapsedGameTimeMs += summary.ElapsedGameTimeMs;
        TotalPeriodsPlayed += summary.PeriodsPlayed;
        TotalActions += summary.TotalActions;
        TotalEvents += summary.EventCount;

        Home = Add(Home, summary.Home);
        HomeShots = ShotTypeTally.Add(HomeShots, summary.HomeShots);
        AwayShots = ShotTypeTally.Add(AwayShots, summary.AwayShots);
        Away = Add(Away, summary.Away);
        Diagnostics = DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters.Merge(
            Diagnostics, summary.Diagnostics);

        var margin = (double)(summary.HomeScore - summary.AwayScore);
        _marginSum += margin;
        _marginSquaredSum += margin * margin;

        if (summary.Completed)
        {
            CompletedCount += 1;
        }
        else
        {
            AbortedCount += 1;
            var reason = summary.AbortReason ?? "(sebep yok)";

            _abortReasons[reason] = _abortReasons.GetValueOrDefault(reason) + 1;
        }

        if (summary.IsTie)
        {
            TieCount += 1;
        }
        else if (summary.HomeScore > summary.AwayScore)
        {
            HomeWins += 1;
        }
        else
        {
            AwayWins += 1;
        }

        if (summary.PeriodsPlayed > PeriodCountOfRegulation)
        {
            OvertimeMatchCount += 1;
        }

        foreach (var sample in summary.PlayerEnergy)
        {
            if (!_players.TryGetValue(sample.PlayerId, out var player))
            {
                player = new PlayerAccumulator(sample.PlayerId, sample.DisplayName, sample.Team);
                _players.Add(sample.PlayerId, player);
            }

            player.Appearances += 1;
            player.EnergySum += sample.Energy;
            player.SecondsOnCourtSum += sample.SecondsOnCourt;

            if (sample.Energy < player.MinEnergy)
            {
                player.MinEnergy = sample.Energy;
            }

            if (sample.Energy > player.MaxEnergy)
            {
                player.MaxEnergy = sample.Energy;
            }
        }
    }

    /// <summary>
    /// Regulation length. A 4-period profile is the simple NBA-inspired one (D31);
    /// a match with more periods than this went to overtime.
    /// </summary>
    public const int PeriodCountOfRegulation = 4;

    public static TeamTotals Add(TeamTotals left, TeamTotals right) => new()
    {
        Points = left.Points + right.Points,
        FieldGoalsMade = left.FieldGoalsMade + right.FieldGoalsMade,
        FieldGoalsAttempted = left.FieldGoalsAttempted + right.FieldGoalsAttempted,
        TwoPointersMade = left.TwoPointersMade + right.TwoPointersMade,
        TwoPointersAttempted = left.TwoPointersAttempted + right.TwoPointersAttempted,
        ThreePointersMade = left.ThreePointersMade + right.ThreePointersMade,
        ThreePointersAttempted = left.ThreePointersAttempted + right.ThreePointersAttempted,
        FreeThrowMakes = left.FreeThrowMakes + right.FreeThrowMakes,
        FreeThrowAttempts = left.FreeThrowAttempts + right.FreeThrowAttempts,
        Assists = left.Assists + right.Assists,
        Turnovers = left.Turnovers + right.Turnovers,
        PersonalFouls = left.PersonalFouls + right.PersonalFouls,
        Blocks = left.Blocks + right.Blocks,
        OffensiveRebounds = left.OffensiveRebounds + right.OffensiveRebounds,
        DefensiveRebounds = left.DefensiveRebounds + right.DefensiveRebounds,
        TeamRebounds = left.TeamRebounds + right.TeamRebounds,
        Possessions = left.Possessions + right.Possessions,
    };

    /// <summary>
    /// An empty accumulator is a <b>valid empty report</b>, not an error: a batch of
    /// zero matches is a legal request and must produce zeros, not a crash.
    /// </summary>
    public static SummaryAccumulator Empty { get; } = new();

    public IReadOnlyList<PlayerAccumulator> PlayersInReportOrder() =>
        [.. _players.Values
            .OrderBy(player => player.Team)
            .ThenBy(player => player.DisplayName, StringComparer.Ordinal)];
}

/// <summary>Per-player totals across the batch, plus the extremes needed for edge cases.</summary>
public sealed class PlayerAccumulator
{
    public PlayerAccumulator(Guid playerId, string displayName, TeamSide team)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        Team = team;
    }

    public Guid PlayerId { get; }

    public string DisplayName { get; }

    public TeamSide Team { get; }

    /// <summary>How many matches this player appeared in.</summary>
    public int Appearances { get; set; }

    public long EnergySum { get; set; }

    public double SecondsOnCourtSum { get; set; }

    public int MinEnergy { get; set; } = 100;

    public int MaxEnergy { get; set; }

    public double AverageEnergy => Appearances == 0 ? 0 : (double)EnergySum / Appearances;

    public double AverageSecondsOnCourt => Appearances == 0 ? 0 : SecondsOnCourtSum / Appearances;

    public double AverageMinutes => AverageSecondsOnCourt / 60.0;
}
