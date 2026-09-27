namespace DreamTeam.MatchEngine.Events;

/// <summary>Event ayrimi. 07 §2'deki event ailesinin M3'e kadar olan alt kumesi.</summary>
public enum MatchEventType
{
    MatchStarted,
    PeriodStarted,
    PeriodEnded,
    MatchEnded,
    MatchAborted,
    PossessionStarted,
    PossessionEnded,
    ActionCompleted,
    ShotAttempt,
    ShotMade,
    ShotMissed,

    /// <summary>M3: blok ayni ShotId'nin niteligidir, ikinci FGA yazmaz.</summary>
    Block,

    Rebound,
    Turnover,

    /// <summary>M3: faul. Kendi basina FGA yazmaz.</summary>
    Foul,

    /// <summary>M3: serbest atis denemesi.</summary>
    FreeThrowAttempt,

    /// <summary>M3: serbest atis isabeti.</summary>
    FreeThrowMade,

    /// <summary>M3: serbest atis kacirmasi.</summary>
    FreeThrowMissed,
}
