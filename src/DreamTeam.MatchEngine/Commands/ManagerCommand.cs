using System.Collections.Immutable;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Commands;

/// <summary>
/// Yonetici komutunun turu. 07 §5'teki zarfin <c>Type</c> alani.
/// </summary>
public enum ManagerCommandKind
{
    ChangeOffense,
    ChangeDefense,
    ChangePace,
    Substitute,
    RequestTimeout,
}

/// <summary>
/// Timeout turu (D83).
///
/// <para><b>Neden motor "sure" bilmiyor?</b> 20 saniyelik timeout bir
/// <i>duvar saati</i> kavramidir. Motor duvar saati tutmaz (03 §"Motor
/// <c>DateTime.Now</c>, sleep ... beklemez"), dolayisiyla motorda yalnizca
/// <b>tipi</b> ve bucaet etkisi vardir. Gercek 20 saniyelik sure canli
/// runner'da (M7) yasar.</para>
/// </summary>
public enum TimeoutKind
{
    /// <summary>Tam timeout. D84: takim basina 4 adet, son 2'si son 2 dakikada.</summary>
    Full,

    /// <summary>20 saniyelik timeout. D83: tip motorda, sure M7'de. Sayi D89'da.</summary>
    Short20,
}

/// <summary>
/// Bir komutun <b>uygulanacagi mantiksal sinir</b> (07 §6).
///
/// <para>Neden duvar saati degil? 07 §6: "Maçin 18. gerçek saniyesi gibi
/// duvar saati tabanli komut, tek basina replay girdisi DEGILDIR." Komut
/// mantiksal sinira baglanir; canli runner kullanici girdisini bu sinira
/// cevirir. Ayni komut listesi + ayni seed = ayni mac.</para>
/// </summary>
public enum CommandBoundary
{
    /// <summary>
    /// Bir sonraki aksiyon karari. Taktik ve tempo BURADA uygulanir
    /// (07 §6 madde 1): cozulmeye baslamis sutu geriye donuk degistirmez.
    /// </summary>
    ActionDecision,

    /// <summary>
    /// Duduk sonrasi dead ball. Substitution ve timeout BURADA uygulanir
    /// (07 §6 madde 2). Motor icinde bu, <c>HandleAfterPossession</c> hunisidir.
    /// </summary>
    DeadBall,

    /// <summary>Devre arasi. Tanim geregi dead ball.</summary>
    PeriodBreak,
}

/// <summary>
/// Komutun taraf bagimli payload'i.
///
/// <para><b>Neden ayri tip yerine union?</b> C#'ta duyarli union yok. Tek bir
/// kayit, <see cref="Kind"/> ile tutarli olmayan bir payload'i
/// <see cref="CommandValidator"/> tarafindan <c>InvalidPayload</c> ile reddeder.
/// Boylece "hangi komutta hangi alan dolu" sorusu tek yerde yanitlanir.</para>
/// </summary>
public sealed record CommandPayload
{
    /// <summary><see cref="ManagerCommandKind.ChangeOffense"/> icin.</summary>
    public Config.OffensiveTactic? OffensiveTactic { get; init; }

    /// <summary><see cref="ManagerCommandKind.ChangeDefense"/> icin.</summary>
    public Config.DefensiveTactic? DefensiveTactic { get; init; }

    /// <summary><see cref="ManagerCommandKind.ChangePace"/> icin.</summary>
    public Config.Pace? Pace { get; init; }

    /// <summary>
    /// <see cref="ManagerCommandKind.Substitute"/> icin sahaya GIRECEK oyuncu
    /// (D85). Zorunlu; motor tahmin etmez.
    /// </summary>
    public Guid? IncomingPlayerId { get; init; }

    /// <summary>
    /// <see cref="ManagerCommandKind.Substitute"/> icin sahadan CIKACAK oyuncu
    /// (D85). Zorunlu. Otomatik cikarma yalnizca foul-out yolunda vardir.
    /// </summary>
    public Guid? OutgoingPlayerId { get; init; }

    /// <summary><see cref="ManagerCommandKind.RequestTimeout"/> icin.</summary>
    public TimeoutKind? TimeoutKind { get; init; }

