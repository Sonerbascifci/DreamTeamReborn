namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// 06_RULES_AND_STATE_MACHINE.md §3 ana akışının M2 alt kümesi.
///
/// <c>ShotPending</c> ve <c>FreeThrows</c> bilinçli olarak yoktur: M2'de şut
/// senkron çözülür, faul ve serbest atış M3'te gelir. Eksik fazlar bir
/// kısayol değildir; M3 bu enum'u genişletir.
/// </summary>
public enum MatchPhase
{
    NotStarted,
    LiveBall,
    DeadBall,

    /// <summary>Periyot bitti; sonraki periyot başlatılıyor. Canlı süre tüketmez.</summary>
    PeriodBreak,

    Completed,
    Aborted,
}
