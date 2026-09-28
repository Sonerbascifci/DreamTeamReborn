using System.Text.Json;
using System.Text.Json.Serialization;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.Api.Contracts;

/// <summary>
/// M7: <c>MatchEvent</c> -&gt; istemciye giden DTO.
///
/// <para><b>NEDEN DTO?</b> SignalR'in JSON protokolu, alani <b>bildirilen</b>
/// tipe gore serilestirir. <c>MatchEvent.Payload</c> soyut bir
/// <c>MatchEventPayload</c> olarak bildiriliyor ve <c>[JsonDerivedType]</c>
/// isareti yok; dogrudan gonderilseydi istemciye yalniz taban alanlari
/// giderdi ve her sey kaybolurdu.</para>
///
/// <para><b>Motor NEDEN DEGISMEDI?</b> Motor saf kalmali (05 §14) ve
/// <c>MatchEvent</c> M3'ten beri kalici bir sozlesme. Serilestirme
/// karari SUNUCUNUN sorumlulugudur, motorun degil.</para>
///
/// <para><b>AYIRICI (discriminator) KORUNUR.</b> <see cref="Type"/> hem
/// sunucu hem istemci tarafinda zorunludur. Istemci payload'i cozebilmek
/// icin once tipi bilmelidir; bu motorun zaten soyledigi
/// "payload tipi type ile eslesmelidir" kuralinin aynisi.</para>
/// </summary>
public sealed record MatchEventDto
{
    public required Guid MatchId { get; init; }

    public required long Sequence { get; init; }

    public required int SchemaVersion { get; init; }

    public required string EngineVersion { get; init; }

    public required string ConfigHash { get; init; }

    /// <summary>Ayirici. Istemci payload tipini buna bakarak cozer.</summary>
    public required MatchEventType Type { get; init; }

    public required int Period { get; init; }

    public required long GameClockMs { get; init; }

    public required long ElapsedGameTimeMs { get; init; }

    public int? PossessionId { get; init; }

    public long? ActionId { get; init; }

    public TeamSide? TeamId { get; init; }

    public Guid? PlayerId { get; init; }

    public Guid? SecondaryPlayerId { get; init; }

    /// <summary>
    /// Payload, <b>gercek</b> tipiyle serilestirilmis. JSON'da duz bir
    /// nesnedir; ic ice gomulmez.
    /// </summary>
    public required JsonElement Payload { get; init; }
}

/// <summary>
/// M7: DTO donusumu. Iki yonlu DEGILDIR: istemciye gondeririz, okumayiz.
/// Istemciden gelen event'e guvenilmez (03: "Client yalniz komut gonderir").
/// </summary>
public static class MatchEventDtoFactory
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        // <b>ADLAMA KURALI: ZARF VE PAYLOAD AYNI OLMAK ZORUNDA.</b>
        //
        // Zarf camelCase idi, payload ise PascalCase idi. Istemci bir alani
        // sequence, digerini HomeScore olarak gormek zorunda kaliyordu. Iki
        // bicim bir arada sozlesmeyi kirar ve "bu alan neden farkli" sorusu
        // her yeni istemciye tekrar sorulur. Simdi ikisi de camelCase.
        //
        // NOT: veritabanindaki JSONB FARKLIDIR ve PascalCase kalir; o ic
        // bir bicimdir ve C# tipini yansitir. Tel sozlesmesi disaridir.
        // Ikisinin neden farkli oldugu burada yazilidir, tahminle degil.
        //
        // Enum ad olarak: istemci "3" degil "ShotMade" gorur. Sayisal enum,
        // enum sirasi degistiginde sessizce bozulur (M5 D88).
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static MatchEventDto From(MatchEvent matchEvent)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);

        var payload = JsonSerializer.SerializeToElement(
            matchEvent.Payload, matchEvent.Payload.GetType(), PayloadOptions);

        return new MatchEventDto
        {
            MatchId = matchEvent.MatchId,
            Sequence = matchEvent.Sequence,
            SchemaVersion = matchEvent.SchemaVersion,
            EngineVersion = matchEvent.EngineVersion,
            ConfigHash = matchEvent.ConfigHash,
            Type = matchEvent.Type,
            Period = matchEvent.Period,
            GameClockMs = matchEvent.GameClockMs,
            ElapsedGameTimeMs = matchEvent.ElapsedGameTimeMs,
            PossessionId = matchEvent.PossessionId,
            ActionId = matchEvent.ActionId,
            TeamId = matchEvent.TeamId,
            PlayerId = matchEvent.PlayerId,
            SecondaryPlayerId = matchEvent.SecondaryPlayerId,
            Payload = payload,
        };
    }

    public static IReadOnlyList<MatchEventDto> From(IReadOnlyList<MatchEvent> events) =>
        [.. events.Select(From)];
}

