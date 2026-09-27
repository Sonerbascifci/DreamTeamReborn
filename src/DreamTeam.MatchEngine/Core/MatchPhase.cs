namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// 06_RULES_AND_STATE_MACHINE.md §3 ana akisinin M3'e kadar olan alt kumesi.
///
/// <c>ShotPending</c> ve <c>FreeThrows</c> M3'te eklendi: bir <c>Advance</c> adimi
/// artik bir aksiyonu bastan sona bitirmek zorunda degildir. Birlikte bekleyen
/// serbest atislar motoru durdurmaz, atislar sirayla cozulur.
///
/// <c>DeadBall</c> tanimlidir ama bir adim sinirinda kalicilik amaciyla
/// kullanilmaz: topsuz devam eden inbound, onu doguran event'le ayni adimda
/// atomik olarak cozulur. Boylece event uretmeyen bir adim olusmaz ve motorun
/// "ilerleme yok" guvenlik agina takilma riski kalmaz.
/// </summary>
public enum MatchPhase
{
    NotStarted,
    LiveBall,
    DeadBall,

    /// <summary>M3: sut birakildi, sonucu bekleniyor.</summary>
    ShotPending,

    /// <summary>M3: serbest atis serisi oynuyor.</summary>
    FreeThrows,

    /// <summary>Periyot bitti; sonraki periyot veya uzatma baslatiliyor.</summary>
    PeriodBreak,

    Completed,
    Aborted,
}
