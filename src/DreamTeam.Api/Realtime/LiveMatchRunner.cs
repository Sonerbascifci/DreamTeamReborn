using System.Collections.Concurrent;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.UseCases;
using DreamTeam.Api.Realtime;
using DreamTeam.Infrastructure;
using DreamTeam.MatchEngine.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DreamTeam.Api.Realtime;

/// <summary>
/// M7: canli mac yurutucusu.
///
/// <para><b>MAC BASINA AYRI GOREV.</b> Tek bir dongu tum maclari gezseydi bir
/// macin 2 saniyelik beklemesi diger maclari bekletirdi. Bu, kullanici
/// gorur ("rakip macim dondu") ve ayri bir hata sinifidir. Bu yuzden her mac
/// kendi <c>Task</c>'ini alir ve <b>kendi saatini kendi yonetir</b>.</para>
///
/// <para><b>DUZ DONGU YOK.</b> Her adim: motor <c>Advance</c> -> eventleri
/// yayinla -> eventleri kalici yaz -> pacer bekle. Pacer beklemesi
/// <c>Advance</c> DONDUKTEN SONRA olur; motor hicbir zaman bekletilmez
/// (D109, D101).</para>
///
/// <para><b>HATA YUTULMAZ.</b> Mac hata verirse gorunur: oturum
/// <c>Aborted</c> olarak kaydedilir ve sebep yazilir (D113 politikasinin
/// aynisi). Sessizce duran bir mac, oyuncunun saatlerce bekledigi bir
/// hata olurdu.</para>
/// </summary>
public sealed class LiveMatchRunner : BackgroundService
{
    private readonly MatchSessionStore _sessions;
    private readonly IEventRepository _events;
    private readonly IMatchRepository _matches;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly CompleteMatch _complete;
    private readonly AbortOrphanedMatches _abort;
    private readonly ILogger<LiveMatchRunner> _log;

    private readonly ConcurrentDictionary<Guid, Task> _running = new();

    public LiveMatchRunner(
        MatchSessionStore sessions,
        IEventRepository events,
        IMatchRepository matches,
        IMatchBroadcaster broadcaster,
        CompleteMatch complete,
        AbortOrphanedMatches abort,
        ILogger<LiveMatchRunner> log)
    {
        _sessions = sessions;
        _events = events;
        _matches = matches;
        _broadcaster = broadcaster;
        _complete = complete;
        _abort = abort;
        _log = log;
    }

    public int RunningCount => _running.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // D113: sunucu AYAĞA KALKARKEN kalan maclari iptal et. Oturumlar
        // bellekteydi ve kayboldu; maclar kurtarilmaz, iptal edilir.
        var affected = await _abort.ExecuteAsync(stoppingToken).ConfigureAwait(false);

        if (affected > 0)
        {
            _log.LogWarning(
                "Sunucu acilisi: {Count} canli mac bulundu ve iptal edildi (D113). Kurtarma yok.",
                affected);
        }

