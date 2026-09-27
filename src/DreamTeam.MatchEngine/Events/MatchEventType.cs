using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Events;

/// <summary>Event ayrımı. 07 §2'deki event ailesinin M2 alt kümesi.</summary>
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
    Rebound,
    Turnover,
}
