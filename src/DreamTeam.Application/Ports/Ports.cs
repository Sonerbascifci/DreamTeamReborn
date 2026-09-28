using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Config;

namespace DreamTeam.Application.Ports;

/// <summary>
/// M7 (D110): use case katmaninin dis dunya ile konustugu YERLER.
///
/// <para><b>Bu dosya bir sozlesmedir, bir altyapi degil.</b> Her arayuzun bir
/// test sahte (fake) uygulamasi vardir ve Application testleri yalnizca onlari
/// kullanir. PostgreSQL, JWT ve SignalR bu arayuzlerin <i>arkasinda</i> durur;
/// Application hicbiri bilmez.</para>
///
/// <para><b>Neden ayri arayuzler?</b> 03 "her isim icin zorunlu interface veya
/// ayri servis uretme" diyor. Bu dosyadaki yedi arayuzun her biri icin somut bir
/// degisim nedeni var ve her biri bir testin degistirilebilir olmasini sagliyor.
/// Liste "belki lazim olur" diye genisletilmedi.</para>
/// </summary>
public interface IUserRepository
{
    Task<UserRecord?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(UserRecord user, CancellationToken cancellationToken);
}

/// <summary>Hesap. 04 §"Kalici urun modeli": kimlik saglayicisi ayrica secilir.</summary>
public sealed record UserRecord(Guid Id, string DisplayName, DateTimeOffset CreatedAt);

public interface IPlayerRepository
{
    Task<PlayerRecord?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerRecord>> ListForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task AddAsync(PlayerRecord player, CancellationToken cancellationToken);
}

/// <summary>
/// D111: oyuncu KISI BASINA bir kopyadir. <see cref="OwnerUserId"/> bu kopyanin
/// sahibidir ve satiri baska bir kullaniciya satilamaz.
/// </summary>
public sealed record PlayerRecord(
    Guid Id,
    Guid OwnerUserId,
    string DisplayName,
    Position Position,
    PlayerRatings Ratings);

public interface ITeamRepository
{
    Task<TeamRecord?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TeamRecord>> ListForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task AddAsync(TeamRecord team, CancellationToken cancellationToken);

    /// <summary>Kadroya oyuncu ekler. Oyuncunun ayni kullaniciya ait oldugunu dogrular.</summary>
    Task AddRosterEntryAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken);

    Task RemoveRosterEntryAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken);

    /// <summary>Takimin anlik kadro oyuncusu kimlikleri, eklenme sirasina gore.</summary>
    Task<IReadOnlyList<Guid>> RosterPlayerIdsAsync(Guid teamId, CancellationToken cancellationToken);
}

/// <summary>
/// D111: kadro boyutu SERBESTIR; lineup 5 oyuncudur. <see cref="RosterPlayerIds"/>
/// bu yuzden motorun lineup kuralindan (D31) farklidir ve burada sinirlanmaz.
/// </summary>
public sealed record TeamRecord(
    Guid Id,
    Guid OwnerUserId,
    string Name,
    DateTimeOffset CreatedAt);

public interface IMatchRepository
{
    Task<MatchRecord?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(MatchRecord match, CancellationToken cancellationToken);

    /// <summary>
    /// 03 "Match completion idempotent": maci <b>kosullu</b> olarak tamamlar.
    /// <c>WHERE lifecycle = 'Running'</c> kosulu olmadan guncellerse iki komsu
    /// istek ikisini de yazar. Donen <c>true</c> yalnizca ilk cagiranin
    /// yazdigini bildirir; ikinci cagri <c>false</c> alir ve bir sey yapmaz.
    /// </summary>
    Task<bool> TryCompleteAsync(
        Guid matchId,
        MatchCompletion completion,
        CancellationToken cancellationToken);

    /// <summary>D113: sunucu yeniden basladiginda kalan Running maclari isaretler.</summary>
    Task<int> AbortRunningAsync(string reason, CancellationToken cancellationToken);
}

/// <summary>
/// M7: kalici mac lifecycle'i.
///
/// <para><b>Neden ayri, <c>MatchStatus</c> degil?</b> <c>MatchStatus</c> moturun
/// <b>SONUC</b>dur: <c>Completed</c> veya <c>Aborted</c>. Canli bir mac motor
/// tarafinda hâlâ <c>NotStarted</c>tir ama sunucuda "suruyor"dur. Iki kavram
/// birbirine karistirilirsa 09'un kabul maddeleri ("client'in gonderdigi skor
/// dikkate alinmaz") olculemez.</para>
/// </summary>
public enum MatchLifecycle
{
    Running,
    Completed,
    Aborted,
}

