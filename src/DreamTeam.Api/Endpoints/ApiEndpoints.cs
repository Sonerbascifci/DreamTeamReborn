using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DreamTeam.Application.Ids;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Application.UseCases;
using DreamTeam.Api.Contracts;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Replay;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DreamTeam.Api.Endpoints;

/// <summary>
/// M7: HTTP yuzeyi.
///
/// <para><b>BU KATMANIN KURALI YOKTUR.</b> Burada dogrulama, yetki veya
/// is mantigi olmaz. Endpoint'ler dogrudan bir use case cagirir ve sonucu
/// HTTP'ye cevirir. Bir kural burada olsaydi, ayni kural SignalR yolunda
/// eksik kalirdi; iki yol, iki kural, biri unutulmus hata.</para>
///
/// <para><b>SOZLESME: kimlik JWT'den gelir.</b> Hicbir endpoint
/// "hangi kullanici" bilgisini govdeden almaz (T19). <c>ICurrentUser</c>
/// middleware tarafindan doldurulur.</para>
///
/// <para><b>HATA SOZLESMESI.</b> Yanit govdesi asagidaki kayitlardan biri
/// ya da duz hata metnidir. Istemci bunu ayirt edebilmelidir.</para>
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// TUM endpoint'leri map eder ve <b>yetkiyi merkezi olarak uygular</b>.
    ///
    /// <para><b>NEDEN MERKEZI?</b> Ilk yazimda her endpoint'i elle
    /// <c>RequireAuthorization()</c> ile korudum. Iki kez unutuldu: biri
    /// derlenmedi (bulunamadigi icin yalnizca 404 verdi), digeri <c>group</c>
    /// kaldirilirken <b>sessizce korumasiz</b> kaldi. Elle koyulan bir
    /// koruma, <b>koymayi unutunca sessizce kaybolur</b>; bu en kotu
    /// hata turudur. Burada tek kural var ve varsayilan KAPALI.</para>
    ///
    /// <para><b>ISTISNALAR ACIKCA ISARETLENIR.</b> <c>[AllowAnonymous]</c>
    /// tasiyan endpoint'ler dokunulmaz. Yalnizca iki tane var: saglik
    /// ve jeton. Ikisi de veri sızdırmaz.</para>
    ///
    /// <para><b>BILINCLI SIRA KAYBI.</b> Yol bazli bu denetim
    /// <c>/api/</c> ile baslamayan endpoint'leri kapsamaz (SignalR hub'bu
    /// ayrica; hub'in kendi <c>[Authorize]</c> niteligi vardir).</para>
    /// </summary>
    public static void MapDreamTeamEndpoints(
        this IEndpointRouteBuilder routes,
        TokenOptions? tokenOptions = null)
    {
        routes.MapHealth();
        routes.MapToken(tokenOptions ?? new TokenOptions { AllowSelfRegistration = true });

        routes.MapPlayers();
        routes.MapTeams();

        routes.MapMatches(
            routes.ServiceProvider.GetService<IMonotonicClock>()
                ?? new Infrastructure.SystemClock(),
            routes.ServiceProvider.GetService<LivePacerOptions>() ?? new LivePacerOptions());

        // Yetki gruplarin USTUNDE: RequireAuthorization() her Map cagrisinda
        // gorunur ve unutulamaz. Merkezi metadata mutasyonu bu SDK surumunde
        // mumkun degil (Endpoint.Metadata salt okunur).
    }

    public static void MapHealth(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/health", () => Results.Ok(new
        {
            status = "ok",
            engine = EngineVersion.Current,
            rules = RulesIdentity.Current,
        }))
        .AllowAnonymous();
    }
    // -----------------------------------------------------------------
    // Kimlik
    // -----------------------------------------------------------------

    /// <summary>
    /// Jeton ister. <b>Gelistirme ve test disinda KAPALI.</b>
    ///
    /// <para>Neden acik bir uctan noktasi? M7'de parola, kayit ve e-posta
    /// yok (Q15, M10). Bu uctan nokta yalnizca testlerin ve gelistirmenin
    /// "kimlik gerekli" yollari denemesi icindir; uretimde
    /// <c>Auth:AllowSelfRegistration</c> <c>false</c> olmalidir ve kapaliyken
    /// uctan nokta 404 doner — <b>gizli degil, YOKTUR</b>.</para>
    /// </summary>
    public static void MapToken(
        this IEndpointRouteBuilder routes,
        TokenOptions options)
    {
        if (!options.AllowSelfRegistration)
        {
            routes.MapPost("/api/auth/token", () => Results.NotFound()).AllowAnonymous();
        }

        routes.MapPost("/api/auth/token", async (
            TokenRequest request,
            IUserRepository userStore,
            Infrastructure.Security.JwtTokenIssuer tokenIssuer,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName)
                || request.DisplayName.Length > CreatePlayer.MaxDisplayNameLength)
            {
                return Results.BadRequest(new ErrorResponse("Gecersiz kullanici adi."));
            }

            var userId = DerivedId.From("user", request.DisplayName.Trim());

            if (await userStore.FindAsync(userId, cancellationToken).ConfigureAwait(false) is null)
            {
                await userStore
                    .AddAsync(
                        new UserRecord(userId, request.DisplayName.Trim(), DateTimeOffset.UtcNow),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return Results.Ok(new TokenResponse(
                tokenIssuer.Issue(userId, request.DisplayName.Trim()),
                userId,
                tokenIssuer.ExpiresAtUtc));
        }).AllowAnonymous();
    }

    // -----------------------------------------------------------------
    // Oyuncu
    // -----------------------------------------------------------------

    public static void MapPlayers(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(string.Empty).RequireAuthorization();

        group.MapGet("/api/players", async (
            IPlayerRepository players,
            ICurrentUser current,
            CancellationToken cancellationToken) =>
        {
            var list = await players
                .ListForOwnerAsync(current.UserId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(list.Select(p => new PlayerResponse(
                p.Id, p.DisplayName, p.Position, p.Ratings)));
        }).RequireAuthorization();

        group.MapPost("/api/players", async (
            CreatePlayerRequest request,
            CreatePlayer create,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.IsDefined(request.Position))
            {
                return Results.BadRequest(new ErrorResponse($"Gecersiz mevki: {request.Position}."));
            }

            var result = await create
                .ExecuteAsync(request.DisplayName, request.Position, request.Ratings, cancellationToken)
                .ConfigureAwait(false);

            // 201 + Location: yeni kayit olustu. 400: girdi hatasi.
            // Ayrim onemli: 400 "dusur", 500 "bizim hatamiz".
            return result.Succeeded
                ? Results.Created($"/api/players/{result.Value!.Id}", result.Value)
                : Results.BadRequest(new ErrorResponse(result.Error!));
        });
    }

    // -----------------------------------------------------------------
    // Takim
    // -----------------------------------------------------------------

    public static void MapTeams(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(string.Empty).RequireAuthorization();

        group.MapGet("/api/teams", async (
            ITeamRepository teams,
            ICurrentUser current,
            CancellationToken cancellationToken) =>
        {
            var list = await teams
                .ListForOwnerAsync(current.UserId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(list);
        });

        group.MapPost("/api/teams", async (
            CreateTeamRequest request,
            ITeamRepository teams,
            ICurrentUser current,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || request.Name.Length > CreatePlayer.MaxDisplayNameLength)
            {
                return Results.BadRequest(new ErrorResponse("Gecersiz takim adi."));
            }

            var name = request.Name.Trim();
            var id = DerivedId.From("team", current.UserId.ToString(), name);

            if (await teams.FindAsync(id, cancellationToken).ConfigureAwait(false) is not null)
            {
                return Results.BadRequest(new ErrorResponse($"Bu isimde bir takiminiz var: {name}."));
            }

            var record = new TeamRecord(id, current.UserId, name, DateTimeOffset.UtcNow);

            await teams.AddAsync(record, cancellationToken).ConfigureAwait(false);

            return Results.Created($"/api/teams/{id}", record);
        });

        // Kadroya ekle. D111: kadro boyutu serbest.
        group.MapPost("/api/teams/{teamId:guid}/roster", async (
            Guid teamId,
            AddRosterEntryRequest request,
            ITeamRepository teams,
            IPlayerRepository players,
            ICurrentUser current,
            CancellationToken cancellationToken) =>
        {
            var team = await teams.FindAsync(teamId, cancellationToken).ConfigureAwait(false);

            if (team is null)
            {
                return Results.NotFound(new ErrorResponse($"Takim bulunamadi: {teamId}."));
            }

            if (team.OwnerUserId != current.UserId)
            {
                return Results.Forbid();
            }

            var player = await players.FindAsync(request.PlayerId, cancellationToken).ConfigureAwait(false);

            if (player is null)
            {
                return Results.BadRequest(new ErrorResponse($"Oyuncu bulunamadi: {request.PlayerId}."));
            }

            // D111: oyuncu KISI BASINA. Baskasinin oyuncusu kadroya girmez.
            if (player.OwnerUserId != current.UserId)
            {
                return Results.BadRequest(
                    new ErrorResponse("Bu oyuncu size ait degil; her kullanici kendi kopyasina sahiptir (D111)."));
            }

            await teams.AddRosterEntryAsync(teamId, request.PlayerId, cancellationToken)
                .ConfigureAwait(false);

            return Results.NoContent();
        });

        group.MapDelete("/api/teams/{teamId:guid}/roster/{playerId:guid}", async (
            Guid teamId,
            Guid playerId,
            ITeamRepository teams,
            ICurrentUser current,
            CancellationToken cancellationToken) =>
        {
            var team = await teams.FindAsync(teamId, cancellationToken).ConfigureAwait(false);

            if (team is null)
            {
                return Results.NotFound(new ErrorResponse($"Takim bulunamadi: {teamId}."));
            }

            if (team.OwnerUserId != current.UserId)
            {
                return Results.Forbid();
            }

            await teams.RemoveRosterEntryAsync(teamId, playerId, cancellationToken)
                .ConfigureAwait(false);

            return Results.NoContent();
        });

        group.MapGet("/api/teams/{teamId:guid}/roster", async (
            Guid teamId,
            ITeamRepository teams,
            ICurrentUser current,
            CancellationToken cancellationToken) =>
        {
            var team = await teams.FindAsync(teamId, cancellationToken).ConfigureAwait(false);

            if (team is null)
            {
                return Results.NotFound(new ErrorResponse($"Takim bulunamadi: {teamId}."));
            }

            if (team.OwnerUserId != current.UserId)
            {
                return Results.Forbid();
            }

            return Results.Ok(
                await teams.RosterPlayerIdsAsync(teamId, cancellationToken).ConfigureAwait(false));
        });

        // Lineup. D31: motor kurali, degistirilemez. 5 oyuncu.
        group.MapPut("/api/teams/{teamId:guid}/lineup", async (
            Guid teamId,
            SetLineupRequest request,
            SetLineup setLineup,
            CancellationToken cancellationToken) =>
        {
            var result = await setLineup
                .ExecuteAsync(teamId, request.PlayerIds, cancellationToken)
                .ConfigureAwait(false);

            return result.Succeeded
                ? Results.Ok(new LineupResponse(result.Value!))
                : Results.BadRequest(new ErrorResponse(result.Error!));
        });
    }

    // -----------------------------------------------------------------
    // Mac
    // -----------------------------------------------------------------

    public static void MapMatches(
        this IEndpointRouteBuilder routes,
        IMonotonicClock clock,
        LivePacerOptions pacer)
    {
        var group = routes.MapGroup(string.Empty).RequireAuthorization();

        group.MapPost("/api/matches", async (
            StartMatchRequest request,
            StartMatch start,
            CancellationToken cancellationToken) =>
        {
            var result = await start
                .ExecuteAsync(
                    request.HomeTeamId,
                    request.AwayTeamId,
                    request.Seed,
                    request.HomeLineup,
                    request.AwayLineup,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return Results.BadRequest(new ErrorResponse(result.Error!));
            }

            var value = result.Value!;

            return Results.Created($"/api/matches/{value.MatchId}", new StartMatchResponse(
                value.MatchId,
                value.Frozen.SetupDigest,
                value.Frozen.ConfigHash,
                value.Frozen.EngineVersion,
                value.Frozen.RulesVersion,
                // 09 §M7: canli mac 8 GERCEK dakika (D109).
                TargetWallClockSeconds: pacer.TargetWallClockMs / 1000,
                Speedup: pacer.Speedup));
        });

        // Reconnect. 07 §8.
        group.MapGet("/api/matches/{matchId:guid}", async (
            Guid matchId,
            MatchSessionStore sessions,
            IMatchRepository matches,
            IEventRepository events,
            ICurrentUser current,
            CancellationToken cancellationToken,

            // <b>Varsayilan degerler burada, parametrede degil.</b> Minimal
            // API, varsayilani olmayan deger turu parametreleri "zorunlu sorgu
            // parametresi" sayar ve istek bunlari gondermezse BOS GOVDELI
            // 400 doner. Istemcinin ilk cagrisinda bunlari bilmesi
            // beklenmez: sifirdan baslamak ve snapshot istememek makul bir
            // varsayilandir.
            //
            // Nullable alinip govde icinde COZULUR; boylece hem istege bagli
            // kalirlar hem de onceki zorunlu parametrelerden sonra gelirler.
            [FromQuery] long? afterSequence = null,
            [FromQuery] bool? includeSnapshot = null) =>
        {
            var from = afterSequence ?? 0L;
            var withSnapshot = includeSnapshot ?? false;
            var record = await matches.FindAsync(matchId, cancellationToken).ConfigureAwait(false);

            if (record is null)
            {
                return Results.NotFound(new ErrorResponse($"Mac bulunamadi: {matchId}."));
            }

            if (record.OwnerUserId != current.UserId)
            {
                return Results.Forbid();
            }

            var session = sessions.Find(matchId);

            if (session is not null)
            {
                var capture = await session
                    .CaptureForAsync(from, withSnapshot, cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(ToResponse(session, capture));
            }

            // Canli degil ama kayit var: kalici event akisindan oku.
            // Sunucu yeniden baslamis olabilir; mac terminal ise bu yol
            // kullanilir.
            var list = await events
                .ReadAfterAsync(matchId, from, maxCount: 5_000, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new StateResponse
            {
                MatchId = matchId,
                FromSequence = from,
                CurrentSequence = list.Count == 0 ? from : list[^1].Sequence,
                ConfigHash = record.ConfigHash,
                IsTerminal = record.Lifecycle != MatchLifecycle.Running,
                Status = record.Lifecycle.ToString(),
                SnapshotJson = null,
                Events = MatchEventDtoFactory.From(list),
            });
        });

        // Komut. SignalR yolu da var; ikisi de AYNI use case'i cagirir.
        group.MapPost("/api/matches/{matchId:guid}/commands", async (
            Guid matchId,
            SendCommandRequest request,
            SendManagerCommand send,
            CancellationToken cancellationToken) =>
        {
            if (request.MatchId != matchId)
            {
                return Results.BadRequest(new ErrorResponse(
                    "Govdedeki mac kimligi yol ile ayni olmali."));
            }

            var command = new DreamTeam.MatchEngine.Commands.ScheduledManagerCommand
            {
                CommandId = request.CommandId,
                Side = request.Side,
                Kind = request.Kind,
                Payload = request.Payload,
                AcceptedOrder = 0,
                TargetBoundary = request.TargetBoundary,
                ExpectedSequence = request.ExpectedSequence,
            };

            var result = await send
                .ExecuteAsync(matchId, command, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return Results.BadRequest(new ErrorResponse(result.Error!));
            }

            // 202: kabul edildi, henuz uygulanmadi. 07 §5: ACK != Applied.
            return Results.Accepted(value: new CommandAck
            {
                CommandId = request.CommandId,
                Accepted = result.Value!.Accepted,
                Applied = result.Value.Applied,
                Epoch = result.Value.Epoch,
                Error = result.Value.Error,
            });
        });
    }

    private static StateResponse ToResponse(MatchSession session, SessionCapture capture) => new()
    {
        MatchId = session.MatchId,
        // <b>FromSequence = "bu yanitta hangi sequence'ten SONRASINI verdim".</b>
        //
        // IKI KURAL, TEK KAYNAK:
        //   * Snapshot donduyse sinir SNAPSHOT'IN sequence'idir; donen
        //     eventler snapshot'tan sonrakilerdir.
        //   * Snapshot donmediyse sinir ISTEMCININ afterSequence'idir.
        //
        // CurrentSequence KULLANILMAZ; o "sunucu su an burada" demektir
        // ve ayri bir alanda zaten var. Ilk yazimda ikisi karistiriliyordu:
        // snapshot istendiginde olmadigi icin from=current donuyordu ve
        // istemci "bu yanitta 1'den basladim" bilgisini kaybediyordu.
        FromSequence = capture.SnapshotSequence >= 0
            ? capture.SnapshotSequence
            : capture.RequestedFrom,
        CurrentSequence = capture.CurrentSequence,
        ConfigHash = capture.ConfigHash,
        IsTerminal = session.IsTerminal,
        Status = session.IsTerminal ? "Terminal" : "Running",
        SnapshotJson = capture.SnapshotJson,
        Events = MatchEventDtoFactory.From(capture.Events),
    };
}