        // Baslatilan her mac icin bir gorev devral.
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var matchId in _sessions.ActiveMatches)
            {
                if (_running.ContainsKey(matchId))
                {
                    continue;
                }

                var session = _sessions.Find(matchId);

                if (session is null)
                {
                    continue;
                }

                _running[matchId] = Task.Run(
                    () => DriveAsync(session, stoppingToken), CancellationToken.None);
            }

            try
            {
                await Task.Delay(200, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Kapanis: bekleyen maclar iptal edilir. D113.
        await AbortOnShutdownAsync().ConfigureAwait(false);
    }

    private async Task DriveAsync(MatchSession session, CancellationToken cancellationToken)
    {
        // Her mac KENDI saatini kendi yonetir. Paylasilan bir saat olsaydi
        // bir macin beklemesi digerini de yavaslatirdi.
        var pacer = new LivePacer(new SystemClock(), new SystemDelay());

        _log.LogInformation(
            "Mac {MatchId} canli basliyor (hedef {Target} ms, carpan {Speedup}).",
            session.MatchId, LivePacerOptions.DefaultSpeedup * 8 * 60 * 1000, LivePacerOptions.DefaultSpeedup);

        try
        {
            while (!session.IsTerminal && !cancellationToken.IsCancellationRequested)
            {
                var step = await session.StepAsync(cancellationToken).ConfigureAwait(false);

                if (step.Events.Length > 0)
                {
                    // Once YAYIN, sonra KALICI YAZ. Yayin basarisiz olursa
                    // event kaybolmaz; yeniden baglanan istemci veritabanindan
                    // alir (07 §8).
                    await _broadcaster
                        .PublishEventsAsync(session.MatchId, step.Events, cancellationToken)
                        .ConfigureAwait(false);

                    await _events
                        .AppendRangeAsync(session.MatchId, step.Events, cancellationToken)
                        .ConfigureAwait(false);
                }

                // 07 §5: "ACK = alindi/kuyruga girdi. Applied = state'e
                // islendi." Istemciye IKI AYRI bildirim gider: bu, Applied
                // bildirimi. ACK, komutun kabul edildigi anda donulmustur.
                foreach (var result in step.CommandResults)
                {
                    await _broadcaster
                        .PublishCommandResultAsync(
                            session.MatchId,
                            result.CommandId,
                            result.Applied,
                            result.Applied ? result.Message : $"Reddedildi: {result.Reason}",
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                // Pacer SADECE burada bekler; motor Advance icinde degil.
                await pacer
                    .WaitAfterStepAsync(step.State.Clock.ElapsedGameTimeMs, cancellationToken)
                    .ConfigureAwait(false);
            }

            await FinishAsync(session, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Sunucu kapaniyor. Bu macin KENDI kaydi iptal edilir; digerleri
            // AbortOnShutdownAsync toplu olarak yapar.
            await AbortSessionAsync(session, "Sunucu kapandi (D113).").ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // Gorunur hata: mac iptal edilir ve sebep yazilir. Sessizce
            // duran bir mac, oyuncunun saatlerce bekledigi bir hatadir.
            _log.LogError(error, "Mac {MatchId} yurutulurken hata; iptal ediliyor.", session.MatchId);

            await AbortSessionAsync(session, $"Yurutucu hatasi: {error.Message}").ConfigureAwait(false);
        }
        finally
        {
            _running.TryRemove(session.MatchId, out _);

            // TryRemove null dondurabilir (baska biri aldiysa). Dispose
            // yalnizca GERCEKTEN bize ait olan oturum icin cagrilir;
            // yalnizca biri baskasinin oturumunu kapatirdi.
            if (_sessions.Remove(session.MatchId, out var removed) && removed is not null)
            {
                removed.Dispose();
            }
        }
    }

    /// <summary>
    /// Terminal oturumu kalici hale getirir. Sonuc <b>motorun</b> durumundan
    /// gelir; istemciden hicbir sey alinmaz.
    /// </summary>
    private async Task FinishAsync(MatchSession session, CancellationToken cancellationToken)
    {
        var outcome = session.CompletedOutcome();

        var written = await _complete.ExecuteAsync(outcome, cancellationToken).ConfigureAwait(false);

        if (written)
        {
            _log.LogInformation(
                "Mac {MatchId} tamamlandi: {Home}-{Away} ({Status}), {Events} event.",
                session.MatchId, outcome.HomeScore, outcome.AwayScore, outcome.Status,
                outcome.Events.Length);
        }
        else
        {
            // Ikinci yazma denemesi: kosullu guncelleme 0 satir etkiledi.
            // Idempotency saglandi; no-op.
            _log.LogInformation("Mac {MatchId} zaten tamamlanmis; tekrar yazilmadi.", session.MatchId);
        }

        await _broadcaster
            .PublishEventsAsync(session.MatchId, outcome.Events, CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Hatada veya kapanista maci iptal eder. <b>Sessizce duran bir mac
    /// olmaz</b>: durum Aborted olur ve sebep yazilir.
    /// </summary>
    private async Task AbortSessionAsync(MatchSession session, string reason)
    {
        try
        {
            var outcome = session.AbortOutcome(reason);

            await _events
                .AppendRangeAsync(session.MatchId, outcome.Events, CancellationToken.None)
                .ConfigureAwait(false);

            var written = await _complete.ExecuteAsync(outcome, CancellationToken.None).ConfigureAwait(false);

            _log.LogWarning(
                "Mac {MatchId} iptal edildi (yazildi={Written}): {Reason}",
                session.MatchId, written, reason);
        }
        catch (Exception error)
        {
            // Iptal de basarisiz oldu. Bu en kotu durum: kayitta Running
            // kalir ve sunucu yeniden baslayinca D113 bunu temizler.
            _log.LogError(error, "Mac {MatchId} iptal edilemedi; kayit Running kaldi.", session.MatchId);
        }
    }

    private async Task AbortOnShutdownAsync()
    {
        var count = _sessions.Count;

        if (count == 0)
        {
            return;
        }

        var affected = await _abort.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        _log.LogWarning(
            "Sunucu kapaniyor: {Sessions} oturum kapatildi, {Total} mac iptal edildi (D113).",
            count, affected);
    }
}
