using System.Security.Claims;
using DreamTeam.Api.Contracts;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DreamTeam.Api.Realtime;

/// <summary>
/// M7: canli mac hub'i.
///
/// <para><b>HUB SIFIR OTORITEYDIR.</b> Burada hicbir kural uygulanmaz;
/// hub yalnizca mesaji kabul eder ve Application katmanina devreder. Bu
/// ayrim test edilebilir: hub'i degistirmeden kurallar degisir.
/// Kural <i>burada</i> konulsaydi her yeni kontrol icin ikinci bir yol
/// acilirdi ve "hangi yol gecerli" sorusu cevapsiz kalirdi.</para>
///
/// <para><b>YETKI: [Authorize].</b> Yetkisiz bir baglanti hicbir metotu
/// goremez. Ek olarak her istek icin macin sahibi TEYIT EDILIR (T19); sahiplik
/// hub'da degil use case'te denetlenir.</para>
///
/// <para><b>SIDEBAR: yayin grubu.</b> Yayin, oturumun kendisinden gelir;
/// istemci "bana su maci gonder" diyemez. <c>Subscribe</c> yalnizca zaten
/// yetkili oldugu bir macin grubuna katilir.</para>
/// </summary>
[Authorize]
public sealed class MatchHub : Hub
{
    private readonly SendManagerCommand _send;
    private readonly MatchSessionStore _sessions;
    private readonly ICurrentUser _current;
    private readonly IMatchRepository _matches;
    private readonly ILogger<MatchHub> _log;

    public MatchHub(
        SendManagerCommand send,
        MatchSessionStore sessions,
        ICurrentUser current,
        IMatchRepository matches,
        ILogger<MatchHub> log)
    {
        _send = send;
        _sessions = sessions;
        _current = current;
        _matches = matches;
        _log = log;
    }

    /// <summary>Yeniden baglanma. 07 §8: snapshot + event siniri ATOMIK.</summary>
    public async Task<StateResponse> RequestState(StateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownership = await CheckOwnershipAsync(request.MatchId).ConfigureAwait(false);

        if (!ownership.IsAllowed)
        {
            throw Forbidden(ownership.Error);
        }

        var session = _sessions.Find(request.MatchId);

        if (session is null)
        {
            // Mac canli degil. Kalici kayitta var olabilir; istemci
            // HTTP GET /api/matches/{id} ile sonucu okur.
            throw new HubException(
                $"Mac canli degil: {request.MatchId}. Sunucu yeniden baslamis veya mac bitmis olabilir.");
        }

        var capture = await session
            .CaptureForAsync(request.AfterSequence, request.IncludeSnapshot, Context.ConnectionAborted)
            .ConfigureAwait(false);

        return new StateResponse
        {
            MatchId = request.MatchId,
            FromSequence = capture.SnapshotSequence >= 0
                ? capture.SnapshotSequence
                : request.AfterSequence,
            CurrentSequence = capture.CurrentSequence,
            ConfigHash = capture.ConfigHash,
            IsTerminal = session.IsTerminal,
            Status = session.IsTerminal ? "Terminal" : "Running",
            SnapshotJson = capture.SnapshotJson,
            Events = MatchEventDtoFactory.From(capture.Events),
        };
    }

    /// <summary>Yonetici komutu. 07 §5'in tam yolu.</summary>
    public async Task<CommandAck> SendCommand(SendCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownership = await CheckOwnershipAsync(request.MatchId).ConfigureAwait(false);

        if (!ownership.IsAllowed)
        {
            return new CommandAck
            {
                CommandId = request.CommandId,
                Accepted = false,
                Applied = false,
                Epoch = 0,
                Error = ownership.Error,
            };
        }

        var command = new DreamTeam.MatchEngine.Commands.ScheduledManagerCommand
        {
            CommandId = request.CommandId,
            Side = request.Side,
            Kind = request.Kind,
            Payload = request.Payload,
            AcceptedOrder = 0,          // Istemci YAZAMAZ; motor atar.
            TargetBoundary = request.TargetBoundary,
            ExpectedSequence = request.ExpectedSequence,
        };

        var result = await _send
            .ExecuteAsync(request.MatchId, command, Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return new CommandAck
            {
                CommandId = request.CommandId,
                Accepted = false,
                Applied = false,
                Epoch = 0,
                Error = result.Error,
            };
        }

        return new CommandAck
        {
            CommandId = request.CommandId,
            Accepted = result.Value!.Accepted,
            Applied = result.Value.Applied,
            Epoch = result.Value.Epoch,
            Error = result.Value.Error,
        };
    }

    /// <summary>Yayina abone ol. Yalniz KENDI macina.</summary>
    public async Task Subscribe(Guid matchId)
    {
        var ownership = await CheckOwnershipAsync(matchId).ConfigureAwait(false);

        if (!ownership.IsAllowed)
        {
            throw Forbidden(ownership.Error);
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId, SignalRMatchBroadcaster.GroupFor(matchId), Context.ConnectionAborted)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sahiplik denetimi. Iki YOL denenir:
    /// <list type="number">
    ///   <item><description><b>Canli oturum.</b> Sahiplik oturumda kayitlidir
    ///   ve okumasindadir; hizlidir.</description></item>
    ///   <item><description><b>Kalici kayit.</b> Oturum yoksa veritabanina
    ///   bakilir. Sunucu yeniden baslamis olabilir; mac kaydi duruyordur ve
    ///   sahibi degismis olamaz.</description></item>
    /// </list>
    ///
    /// <para><b>NEDEN BURADA VE USE CASE'TE IKI KEZ?</b> Bu, hub'in
    /// <i>bilgi</i> katmani: kullaniciya ne donulecegini belirler. Gercek
    /// karar <c>SendManagerCommand</c>'da verilir ve motora yalniz oradan
    /// girilir. Burada sadece daha iyi bir hata mesaji uretiyoruz.</para>
    /// </summary>
    private async Task<Ownership> CheckOwnershipAsync(Guid matchId)
    {
        var session = _sessions.Find(matchId);

        if (session is not null)
        {
            return session.OwnerUserId == _current.UserId
                ? Ownership.Allowed
                : Ownership.Denied("Bu mac size ait degil.");
        }

        var record = await _matches.FindAsync(matchId, Context.ConnectionAborted).ConfigureAwait(false);

        if (record is null)
        {
            return Ownership.Denied($"Mac bulunamadi: {matchId}.");
        }

        return record.OwnerUserId == _current.UserId
            ? Ownership.Allowed
            : Ownership.Denied("Bu mac size ait degil.");
    }

    /// <summary>
    /// 403. SignalR'in hub hatalari istemciye <c>Completion</c> mesaji olarak
    /// gider; kod ve mesaj birlikte. Mesaj kullaniciya gosterilir, bu yuzden
    /// ic sistem ayrintisi (SQL, iz) ICERMEZ.
    /// </summary>
    private static HubException Forbidden(string? reason) =>
        new HubException($"403: {reason}");

    private readonly record struct Ownership(bool IsAllowed, string? Error)
    {
        public static Ownership Allowed => new(true, null);

        public static Ownership Denied(string error) => new(false, error);
    }
}

/// <summary>M7: yeniden baglanma istegi.</summary>
public sealed record StateRequest
{
    public required Guid MatchId { get; init; }

    /// <summary>Istemcinin elinde olan son sequence.</summary>
    public required long AfterSequence { get; init; }

    /// <summary>Snapshot isteniyor mu. Uzun maclarda gerekli.</summary>
    public bool IncludeSnapshot { get; init; }
}
