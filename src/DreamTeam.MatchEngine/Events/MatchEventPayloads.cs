using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Events;

/// <summary>
/// Bir event'in türüne özgü verisi. 07 §1: "Event sözleşmesinde payload türüne göre
/// doğrulanmış olmalıdır." Her event türü tam olarak bir payload tipine karşılık gelir;
/// uyumsuzluk <see cref="MatchEvent.PayloadAs{T}"/> ile erken ve anlaşılır biçimde hata verir.
///
/// Ayrım kuralı: <c>ShotAttempt</c> şutun kimliğini açar ve <b>FGA saymaz</b>.
/// Sayılabilirlik <c>ShotMade</c>/<c>ShotMissed</c> payload'ındaki
/// <c>CountsAsFieldGoalAttempt</c> ile kesinleşir (07 §3). M2'de bu bayrak daima
/// true'dur; kaçan shooting foul yüzünden sayılmayan deneme M3 konusudur.
/// </summary>
public abstract record MatchEventPayload;

// --- Lifecycle ---

public sealed record MatchStartedPayload(string HomeTeamName, string AwayTeamName) : MatchEventPayload;

public sealed record PeriodStartedPayload(int StartedPeriod, long PeriodDurationMs, bool IsOvertime)
    : MatchEventPayload;

public sealed record PeriodEndedPayload(int EndedPeriod, bool IsOvertime) : MatchEventPayload;

/// <summary>
/// <see cref="IsTie"/> true ise kazanan seçilmemiştir. M2'de uzatma yoktur ve eşitlik
/// rastgele kazananla çözülmez (06 §8). M3 uzatmayı ekleyecek.
/// </summary>
public sealed record MatchEndedPayload(int HomeScore, int AwayScore, bool IsTie) : MatchEventPayload;

/// <summary>Guard aşıldı veya kurulum reddedildi. Skor uydurulmaz.</summary>
public sealed record MatchAbortedPayload(string Reason) : MatchEventPayload;

// --- Possession ---

public sealed record PossessionStartedPayload(int NewPossessionId, TeamSide Offense) : MatchEventPayload;

public sealed record PossessionEndedPayload(int EndedPossessionId, PossessionEndReason Reason) : MatchEventPayload;

// --- Shot ---

/// <summary>
/// Aksiyon canlı süre tüketti ama şut denemesine dönüşmedi; possession devam eder.
///
/// Bu event olmadan saat ilerlemesi event akışında görünmez olurdu. Her aksiyon
/// tam olarak bir "sonuç" event'i üretir: <c>Turnover</c>, <c>ShotAttempt</c> veya
/// <c>ActionCompleted</c>. 05 §4'teki çok aşamalı possession yapısının basitleştirilmiş
/// hâli; ayrıntılı <c>Pass</c>/<c>Drive</c>/<c>Screen</c> event'leri M3/M4'te gelir.
/// </summary>
public sealed record ActionCompletedPayload(OffensiveAction Action) : MatchEventPayload;

/// <summary>
/// Şut denemesi. 07 §2'deki zarf + payload sözleşmesi.
///
/// <para><b>M4: iki alan eklendi (D67).</b> <c>Quality</c> ve <c>ShooterEnergy</c>
/// politika etkisini event'ten <b>gözlenebilir</b> kılar. M4'ün kabul kriteri
/// ("controlled policy değişimi beklenen aksiyon karışımını etkiliyor") yalnız
/// kalite değişimi okunabildiğinde yazılabilir; 08 §88'in istediği enerji
/// dağılımı ve M6 kalibrasyonu da bu alanlara bağlıdır.</para>
///
/// <para><c>Quality</c> savunma + taktik + IQ'dan türetilir (0-100);
/// <c>ShooterEnergy</c> bırakma anındaki enerjidir (0-100). İkisi de
/// <b>çözüm girdisinin görünür hâlidir</b>, ikinci bir hesap yolunu temsil
/// etmez.</para>
/// </summary>
public sealed record ShotAttemptPayload(
    long ShotId,
    ShotType ShotType,
    int Quality,
    int ShooterEnergy) : MatchEventPayload;

public sealed record ShotMadePayload(
    long ShotId,
    ShotType ShotType,
    int Points,
    bool CountsAsFieldGoalAttempt) : MatchEventPayload;

public sealed record ShotMissedPayload(
    long ShotId,
    ShotType ShotType,
    bool CountsAsFieldGoalAttempt) : MatchEventPayload;

// --- M3: blok ---

/// <summary>
/// Blok. Ayni <c>ShotId</c>'nin niteligidir: yeni FGA yaratmaz, yeni miss
/// yaratmaz (zaten <c>ShotMissed</c> vardir) ve isabet sayacina girmez.
/// 07 §3: "Block ayni ShotId'nin niteligidir; yeni miss/FGA uretmez."
/// </summary>
public sealed record BlockPayload(long ShotId, Guid DefenderId) : MatchEventPayload;

// --- Rebound ---

/// <summary>
/// Canlı ribaund fırsatının sonucu. <see cref="IsTeamRebound"/> true ise ribaund
/// oyuncuya atfedilmez: 08 §2 takım ribaundu oyuncuya rastgele dağıtılamaz.
/// M2'de bu yol üretilmez; sayaç M2'de sıfırdır.
/// </summary>
public sealed record ReboundPayload(long ShotId, bool Offensive, bool IsTeamRebound) : MatchEventPayload;

