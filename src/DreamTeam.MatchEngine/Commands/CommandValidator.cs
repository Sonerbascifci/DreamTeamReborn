using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Rules;

namespace DreamTeam.MatchEngine.Commands;

/// <summary>
/// Komut dogrulama. 07 §5'teki validation listesi: "sahiplik, mac lifecycle,
/// oyuncu uygunlugu, izinli tactic/pace, timeout hakki, stale-state politikasi,
/// rate limit ve payload schema."
///
/// <para><b>Saf ve deterministik.</b> Bu tip <b>RNG tüketmez</b>, <b>saat
/// okumaz</b>, <b>I/O yapmaz</b> ve <b>state degistirmez</b>. 07 §5 yalniz
/// gecersiz komutun RNG tüketmemesini sart kosuyor; biz bunu gecerli komut
/// icin de sart kiliyoruz, boylece M4'ün cagri sirasi sozlesmesi bozulmaz.</para>
///
/// <para><b>Iki asamali dogrulama (06 §7).</b> "Substitution istek kabulunde ve
/// uygulama aninda tekrar dogrulanir." Araya baska bir komut girebilecegi icin
/// kabul aninda dogrulanması yeterli degildir:
/// <list type="bullet">
///   <item><description><see cref="ValidateEnvelope"/> — kabul aninda: payload
///   tutarliligi, enum tanimliligi, kadro uyumu. Yanlissa komut <b>kuyruga
///   girmez</b>.</description></item>
///   <item><description><see cref="ValidateForApplication"/> — uygulama aninda:
///   sahada mi, foul-out mu, butce var mi, pencere acik mi. Yanlissa komut
///   kuyruktan dusurulur ve <b>reddedilir</b>.</description></item>
/// </list></para>
/// </summary>
public static class CommandValidator
{
    /// <summary>
    /// Komut turunun hangi mantiksal sinirlarda uygulanabilecegini dondurur.
    /// 07 §6: taktik/tempo aksiyon sinirinda; substitution/timeout dead-ball'da.
    /// </summary>
    public static bool BoundaryAllows(ManagerCommandKind kind, CommandBoundary boundary) =>
        kind switch
        {
            ManagerCommandKind.ChangeOffense
                or ManagerCommandKind.ChangeDefense
                or ManagerCommandKind.ChangePace => boundary == CommandBoundary.ActionDecision,

            ManagerCommandKind.Substitute
                or ManagerCommandKind.RequestTimeout => boundary is CommandBoundary.DeadBall
                    or CommandBoundary.PeriodBreak,

            _ => false,
        };

    /// <summary>Hangi siniri varsayilan olarak hedeflemeli.</summary>
    public static CommandBoundary DefaultBoundaryFor(ManagerCommandKind kind) => kind switch
    {
        ManagerCommandKind.ChangeOffense
            or ManagerCommandKind.ChangeDefense
            or ManagerCommandKind.ChangePace => CommandBoundary.ActionDecision,

        ManagerCommandKind.Substitute
            or ManagerCommandKind.RequestTimeout => CommandBoundary.DeadBall,

        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Bilinmeyen komut turu."),
    };

    /// <summary>
    /// Kabul anindaki dogrulama. Basariliysa <c>null</c>, aksi halde sebep kodu
    /// ve aciklama doner.
    /// </summary>
    public static (CommandRejectionReason Reason, string? Message)? ValidateEnvelope(
        ScheduledManagerCommand command,
        TeamMatchState team)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(team);

        if (!BoundaryAllows(command.Kind, command.TargetBoundary))
        {
            return (
                CommandRejectionReason.InvalidPayload,
                $"{command.Kind} turu {command.TargetBoundary} sinirinda uygulanamaz.");
        }

        var payload = command.Payload;

