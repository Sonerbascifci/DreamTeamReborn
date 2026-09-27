using System.Collections.Immutable;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Commands;

/// <summary>
/// Bir komutun neden uygulanmadigini anlatan sebep kodu (07 §5 "reddetme sebebi
/// kaydedilmeli").
///
/// <para>Kodlar <b>kapalidir</b>: yeni bir sebep eklemek bu enum'a yeni bir
/// deger demektir, dolayisiyla API tüketicisi (M7 client) yeni kodlari fark
/// eder. Serbest metin kullanmaz; aciklamalar icin <c>CommandResult.Message</c>
/// vardir.</para>
/// </summary>
public enum CommandRejectionReason
{
    None = 0,

    /// <summary>Komut bu tur icin yasali degil (orn. live-ball'da substitution).</summary>
    NotADeadBallWindow,

    /// <summary>Gelen oyuncu kadroda degil.</summary>
    PlayerNotInRoster,

    /// <summary>Gelen oyuncu zaten sahada.</summary>
    PlayerAlreadyOnCourt,

    /// <summary>Gelen oyuncu foul-out olmus; geri gelemez (06 §7).</summary>
    PlayerFouledOut,

    /// <summary>Cikan oyuncu su an sahada degil.</summary>
    OutgoingNotOnCourt,

    /// <summary>Cikan oyuncu foul-out olmus; sahada olamaz.</summary>
    OutgoingFouledOut,

    /// <summary>Değişiklik sonucu beş yasal oyuncu vermiyor.</summary>
    ResultingLineupIllegal,

    /// <summary>Maç terminal durumda.</summary>
    MatchAlreadyTerminal,

    /// <summary><c>ExpectedSequence</c> gecmise ait.</summary>
    StaleSequence,

    /// <summary>Bu <c>CommandId</c> daha once islendi (T14).</summary>
    DuplicateCommand,

    /// <summary>Timeout butcesi tukendi (D84).</summary>
    TimeoutBudgetExhausted,

    /// <summary>Son iki dakika penceresi gerekli ama saat uygun degil (D84).</summary>
    TimeoutWindowNotOpen,

    /// <summary>Komutun siniri gecti; kuyruktan dusuruldu (07 §6 madde 4).</summary>
    Expired,

    /// <summary>Payload <see cref="ManagerCommandKind"/> ile tutarsiz.</summary>
    InvalidPayload,

    /// <summary>Tanim disi enum degeri (fixture/deserialize yolu).</summary>
    UnknownEnumValue,
}

/// <summary>
/// Bir komutun sonucu. <see cref="StepResult"/>'in parcasidir (03: "StepResult
/// yeni state, events, <b>command sonuclari</b> ve durum tasir").
///
/// <para><b>Reddedilme maci bitirmez</b> (07 §5). Yonetici hata yaptiginda mac
/// devam eder; yalniz sonuc raporlanir.</para>
/// </summary>
public sealed record CommandResult
{
    public required Guid CommandId { get; init; }

    public required ManagerCommandKind Kind { get; init; }

    public required TeamSide Side { get; init; }

    public required long AcceptedOrder { get; init; }

    /// <summary>
    /// Komutun uygulandigi mantiksal sinir. Reddedilen komutlarda bu, komutun
    /// <i>hedefledigi</i> sinirdir — neden reddedildigini yorumlamak icin
    /// gereklidir (07 §5).
    /// </summary>
    public required CommandBoundary Boundary { get; init; }

    public required bool Applied { get; init; }

    public required CommandRejectionReason Reason { get; init; }

    /// <summary>Reddedilme sebebinin insan-okunur acıklamasi. API'de gosterilir.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Komutun uygulandigi/birakildigi andaki <c>NextSequence</c>. Replay ve
    /// client senkronizasyonu icin (07 §8 M7+).
    /// </summary>
    public required long StateSequence { get; init; }

    public static CommandResult Applied1(
        ScheduledManagerCommand command,
        long stateSequence) => new()
        {
            CommandId = command.CommandId,
            Kind = command.Kind,
            Side = command.Side,
            AcceptedOrder = command.AcceptedOrder,
            Boundary = command.TargetBoundary,
            Applied = true,
            Reason = CommandRejectionReason.None,
            StateSequence = stateSequence,
        };

    public static CommandResult Rejected1(
        ScheduledManagerCommand command,
        CommandRejectionReason reason,
        long stateSequence,
        string? message = null) => new()
        {
            CommandId = command.CommandId,
            Kind = command.Kind,
            Side = command.Side,
            AcceptedOrder = command.AcceptedOrder,
            Boundary = command.TargetBoundary,
            Applied = false,
            Reason = reason,
            Message = message,
            StateSequence = stateSequence,
        };
}

/// <summary>Kuyruk ve sonuc koleksiyonlari icin kucuk yardimcilar.</summary>
public static class CommandResultHelpers
{
    public static ImmutableArray<CommandResult> Empty => [];

    public static CommandResult ToResult(this ScheduledManagerCommand command, long stateSequence) =>
        CommandResult.Applied1(command, stateSequence);

    public static CommandResult ToResult(
        this ScheduledManagerCommand command,
        CommandRejectionReason reason,
        long stateSequence,
        string? message = null) =>
        CommandResult.Rejected1(command, reason, stateSequence, message);
}
