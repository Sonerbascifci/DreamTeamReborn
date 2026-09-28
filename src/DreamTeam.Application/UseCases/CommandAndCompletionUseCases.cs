using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Application.UseCases;

public sealed record CommandOutcome(
    bool Accepted,
    bool Applied,
    string? Error,
    long Epoch)
{
    public static CommandOutcome Acked(long epoch) => new(true, false, null, epoch);

    public static CommandOutcome Rejected(string error, long epoch) => new(false, false, error, epoch);

    public static CommandOutcome Applied1(long epoch) => new(true, true, null, epoch);
}

/// <summary>
/// M7: yonetici komutunun sunucudan gecisi. 07 §5'i harfi harfine izler.
///
/// <para><b>SIRA:</b></para>
/// <list type="number">
///   <item><description>Oturum var mi? (yoksa red)</description></item>
///   <item><description><b>Sahiplik.</b> Bu mac bu kullanicinin mi? 07 §5:
///   "AuthenticatedUserId baglanti/session'dan cozulur; client'in 'ben su
///   takimim' beyanina guvenilmez." Reddedilirse komut MOTORA GIRMEZ.</description></item>
///   <item><description><b>Idempotency.</b> 07 §5: "Ayni CommandId yeniden
///   gelirse ayni sonuc dondurulur." <c>UNIQUE (match_id, command_id)</c>
///   yazimi basarisiz olursa bu bir TEKRAR gonderimidir, yeni komut degil.</description></item>
///   <item><description>Kuyruga ekle. Motor bir sonraki adimda gorur.</description></item>
/// </list>
///
/// <para><b>ACK vs APPLIED.</b> 07 §5: "ACK = alindi/kuyruga girdi. Applied =
/// state'e islendi. Bu ikisini tek basari mesajinda karistirma." Burada donen
/// <c>Accepted=true, Applied=false</c> tam olarak ACK'tir. Applied, motor
/// adiminda ayrica bildirilir.</para>
/// </summary>
public sealed class SendManagerCommand
{
    private readonly MatchSessionStore _sessions;
    private readonly ICommandLogRepository _log;
    private readonly ICurrentUser _current;
    private readonly IMonotonicClock _clock;

    public SendManagerCommand(
        MatchSessionStore sessions,
        ICommandLogRepository log,
        ICurrentUser current,
        IMonotonicClock clock)
    {
        _sessions = sessions;
        _log = log;
        _current = current;
        _clock = clock;
    }

