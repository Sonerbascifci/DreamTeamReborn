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
