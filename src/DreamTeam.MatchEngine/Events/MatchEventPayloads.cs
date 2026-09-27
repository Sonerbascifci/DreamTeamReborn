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

public sealed record ShotAttemptPayload(long ShotId, ShotType ShotType) : MatchEventPayload;

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