    public async Task<UseCaseResult<CommandOutcome>> ExecuteAsync(
        Guid matchId,
        ScheduledManagerCommand command,
        CancellationToken cancellationToken)
    {
        MatchIdGuard.Require(matchId);
        ArgumentNullException.ThrowIfNull(command);

        var session = _sessions.Find(matchId);

        if (session is null)
        {
            return UseCaseResult<CommandOutcome>.Fail(
                $"Mac canli degil: {matchId}. Sunucu yeniden baslamis veya mac bitmis olabilir.");
        }

        // 1) SAHIPLIK. Istemci "ben su takimim" diyemez; kimlik JWT'den gelir ve
        //    karsilastirma SUNUCUDA, oturumun kayitli sahibine karşı yapilır.
        //
        //    D112 (PvP yok) burada gorunur: macin TEK sahibi vardir ve o
        //    kullanici iki tarafi da yonetir. Bir baska kullanicinin komutu
        //    MOTORA GIRMEZ.
        if (session.OwnerUserId != _current.UserId)
        {
            return UseCaseResult<CommandOutcome>.Fail(
                "Bu mac size ait degil. Yetkisiz komut MOTORA GIRMEZ.");
        }

        // 2) IDEMPOTENCY. Tekrar gonderim ayni sonucu dondurur.
        var recorded = await _log.TryRecordAcceptedAsync(
                matchId,
                command.CommandId,
                session.OwnerUserId,
                command.Kind,
                command.TargetBoundary,
                DateTimeOffset.FromUnixTimeMilliseconds(_clock.ElapsedMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);

        if (!recorded)
        {
            return UseCaseResult<CommandOutcome>.Ok(CommandOutcome.Acked(session.Epoch));
        }

        // 3) KUYRUĞA. Istege bagli epoch denetimi: bayat istek durumu degistirmez.
        if (!session.TryEnqueueCommand(command))
        {
            await _log.RecordResultAsync(
                    matchId,
                    command.CommandId,
                    applied: false,
                    CommandRejectionReason.MatchAlreadyTerminal,
                    "Mac terminal durumda; komut kuyruga alinmadi.",
                    DateTimeOffset.FromUnixTimeMilliseconds(_clock.ElapsedMilliseconds),
                    cancellationToken)
                .ConfigureAwait(false);

            return UseCaseResult<CommandOutcome>.Ok(
                CommandOutcome.Rejected("Mac terminal durumda.", session.Epoch));
        }

        return UseCaseResult<CommandOutcome>.Ok(CommandOutcome.Acked(session.Epoch));
    }
}

/// <summary>
/// M7, 03: "Match completion idempotent olmali; ayni mac odulu yeniden
/// verilmemeli."
///
/// <para><b>Idempotency VERITABANI KISITIYLA saglanir</b>, <c>record.Equals</c>
/// ile degil. <c>IMatchRepository.TryCompleteAsync</c> <c>WHERE status =
/// 'Running'</c> kosullu gunceller; iki komsu istekten yalniz biri yazar.
/// D87 (bozuk record esitligi) bu yuzden M7'yi ENGELLEMEZ.</para>
/// </summary>
public sealed class CompleteMatch
{
    private readonly MatchSessionStore _sessions;
    private readonly IMatchRepository _matches;
    private readonly IEventRepository _events;

    public CompleteMatch(
        MatchSessionStore sessions,
        IMatchRepository matches,
        IEventRepository events)
    {
        _sessions = sessions;
        _matches = matches;
        _events = events;
    }

    /// <summary>
    /// Terminal oturumu kalici hale getirir. <c>false</c> donerse bu mac
    /// ZATEN tamamlanmistir; ikinci deneme bir sey YAPMADI.
    /// </summary>
    public async Task<bool> ExecuteAsync(SessionOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        // Eventler ONCE yazilir. Macin sonucu yazilirken eventler hazir
        // olmali; 03 "olay sirasi" kalicidir.
        await _events
            .AppendRangeAsync(outcome.State.Setup.MatchId, outcome.Events, cancellationToken)
            .ConfigureAwait(false);

        var record = await _matches
            .FindAsync(outcome.State.Setup.MatchId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return false;
        }

        return await _matches.TryCompleteAsync(
                record.Id,
                new MatchCompletion(
                    outcome.Status,
                    outcome.HomeScore,
                    outcome.AwayScore,
                    outcome.IsTie,
                    outcome.HomePossessions,
                    outcome.AwayPossessions,
                    outcome.ElapsedGameTimeMs,
                    outcome.PeriodsPlayed,
                    outcome.AbortReason,
                    DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// M7, D113: sunucu yeniden basladi. Kalan Running maclar Aborted.
///
/// <para><b>Bu bir KURTARMA DEGIL.</b> Oturumlar bellekteydi ve kayboldu.
/// Maclar iptal edilir, sebep yazilir, odul verilmez. Snapshot'tan devam etme
/// M7'de YOKTUR (D113).</para>
/// </summary>
public sealed class AbortOrphanedMatches
{
    private readonly MatchSessionStore _sessions;
    private readonly IMatchRepository _matches;

    public AbortOrphanedMatches(MatchSessionStore sessions, IMatchRepository matches)
    {
        _sessions = sessions;
        _matches = matches;
    }

    public const string Reason =
        "Sunucu yeniden basladi; mac surdurulmedi (D113). Kurtarma M7 kapsaminda degil.";

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var cleared = _sessions.ClearAll();
        var aborted = await _matches.AbortRunningAsync(Reason, cancellationToken).ConfigureAwait(false);
        return cleared + aborted;
    }
}
