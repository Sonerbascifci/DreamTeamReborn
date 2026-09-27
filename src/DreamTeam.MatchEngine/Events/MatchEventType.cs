namespace DreamTeam.MatchEngine.Events;

/// <summary>
/// Event ayrimi. 07 §2'deki event ailesinin M5'e kadar olan alt kumesi.
///
/// <para><b><c>Steal</c>, <c>TeamRebound</c> ve <c>BallOutOfBounds</c> YOK.</b>
/// 07 §2'de listelenirler ama M5'te eklenmedi: steal atfedimi D66'da M5 adayi
/// olarak isaretlendi ama M5'in command yuzeyiyle cakisma riski nedeniyle
/// M6'ya birakildi. <c>Turnover</c> yine de <c>LostBall</c> uretiyor; bu
/// bilinçli ve dokumante bir eksikliktir, sahte bir ayrim degil.</para>
///
/// <para><c>Pass</c>/<c>Drive</c>/<c>Screen</c> da eklenmedi: 07 §2 bunlari
/// "sunum ipuclari" olarak tanimlar ve "her gorsel ayrintiyi domain event
/// haline getirme" der. M8/M9 sunum sozlesmesinin isidir.</para>
/// </summary>
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

    // ------------------------------------------------ M5: mudahale (07 §2)

    /// <summary>
    /// M5 (D86): hucum taktigi degisti. <c>CommandApplied</c> DEGILDIR; bu
    /// domain olayidir, 07 §2 "Müdahale" ailesinde yer alir. Onceki/sonraki
    /// degerler payload'da.
    /// </summary>
    TacticChanged,

    /// <summary>M5: savunma policy'si degisti.</summary>
    DefenseChanged,

    /// <summary>M5: tempo degisti (D59).</summary>
    PaceChanged,

    /// <summary>
    /// M5 (D85): substitution. <b>Atomik</b> bes-bes gecistir (07 §3); ara
    /// durum olmaz.
    /// </summary>
    Substitution,

    /// <summary>M5 (D84): timeout alindi. Canli saati tuketmez, hucrem saatini baslatmaz.</summary>
    Timeout,

    // ------------------------------------------- M5: komut sonucu (07 §2)

    /// <summary>
    /// M5: komut state'e islendi. <c>CommandRejected</c> DEGILDIR. 07 §5: "ACK =
    /// alindi/kuyruga girdi. Applied = state'e islendi. Bu ikisini tek basari
    /// mesajinda karistirma."
    /// </summary>
    CommandApplied,

    /// <summary>M5: komut reddedildi. Sebep kodu payload'da; <b>maci bitirmez</b>.</summary>
    CommandRejected,
}