    public static CommandPayload ForOffense(Config.OffensiveTactic tactic) =>
        new() { OffensiveTactic = tactic };

    public static CommandPayload ForDefense(Config.DefensiveTactic tactic) =>
        new() { DefensiveTactic = tactic };

    public static CommandPayload ForPace(Config.Pace pace) =>
        new() { Pace = pace };

    public static CommandPayload ForSubstitution(Guid incoming, Guid outgoing) =>
        new() { IncomingPlayerId = incoming, OutgoingPlayerId = outgoing };

    public static CommandPayload ForTimeout(TimeoutKind kind) =>
        new() { TimeoutKind = kind };
}

/// <summary>
/// <b>Zamanlanmis</b> yonetici komutu (03 hedef sozlesmesinin adi).
///
/// <para><b>Idempotency (07 §5).</b> <see cref="CommandId"/> tekrarlanan
/// gonderimlerde degismez. Ayni <c>CommandId</c> ikinci kez gorulurse etki
/// TEKRARLANMAZ ve ayni sonuc doner.</para>
///
/// <para><b><see cref="AcceptedOrder"/> istemci tarafindan belirlenemez</b>
/// (07 §5). Motor, komutu kabul ettigi sirayi atar. Kuyruk bu alana gore
/// siralanir; <c>ImmutableDictionary</c> veya <c>HashSet</c> yineleme
/// sirasina bagli bir sonuc uretmez.</para>
///
/// <para><b>Bu tip <b>kuyrukta</b> yaşar, yani <c>MatchState</c>'in parcasidir.</b>
/// Uygulanamayan komut bir sonraki sinira kadar korunur; bu yuzden kayit
/// edilebilir ve replay edilebilir olmak ZORUNLUDUR.</para>
/// </summary>
public sealed record ScheduledManagerCommand
{
    /// <summary>Tekrar gonderimlerde degismeyen idempotency kimligi (07 §5).</summary>
    public required Guid CommandId { get; init; }

    /// <summary>Hedef takim. 07 §5: sahiplik buradan gelir; auth M7'de.</summary>
    public required TeamSide Side { get; init; }

    public required ManagerCommandKind Kind { get; init; }

    public required CommandPayload Payload { get; init; }

    /// <summary>
    /// Istegin hangi gorulen state'e gore yapildigi (07 §5). <c>null</c> ise
    /// stale kontrolu yapilmaz. Ustel degisme reddi: <c>ExpectedSequence</c>
    /// gonderildigi andaki <c>NextSequence</c>'den buyukse komut gecmis bir
    /// state'i hedefliyordur ve reddedilir.
    /// </summary>
    public long? ExpectedSequence { get; init; }

    /// <summary>Motorun attigi deterministik kabul sirasi. Istemci yazamaz.</summary>
    public required long AcceptedOrder { get; init; }

    public required CommandBoundary TargetBoundary { get; init; }

    /// <summary>
    /// Komut bu sinirdan sonra uygulanamaz ise sonlanir. <c>null</c> ise mac
    /// bitene kadar kuyrukta kalir. 07 §6: "queue/expire/reject policy acik
    /// olmali" — burada politika acikca yazilmis durumda.
    /// </summary>
    public long? ExpiresAfterSequence { get; init; }

    /// <summary>
    /// Komutun suresi doldu mu?
    ///
    /// <para><b>Suresi BITMIS bir pencere icin sonuc <c>evet</c> sayilir</b>
    /// (<c>&gt;=</c>). "After" kelimesi yorumlanabilir olsa da sonuc tutarli
    /// olmasi icin muhafazakar yorum secildi: pencere kapanmis ise komut
    /// zaten gecmis bir an hedefliyordur.</para>
    /// </summary>
    public bool IsExpiredAfter(long sequence) =>
        ExpiresAfterSequence is { } limit && sequence >= limit;

    public ScheduledManagerCommand WithOrder(long order) => this with { AcceptedOrder = order };

    public ScheduledManagerCommand WithBoundary(CommandBoundary boundary) =>
        this with { TargetBoundary = boundary };
}

/// <summary>Komut kuyrugunun değişmez anlık görüntüsü.</summary>
public static class CommandList
{
    public static readonly ImmutableArray<ScheduledManagerCommand> Empty = [];
}