// =====================================================================
// Sözleşme kayıtları
// =====================================================================

/// <summary>M7: hata govdesi. Istemci bunu ayirt edebilmeli.</summary>
public sealed record ErrorResponse(string Error);

public sealed record TokenRequest(string DisplayName);

public sealed record TokenResponse(string AccessToken, Guid UserId, DateTimeOffset ExpiresAt);

public sealed record PlayerResponse(Guid Id, string DisplayName, Position Position, PlayerRatings Ratings);

public sealed record CreatePlayerRequest(string DisplayName, Position Position, PlayerRatings Ratings);

public sealed record CreateTeamRequest(string Name);

public sealed record AddRosterEntryRequest(Guid PlayerId);

public sealed record SetLineupRequest(IReadOnlyList<Guid> PlayerIds);

public sealed record LineupResponse(ImmutableArray<Guid> PlayerIds);

public sealed record StartMatchRequest(
    Guid HomeTeamId,
    Guid AwayTeamId,
    ulong Seed,
    IReadOnlyList<Guid> HomeLineup,
    IReadOnlyList<Guid> AwayLineup);

public sealed record StartMatchResponse(
    Guid MatchId,
    string SetupDigest,
    string ConfigHash,
    string EngineVersion,
    string RulesVersion,
    int TargetWallClockSeconds,
    double Speedup);

/// <summary>
/// M7: <c>Auth:AllowSelfRegistration</c>. Varsayilan <b>KAPALI</b>.
/// Kapaliyken <c>/api/auth/token</c> hicbir sey map etmez; endpoint 404 doner.
/// </summary>
public sealed class TokenOptions
{
    public const string SectionName = "Auth";

    public bool AllowSelfRegistration { get; set; }
}