// --- Turnover ---

/// <summary>
/// Hücum sonucu olan top kaybı. Savunma atfı (steal) ayrı bir nedendir ve M3'te
/// gelir; o zaman attribution zarfın <c>SecondaryPlayerId</c> alanına yazılır ve
/// turnover sayısı artmaz (04 sözlüğü notu).
/// </summary>
public sealed record TurnoverPayload(long TurnoverId, TurnoverKind Kind) : MatchEventPayload;

// --- M3: faul ve serbest atis ---

/// <summary>
/// Faul. Zarfin <c>PlayerId</c> alani faulu <b>eden</b> oyuncuyu gosterir;
/// faulu yiyen oyuncu <c>SecondaryPlayerId</c>'dir. 07 §2'deki atıf ters çevrilerek
/// yazılmaz: sözleşmede birincil aktör "shooter, ribaund alan, top kaybeden"
/// tanımlıdır, faul eden de bu listeye girer.
///
/// Faul event'i hiçbir koşulda FGA yazmaz. Sayılabilirlik yalnız
/// <c>ShotMade</c>/<c>ShotMissed</c> payload'ındadır (07 §3).
/// </summary>
public sealed record FoulPayload(long FoulId, FoulType Type, int FreeThrowCount) : MatchEventPayload;

/// <summary>
/// Serbest atis denemesi. Canli oyun suresi tuketmez (06 §2).
/// <c>Index</c> 0 tabanlidir, <c>Count</c> serinin toplamidir.
/// </summary>
public sealed record FreeThrowAttemptPayload(
    long FTSeriesId,
    int Index,
    int Count,
    bool IsFinalShot) : MatchEventPayload;

public sealed record FreeThrowMadePayload(
    long FTSeriesId,
    int Index,
    int Count) : MatchEventPayload;

public sealed record FreeThrowMissedPayload(
    long FTSeriesId,
    int Index,
    int Count,
    bool BallIsLive) : MatchEventPayload;

// ================================================================ M5: mudahale

/// <summary>
/// M5 (D86): hucum taktigi degisti. 07 §2 "Müdahale" ailesi; CommandId
/// takip icin payload'da tasinir (zarfin <c>actionId</c>'si kullanilamaz,
/// cunku mudahale bir aksiyona ait degildir).
/// </summary>
public sealed record TacticChangedPayload(
    Guid CommandId,
    OffensiveTactic Previous,
    OffensiveTactic Current) : MatchEventPayload;

/// <summary>M5: savunma policy'si degisti.</summary>
public sealed record DefenseChangedPayload(
    Guid CommandId,
    DefensiveTactic Previous,
    DefensiveTactic Current) : MatchEventPayload;

/// <summary>M5 (D59): tempo degisti.</summary>
public sealed record PaceChangedPayload(
    Guid CommandId,
    Pace Previous,
    Pace Current) : MatchEventPayload;

/// <summary>
/// M5 (D85): substitution. <b>Atomik</b> bes-bes gecistir (07 §3).
///
/// <para><c>Sequence</c> degismedigi icin bu iki oyuncu "sirayla degisti"
/// gibi yorumlanamaz; tek bir gecis olarak okunur.</para>
/// </summary>
public sealed record SubstitutionPayload(
    Guid CommandId,
    Guid IncomingPlayerId,
    Guid OutgoingPlayerId) : MatchEventPayload;

/// <summary>
/// M5 (D84): timeout alindi.
///
/// <para><b>Canli saati tuketmez</b> (06 §27) ve <b>hucrem saatini baslatmaz</b>
/// (06 §6). Payload bunu belirtmez cunku etkisi zaten yoktur; testler
/// saatin degismedigini dogrular.</para>
///
/// <para><c>FullUsed</c>/<c>ShortUsed</c> sayaçlar bu olaydan SONRAki
/// degerleri tasir, boylece istemci bultutu gondermeden bilesin.</para>
/// </summary>
public sealed record TimeoutPayload(
    Guid CommandId,
    TimeoutKind Kind,
    int FullUsed,
    int ShortUsed) : MatchEventPayload;

// ========================================================== M5: komut sonucu

/// <summary>
/// M5: komut state'e islendi (07 §5 "Applied = state'e islendi").
///
/// <para>Bu, ACK DEGILDIR. ACK ("alindi/kuyruga girdi") yalnizca kuyruga
/// giris anindadir ve domain event degildir; replay gerektirmez.</para>
/// </summary>
public sealed record CommandAppliedPayload(
    Guid CommandId,
    ManagerCommandKind Kind,
    CommandBoundary Boundary,
    long AcceptedOrder) : MatchEventPayload;

/// <summary>
/// M5: komut reddedildi. Sebep kodu kaydedilir (07 §5).
///
/// <para><b>Reddetme maci bitirmez.</b> Yonetici hata yaptiginda mac devam
/// eder; yalniz sonuc raporlanir.</para>
/// </summary>
public sealed record CommandRejectedPayload(
    Guid CommandId,
    ManagerCommandKind Kind,
    CommandRejectionReason Reason,
    string? Message) : MatchEventPayload;
