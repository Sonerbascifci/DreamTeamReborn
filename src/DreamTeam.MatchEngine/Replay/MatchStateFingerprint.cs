using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Randomness;
using DreamTeam.MatchEngine.Rules;

namespace DreamTeam.MatchEngine.Replay;

/// <summary>
/// D87: durum esitligi guvenilir DEGILDIR.
///
/// <para><b>Bu bir performans araci degil, bir tuzaktir.</b> Motorun cekirdek
/// durum tipleri <see cref="ImmutableArray{T}"/> icerir ve
/// <c>ImmutableArray&lt;T&gt;.Equals</c> <b>REFERANS</b> esitligi kullanir.
/// <c>record</c> ureticisi bu yuzden <c>Team</c>, <c>TeamMatchSetup</c>,
/// <c>MatchSetup</c> ve <c>MatchState</c> icin bozuktur. Bu olcum bu oturumda
/// YAPILDI:</para>
/// <list type="bullet">
///   <item><description>Ayni id + ayni roster, AYRI orneklenmis iki <c>Team</c>:
///   <c>Equals</c> = <b>False</b>.</description></item>
///   <item><description><c>ImmutableArray</c> icermeyen <c>Player</c>:
///   <c>Equals</c> = <b>True</b> (sorunun kaynagi bu).</description></item>
///   <item><description><c>Team</c> JSON round-trip bayt ayni, ama
///   <c>Equals</c> yine <b>False</b>.</description></item>
/// </list>
///
/// <para><b>Sonuc.</b> T16 ("snapshot serialize/restore -> kesintisiz kosuyla
/// ayni devam") <c>Assert.Equal(state, restored)</c> ile test <b>EDILEMEZ</b>:
/// dogru kod <c>False</c> doner (yanlis negatif), ayni referansi paylasan iki
/// farkli kod <c>True</c> doner (yanlis pozitif). Bu yuzden M5'te butun durum
/// karsilastirmalari bu parmak izini kullanir.</para>
///
/// <para><b>M7 notu.</b> 03 §"Match completion idempotent" bir <b>kalici
/// katman</b> esitligi gerektirir. Bu sorunun kalici cozumu M7'nindir; burada
/// yalniz test/replay yuzeyi kapatilir.</para>
/// </summary>
public static class MatchStateFingerprint
{
    /// <summary>
    /// Durumun alan alan, sirali bir metin parmak izi. Ayni durum icin
    /// <b>her zaman ayni</b> metni uretir.
    /// </summary>
    public static string Of(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var builder = new StringBuilder(4096);

        builder.Append("clock|").Append(state.Clock.Period).Append('|')
            .Append(state.Clock.GameClockMs).Append('|')
            .Append(state.Clock.ShotClockMs).Append('|')
            .Append(state.Clock.ElapsedGameTimeMs).Append('\n');

        builder.Append("phase|").Append(state.Phase).Append('\n');
        builder.Append("score|").Append(state.HomeScore).Append('|').Append(state.AwayScore).Append('\n');

        builder.Append("counters|").Append(state.NextSequence).Append('|')
            .Append(state.NextActionId).Append('|')
            .Append(state.NextShotId).Append('|')
            .Append(state.NextTurnoverId).Append('|')
            .Append(state.NextFoulId).Append('|')
            .Append(state.NextFTSeriesId).Append('|')
            .Append(state.TotalActionCount).Append('|')
            .Append(state.PossessionCount).Append('\n');

        builder.Append("possession|").Append(state.Possession?.PossessionId.ToString() ?? "-").Append('|')
            .Append(state.Possession?.Offense.ToString() ?? "-").Append('|')
            .Append(state.Possession?.ActionCount.ToString(CultureInfo.InvariantCulture) ?? "-").Append('|')
            .Append(state.Possession?.ShotCount.ToString(CultureInfo.InvariantCulture) ?? "-").Append('\n');

        AppendTeam(builder, "home", state.Home);
        AppendTeam(builder, "away", state.Away);

        builder.Append("pendingShot|").Append(state.PendingShot is null ? "-" : "yes").Append('\n');

        if (state.PendingShot is { } shot)
        {
            builder.Append("  ").Append(shot.ActionId).Append('|')
                .Append(shot.ShotId).Append('|')
                .Append(shot.FoulId).Append('|')
                .Append(shot.FoulType).Append('|')
                .Append(shot.ShooterId).Append('|')
                .Append(shot.PrimaryDefenderId).Append('|')
                .Append(shot.ShotType).Append('|')
                .Append(shot.SkillRating).Append('|')
                .Append(shot.Quality).Append('|')
                .Append(shot.ShooterEnergy.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(shot.RimContact).Append('\n');
        }

        builder.Append("pendingFrees|").Append(state.PendingFrees is null ? "-" : "yes").Append('\n');

        if (state.PendingFrees is { } frees)
        {
            builder.Append("  ").Append(frees.FTSeriesId).Append('|')
                .Append(frees.ShooterId).Append('|')
                .Append(frees.Count).Append('|')
                .Append(frees.Index).Append('|')
                .Append(frees.FinalShotIsLive).Append('|')
                .Append(frees.Offense).Append('|')
                .Append(frees.PossessionId).Append('|')
                .Append(frees.FieldGoalWasCounted).Append('\n');
        }

        // RNG state. Simulation replay'in butunlugu buna baglidir.
        Span<byte> rngState = stackalloc byte[SeededRandom.StateSizeInBytes];
        state.Random.GetState(rngState);
        builder.Append("rng|").Append(Convert.ToHexString(rngState)).Append('\n');

        // Komut kuyrugu: sirasi ONEMLIDIR (D86). Kuyruk kaybolursa replay
        // bozulur, bu yuzden parmak izine girer.
        builder.Append("commandQueue|next=").Append(state.CommandQueue.NextOrder)
            .Append("|pending=").Append(state.CommandQueue.PendingCount)
            .Append("|settled=").Append(state.CommandQueue.Settled.Count).Append('\n');

        foreach (var command in state.CommandQueue.Pending)
        {
            builder.Append("  cmd|").Append(command.AcceptedOrder).Append('|')
                .Append(command.CommandId).Append('|')
                .Append(command.Side).Append('|')
                .Append(command.Kind).Append('|')
                .Append(command.TargetBoundary).Append('\n');
        }

        // M6 (D100): diagnostics sayaclari. Snapshot round-trip'inin bu alani
        // tasidigini dogrulamak icin parmak izine giriyor. T15 bunlarin domain
        // sonucunu DEGISTIRMEDIGini ayrica test eder.
        builder.Append("diagnostics|").Append(state.Diagnostics.ToReport());

        return builder.ToString();
    }

    /// <summary>
    /// Event akisinin parmak izi. Mevcut <c>M2TestData.Fingerprint</c> ile ayni
    /// amaci gorur; replay testleri bunu kullanir.
    /// </summary>
    public static string OfEvents(IReadOnlyList<MatchEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var builder = new StringBuilder(events.Count * 96);

        foreach (var matchEvent in events)
        {
            builder.Append(matchEvent.Sequence).Append('|')
                .Append(matchEvent.Type).Append('|')
                .Append(matchEvent.Period).Append('|')
                .Append(matchEvent.GameClockMs).Append('|')
                .Append(matchEvent.ElapsedGameTimeMs).Append('|')
                .Append(matchEvent.PossessionId?.ToString() ?? "-").Append('|')
                .Append(matchEvent.ActionId?.ToString() ?? "-").Append('|')
                .Append(matchEvent.TeamId?.ToString() ?? "-").Append('|')
                .Append(matchEvent.PlayerId?.ToString() ?? "-").Append('|')
                .Append(matchEvent.SecondaryPlayerId?.ToString() ?? "-").Append('|')
                .Append(matchEvent.Payload?.GetType().Name ?? "-").Append('|')
                .Append(PayloadSummary.Of(matchEvent.Payload)).Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendTeam(StringBuilder builder, string label, TeamMatchState team)
    {
        builder.Append(label).Append("|").Append(team.Side).Append('|')
            .Append(team.OffensiveTactic).Append('|')
            .Append(team.DefensiveTactic).Append('|')
            .Append(team.Pace).Append('|')
            .Append(team.FullTimeoutsUsed).Append('|')
            .Append(team.ShortTimeoutsUsed).Append('|')
            .Append(team.Fouls.Personal.Length).Append('|')
            .Append(team.Fouls.TeamFoulsThisPeriod).Append('|')
            .Append(team.FoulOutPlayerIds.Length).Append('\n');

        builder.Append(label).Append(".oncourt|");

        foreach (var player in team.OnCourt)
        {
            builder.Append(player.Id).Append(',');
        }

        builder.Append('\n');

        builder.Append(label).Append(".states|");

        foreach (var playerState in team.PlayerStates)
        {
            builder.Append(playerState.PlayerId).Append('=')
                .Append(playerState.Energy.ToString("R", CultureInfo.InvariantCulture)).Append('=')
                .Append(playerState.SecondsOnCourt.ToString("R", CultureInfo.InvariantCulture))
                .Append(';');
        }

        builder.Append('\n');
    }
}

/// <summary>
/// Payload ozeti. Amac <b>tam serilestirme degil</b>, parmak izinin iki
/// farkli kod yolunu AYIRT etmesidir; bu yuzden yalnizca ayirt edici alanlar
/// yazilir.
/// </summary>
internal static class PayloadSummary
{
    public static string Of(object? payload) => payload switch
    {
        null => "-",
        ShotAttemptPayload shot => $"s{shot.ShotId}:{shot.ShotType}:q{shot.Quality}:e{shot.ShooterEnergy}",
        ShotMadePayload made => $"s{made.ShotId}:{made.ShotType}:{made.Points}:{made.CountsAsFieldGoalAttempt}",
        ShotMissedPayload missed => $"s{missed.ShotId}:{missed.ShotType}:{missed.CountsAsFieldGoalAttempt}",
        BlockPayload block => $"s{block.ShotId}:{block.DefenderId}",
        ReboundPayload rebound => $"s{rebound.ShotId}:o{rebound.Offensive}:t{rebound.IsTeamRebound}",
        FoulPayload foul => $"f{foul.FoulId}:{foul.Type}:{foul.FreeThrowCount}",
        TurnoverPayload turnover => $"t{turnover.TurnoverId}:{turnover.Kind}",
        ActionCompletedPayload action => $"a{action.Action}",
        TacticChangedPayload tactic => $"{tactic.CommandId}:{tactic.Previous}>{tactic.Current}",
        DefenseChangedPayload defense => $"{defense.CommandId}:{defense.Previous}>{defense.Current}",
        PaceChangedPayload pace => $"{pace.CommandId}:{pace.Previous}>{pace.Current}",
        SubstitutionPayload sub => $"{sub.CommandId}:in{sub.IncomingPlayerId}:out{sub.OutgoingPlayerId}",
        TimeoutPayload timeout => $"{timeout.CommandId}:{timeout.Kind}:{timeout.FullUsed}/{timeout.ShortUsed}",
        CommandAppliedPayload applied => $"ok:{applied.CommandId}:{applied.Kind}",
        CommandRejectedPayload rejected => $"no:{rejected.CommandId}:{rejected.Reason}",
        FreeThrowAttemptPayload attempt => $"f{attempt.FTSeriesId}:{attempt.Index}/{attempt.Count}",
        _ => payload.GetType().Name,
    };
}