        switch (command.Kind)
        {
            case ManagerCommandKind.ChangeOffense:
                if (payload.OffensiveTactic is not { } offense)
                {
                    return (CommandRejectionReason.InvalidPayload, "ChangeOffense icin OffensiveTactic gerekli.");
                }

                if (!Enum.IsDefined(offense))
                {
                    return (CommandRejectionReason.UnknownEnumValue, $"Tanimsiz taktik: {(int)offense}.");
                }

                return null;

            case ManagerCommandKind.ChangeDefense:
                if (payload.DefensiveTactic is not { } defense)
                {
                    return (CommandRejectionReason.InvalidPayload, "ChangeDefense icin DefensiveTactic gerekli.");
                }

                if (!Enum.IsDefined(defense))
                {
                    return (CommandRejectionReason.UnknownEnumValue, $"Tanimsiz savunma: {(int)defense}.");
                }

                return null;

            case ManagerCommandKind.ChangePace:
                if (payload.Pace is not { } pace)
                {
                    return (CommandRejectionReason.InvalidPayload, "ChangePace icin Pace gerekli.");
                }

                if (!Enum.IsDefined(pace))
                {
                    return (CommandRejectionReason.UnknownEnumValue, $"Tanimsiz tempo: {(int)pace}.");
                }

                return null;

            case ManagerCommandKind.Substitute:
                if (payload.IncomingPlayerId is not { } incoming || payload.OutgoingPlayerId is not { } outgoing)
                {
                    return (
                        CommandRejectionReason.InvalidPayload,
                        "Substitute icin hem IncomingPlayerId hem OutgoingPlayerId gerekli (D85).");
                }

                if (incoming == outgoing)
                {
                    return (
                        CommandRejectionReason.InvalidPayload,
                        "Ayni oyuncu hem girip hem cikamaz.");
                }

                if (FindInRoster(team, incoming) is null)
                {
                    return (
                        CommandRejectionReason.PlayerNotInRoster,
                        $"Girecek oyuncu kadroda degil: {incoming} ({team.Side}).");
                }

                if (FindInRoster(team, outgoing) is null)
                {
                    return (
                        CommandRejectionReason.PlayerNotInRoster,
                        $"Cikacak oyuncu kadroda degil: {outgoing} ({team.Side}).");
                }

                return null;

            case ManagerCommandKind.RequestTimeout:
                if (payload.TimeoutKind is not { } timeoutKind)
                {
                    return (CommandRejectionReason.InvalidPayload, "RequestTimeout icin TimeoutKind gerekli.");
                }

                if (!Enum.IsDefined(timeoutKind))
                {
                    return (CommandRejectionReason.UnknownEnumValue, $"Tanimsiz timeout turu: {(int)timeoutKind}.");
                }

                return null;

            default:
                return (CommandRejectionReason.UnknownEnumValue, $"Bilinmeyen komut turu: {command.Kind}.");
        }
    }

    /// <summary>
    /// Uygulama anindaki dogrulama. 06 §7'nin ikinci dogrulamasi.
    /// </summary>
    /// <param name="isDeadBallWindow">
    /// Mevcut an yasal bir dead-ball penceresi mi. <c>PeriodBreak</c> sinirinda
    /// her zaman <c>true</c>'dur.
    /// </param>
    /// <param name="isFinalTwoMinutes">
    /// Duzenleme periyodunun son iki dakikasinda mi (D84).
    /// </param>
    public static (CommandRejectionReason Reason, string? Message)? ValidateForApplication(
        ScheduledManagerCommand command,
        TeamMatchState team,
        RulesProfile rules,
        MatchClock clock,
        bool isDeadBallWindow,
        bool isFinalTwoMinutes)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(rules);

        var envelope = ValidateEnvelope(command, team);

        if (envelope is not null)
        {
            return envelope;
        }

        switch (command.Kind)
        {
            case ManagerCommandKind.Substitute:
            {
                if (!isDeadBallWindow)
                {
                    // D82: normal basket sonrasi pencere ACILMAZ. DREB sonrasi
                    // oyun canlidir, o da reddedilir.
                    return (
                        CommandRejectionReason.NotADeadBallWindow,
                        "Substitution yalniz yasal dead-ball penceresinde uygulanir (D82).");
                }

                var incoming = command.Payload.IncomingPlayerId!.Value;
                var outgoing = command.Payload.OutgoingPlayerId!.Value;

                if (IsOnCourt(team, incoming))
                {
                    return (CommandRejectionReason.PlayerAlreadyOnCourt, "Girecek oyuncu zaten sahada.");
                }

                if (team.FoulOutPlayerIds.Contains(incoming))
                {
                    // 06 §7: "foul-out sonrasi geri gelmesi engellenir"
                    return (CommandRejectionReason.PlayerFouledOut, "Foul-out olan oyuncu geri giremez.");
                }

                if (!IsOnCourt(team, outgoing))
                {
                    return (CommandRejectionReason.OutgoingNotOnCourt, "Cikacak oyuncu sahada degil.");
                }

                // D53: sonuc DAIMA bes yasal oyuncu olmali; hicbir kesme veya
                // sinirlama yok.
                var projected = SubstitutionPolicy.ProjectLineup(team, incoming, outgoing);

                if (!SubstitutionPolicy.IsLegalLineup(team, projected))
                {
                    return (CommandRejectionReason.ResultingLineupIllegal, "Besi yasal oyuncuya indirgemiyor.");
                }

                return null;
            }

            case ManagerCommandKind.RequestTimeout:
            {
                if (!isDeadBallWindow)
                {
                    return (
                        CommandRejectionReason.NotADeadBallWindow,
                        "Timeout yalniz yasal dead-ball penceresinde uygulanir (D84 sapmasi).");
                }

                var kind = command.Payload.TimeoutKind!.Value;

                if (!TimeoutPolicy.CanSpend(team, rules, clock, kind, isFinalTwoMinutes))
                {
                    return (
                        CommandRejectionReason.TimeoutBudgetExhausted,
                        TimeoutPolicy.DescribeRefusal(team, rules, clock, kind, isFinalTwoMinutes));
                }

                return null;
            }

            // Taktik/tempo: penceresi, butcesi veya oyuncu gereksinimi YOKTUR.
            // Zarf dogrulamasi (yukarida) tek kontroldur ve basarili oldu.
            //
            // D90: bu dal once "InvalidOperationException" idi. Bu bir M5
            // hatasiydi: ActionDecision sinirinda taktik komutlari her zaman
            // buraya girdigi icin TUM taktik komutlari istisna firlatiyordu.
            // Sessizce gecmek yerine acikca "bu turde ek kural yok" demek
            // dogru davranistir; tanimsiz turun ise hata vermeye devam eder.
            case ManagerCommandKind.ChangeOffense:
            case ManagerCommandKind.ChangeDefense:
            case ManagerCommandKind.ChangePace:
                return null;

            default:
                throw new InvalidOperationException(
                    $"{command.Kind} turu icin uygulama dogrulamasi tanimsiz. "
                    + "Yeni bir komut turu eklendiyse burasi da guncellenmelidir.");
        }
    }

    private static Player? FindInRoster(TeamMatchState team, Guid playerId)
    {
        foreach (var player in team.Roster)
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return null;
    }

    private static bool IsOnCourt(TeamMatchState team, Guid playerId)
    {
        foreach (var player in team.OnCourt)
        {
            if (player.Id == playerId)
            {
                return true;
            }
        }

        return false;
    }
}