/// <summary>
/// M7: reconnect yaniti. 07 §8'in atomik siniri burada TEK nesne olarak
/// gider; ayri iki istekte ayri okunursa araya event girer.
///
/// <para><b>SEQUENCE SOZLESMESI:</b> <see cref="FromSequence"/> = "sana
/// bundan sonrasini gonderiyorum", yani <b>son teslim edilen</b> sequence.
/// Ham snapshot JSON'undaki <c>StateSequence</c> ise motorun "sonraki"
/// isaretçisidir ve <c>FromSequence + 1</c>'dir; bu bir fark degil, iki
/// ayri sayimdir. Istemci <b>bu</b> alani kullanir, ham JSON'i degil.</para>
/// </summary>
public sealed record StateResponse
{
    public required Guid MatchId { get; init; }

    /// <summary>Istemcinin bu yanittan sonra kendi konumu.</summary>
    public required long FromSequence { get; init; }

    /// <summary>Sunucudaki son sequence.</summary>
    public required long CurrentSequence { get; init; }

    public required string ConfigHash { get; init; }

    public required bool IsTerminal { get; init; }

    public required string? Status { get; init; }

    /// <summary>Yalniz istenirse. Ham motor JSON'u; cozumlemesi istemcinin isidir.</summary>
    public string? SnapshotJson { get; init; }

    public required IReadOnlyList<MatchEventDto> Events { get; init; }
}

/// <summary>
/// M7: yonetici komutu istegi.
///
/// <para><b>SAHIPLIK ALANI YOKTUR.</b> Istemci "ben su takimim" diyemez;
/// kimlik JWT'den gelir ve karsilastirma sunucuda yapilir (07 §5). Boyle bir
/// alan olsaydi, guvenilmeyen bir degeri dogrulanmak zorunda kalirdik.</para>
/// </summary>
public sealed record SendCommandRequest
{
    public required Guid MatchId { get; init; }

    /// <summary>Idempotency kimligi. Ayni kimlikle gelen istek TEK islenir.</summary>
    public required Guid CommandId { get; init; }

    public required TeamSide Side { get; init; }

    public required ManagerCommandKind Kind { get; init; }

    public required CommandBoundary TargetBoundary { get; init; }

    /// <summary>07 §5: ustel degisme reddi. Istege bagli.</summary>
    public long? ExpectedSequence { get; init; }

    public CommandPayload Payload { get; init; } = new();
}

/// <summary>
/// M7: komut yaniti.
///
/// <para><b>ACK VE APPLIED AYRI.</b> 07 §5: "ACK = alindi/kuyruga girdi.
/// Applied = state'e islendi. Bu ikisini tek basari mesajinda karistirma."
/// Burada donen <c>Accepted=true, Applied=false</c> tam olarak ACK'tir;
/// <c>Applied</c> ancak motor adiminda ayrica bildirilir.</para>
/// </summary>
public sealed record CommandAck
{
    public required Guid CommandId { get; init; }

    public required bool Accepted { get; init; }

    public required bool Applied { get; init; }

    /// <summary>
    /// Istegi yaparken gorulen epoch. Bayat bir istek fark edilir
    /// diye istemciye geri verilir.
    /// </summary>
    public required long Epoch { get; init; }

    public string? Error { get; init; }
}