/// <summary>
/// Motorun <b>SONUC</b> durumunu kalici <b>YASAM</b> durumuna cevirir.
/// Ikisi ayri kavramlar (bkz. <see cref="MatchLifecycle"/>); bu esleme tek
/// yerde durur ki biri degistiginde digeri unutulmasin.
/// </summary>
public static class MatchLifecycleMapping
{
    public static MatchLifecycle ToLifecycle(this MatchStatus status) => status switch
    {
        MatchStatus.Completed => MatchLifecycle.Completed,
        MatchStatus.Aborted => MatchLifecycle.Aborted,
        _ => throw new ArgumentOutOfRangeException(
            nameof(status),
            status,
            "Terminal olmayan bir motor durumu kalici mac kaydina yazilamaz."),
    };
}

public sealed record MatchRecord(
    Guid Id,
    Guid OwnerUserId,
    MatchLifecycle Lifecycle,
    ulong Seed,
    string ConfigHash,
    string EngineVersion,
    string RulesVersion,
    string SetupDigest,
    Guid HomeTeamId,
    Guid AwayTeamId,
    DateTimeOffset StartedAt,
    int? HomeScore,
    int? AwayScore,
    string? AbortReason);

/// <summary>Kalici sonuc. 04 §"Veri butunlugu": mac sonucu bir kez yazilir.</summary>
public sealed record MatchCompletion(
    MatchStatus Status,
    int HomeScore,
    int AwayScore,
    bool IsTie,
    int HomePossessions,
    int AwayPossessions,
    long ElapsedGameTimeMs,
    int PeriodsPlayed,
    string? AbortReason,
    DateTimeOffset CompletedAt);

public interface IEventRepository
{
    /// <summary>
    /// 04: <c>(MatchId, Sequence)</c> benzersizdir. Tekrar yazma <b>bilerek</b>
    /// yoksayilir: yeniden baglanan istemci ayni araligi tekrar isteyebilir ve
    /// bu bir hata degil, beklenen davranistir.
    /// </summary>
    Task<int> AppendRangeAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MatchEvent>> ReadAfterAsync(
        Guid matchId,
        long afterSequence,
        int maxCount,
        CancellationToken cancellationToken);
}

public interface ICommandLogRepository
{
    /// <summary>
    /// 07 §5: "Ayni CommandId yeniden gelirse ayni sonuc dondurulur."
    /// <c>UNIQUE (match_id, command_id)</c> bunu garanti eder. Donen deger
    /// <c>false</c> ise komut daha once kaydedilmis demektir ve sonucu
    /// <b>yeniden hesaplanmaz</b>.
    /// </summary>
    Task<bool> TryRecordAcceptedAsync(
        Guid matchId,
        Guid commandId,
        Guid ownerUserId,
        ManagerCommandKind kind,
        CommandBoundary boundary,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task RecordResultAsync(
        Guid matchId,
        Guid commandId,
        bool applied,
        CommandRejectionReason? rejectionReason,
        string? message,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken);
}

/// <summary>
/// Motorun <c>ConfigHash</c>'ini verir. M6 D103: denge kaynagi JSON
/// belgedir; motorun fabrika varsayilani DEGIL. Bu port sayesinde Application
/// motoru hic kurmaz, hazir config'i alir.
/// </summary>
public interface IMatchConfigProvider
{
    EngineConfig GetConfig(out string configHash);
}

/// <summary>
/// D87 icin kanonik digest. <c>record.Equals</c> yerine gecer: kadro
/// <c>RosterOrdering.Canonical</c> ile siralanir, 18 rating alani acik sabit
/// sirayla yazilir, SHA-256 alinir.
/// </summary>
public interface ISetupDigest
{
    string Of(MatchSetup setup);
}

/// <summary>
/// Mevcut kullanicinin kimligi. 07 §5: "AuthenticatedUserId baglanti/session'dan
/// cozulur; client'in 'ben su takimim' beyanina guvenilmez."
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}

/// <summary>Zaman. Testler bunu sahte (fake) uygular; duvar saati motora girmez.</summary>
public interface IMonotonicClock
{
    long ElapsedMilliseconds { get; }
}

/// <summary>Bekleme. Motor beklemez; pacer bu port uzerinden bekler.</summary>
public interface IDelay
{
    Task DelayAsync(int milliseconds, CancellationToken cancellationToken);
}

/// <summary>
/// Canli mac olaylarini istemcilere yayin. Application SignalR'yi BILMEZ;
/// Api bu arayuzu <c>IHubContext&lt;MatchHub&gt;</c> ile uygular.
/// </summary>
public interface IMatchBroadcaster
{
    Task PublishEventsAsync(Guid matchId, IReadOnlyList<MatchEvent> events, CancellationToken cancellationToken);

    Task PublishCommandResultAsync(
        Guid matchId,
        Guid commandId,
        bool applied,
        string? reason,
        CancellationToken cancellationToken);
}
