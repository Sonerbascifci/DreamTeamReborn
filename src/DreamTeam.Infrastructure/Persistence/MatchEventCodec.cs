using System.Text.Json;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.Infrastructure.Persistence;

/// <summary>
/// M7: <c>MatchEvent</c> &lt;-&gt; kalici satir.
///
/// <para><b>POLIMORFIZM NEREDE?</b> Zarf sutunlarda (<c>type</c> smallint),
/// payload JSONB'de. <see cref="Type"/> alani payload'in tipini BELIRLER:
/// acik bir <c>switch</c> 25 tipi 25 payload kaydina baglar.</para>
///
/// <para><b>NEDEN ACIK SWITCH?</b> Cunku <c>MatchEventType</c> bir enum ve
/// enum degerleri sirali. Bir <c>Enum.GetValues</c> + reflection cozumlesi
/// kod olarak daha kisadir ama iki sessiz kirlilik yaratir: (1) enum'a yeni
/// bir tip eklenip payload'i unutulursa calisma zamaninda patlar, (2) enum
/// SIRASI degisirse eski satirlar <b>yanlis payload</b> olarak okunur —
/// belki hata bile vermez. Acik switch her ikisini derleme zamanina
/// yakinlastirir ve gozle denetlenebilir kilar.</para>
///
/// <para><b>BILINMEYEN DEGER HATA VERIR.</b> Sessizce bos payload'a dusmek
/// yerine hata veriyoruz. 07 §1: "Payload tipi Type ile eslesmelidir;
/// eslesmezse anlasilir bir hata verir." Ayni kural burada da gecerli.</para>
///
/// <para><b>ENUM SIRASI DONDURULMUSTUR</b> (M3/M5, 25 tip). Migration'da
/// <c>CHECK (type BETWEEN 0 AND 24)</c> vardir; veritabani da ayni siniri
/// uygular. Test enum sayisini ve siraliyor oldugunu dogrular.</para>
/// </summary>
public static class MatchEventCodec
{
    /// <summary>Event semasi surumu. M6'da 4 idi ve M7'de DEGISMEDI.</summary>
    public const int SchemaVersion = 4;

    public static MatchEventType PayloadTypeFor(MatchEventType type) => type switch
    {
        MatchEventType.MatchStarted => MatchEventType.MatchStarted,
        MatchEventType.PeriodStarted => MatchEventType.PeriodStarted,
        MatchEventType.PeriodEnded => MatchEventType.PeriodEnded,
        MatchEventType.MatchEnded => MatchEventType.MatchEnded,
        MatchEventType.MatchAborted => MatchEventType.MatchAborted,
        MatchEventType.PossessionStarted => MatchEventType.PossessionStarted,
        MatchEventType.PossessionEnded => MatchEventType.PossessionEnded,
        MatchEventType.ActionCompleted => MatchEventType.ActionCompleted,
        MatchEventType.ShotAttempt => MatchEventType.ShotAttempt,
        MatchEventType.ShotMade => MatchEventType.ShotMade,
        MatchEventType.ShotMissed => MatchEventType.ShotMissed,
        MatchEventType.Block => MatchEventType.Block,
        MatchEventType.Rebound => MatchEventType.Rebound,
        MatchEventType.Turnover => MatchEventType.Turnover,
        MatchEventType.Foul => MatchEventType.Foul,
        MatchEventType.FreeThrowAttempt => MatchEventType.FreeThrowAttempt,
        MatchEventType.FreeThrowMade => MatchEventType.FreeThrowMade,
        MatchEventType.FreeThrowMissed => MatchEventType.FreeThrowMissed,
        MatchEventType.TacticChanged => MatchEventType.TacticChanged,
        MatchEventType.DefenseChanged => MatchEventType.DefenseChanged,
        MatchEventType.PaceChanged => MatchEventType.PaceChanged,
        MatchEventType.Substitution => MatchEventType.Substitution,
        MatchEventType.Timeout => MatchEventType.Timeout,
        MatchEventType.CommandApplied => MatchEventType.CommandApplied,
        MatchEventType.CommandRejected => MatchEventType.CommandRejected,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Bilinmeyen event tipi; sema ile eslesmiyor."),
    };

    public static string EncodePayload(MatchEvent matchEvent)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);

        // Zarf, payload'in BEKLENEN tipiyle eslesmiyor mu? Motor bunu
        // garantiler; biz de bir daha kontrol ederiz, cunku bozuk bir kaydi
        // sessizce kaydetmek hatayi okuma anina birakir ve o an kimse hatayi
        // aramaz.
        var expected = PayloadClrTypeFor(matchEvent.Type);

        if (matchEvent.Payload.GetType() != expected)
        {
            throw new InvalidOperationException(
                $"Event {matchEvent.Type} icin beklenen payload {expected.Name}, "
                + $"{matchEvent.Payload.GetType().Name} geldi (Sequence {matchEvent.Sequence}). "
                + "Motor sozlesmesi bozuldu.");
        }

        return JsonSerializer.Serialize(matchEvent.Payload, expected);
    }

    public static MatchEventPayload DecodePayload(int typeValue, string json)
    {
        var type = (MatchEventType)typeValue;

        // Varligi dogrula: bilinmeyen bir deger icin switch'i calistirmak
        // anlamsiz.
        _ = PayloadTypeFor(type);

        var payloadType = PayloadClrTypeFor(type);

        return (MatchEventPayload)(JsonSerializer.Deserialize(json, payloadType)
            ?? throw new InvalidOperationException(
                $"Event {type} icin payload okunamadi (tip {typeValue})."));
    }

    /// <summary>Satirdan <c>MatchEvent</c> kurar. Dapper bunu cagirir.</summary>
    public static MatchEvent ToEvent(
        Guid matchId,
        long sequence,
        int schemaVersion,
        string engineVersion,
        string configHash,
        int typeValue,
        short period,
        long gameClockMs,
        long elapsedGameTimeMs,
        int? possessionId,
        long? actionId,
        byte? teamId,
        Guid? playerId,
        Guid? secondaryPlayerId,
        string payload)
    {
        return new MatchEvent
        {
            MatchId = matchId,
            Sequence = sequence,
            SchemaVersion = schemaVersion,
            EngineVersion = engineVersion,
            ConfigHash = configHash,
            Type = (MatchEventType)typeValue,
            Period = period,
            GameClockMs = gameClockMs,
            ElapsedGameTimeMs = elapsedGameTimeMs,
            PossessionId = possessionId,
            ActionId = actionId,
            TeamId = teamId is null ? null : (TeamSide)teamId.Value,
            PlayerId = playerId,
            SecondaryPlayerId = secondaryPlayerId,
            Payload = DecodePayload(typeValue, payload),
        };
    }

    /// <summary>Bilinmeyen tipte hata veren tek esleme tablosu.</summary>
    public static Type PayloadClrTypeFor(MatchEventType type) => type switch
    {
        MatchEventType.MatchStarted => typeof(MatchStartedPayload),
        MatchEventType.PeriodStarted => typeof(PeriodStartedPayload),
        MatchEventType.PeriodEnded => typeof(PeriodEndedPayload),
        MatchEventType.MatchEnded => typeof(MatchEndedPayload),
        MatchEventType.MatchAborted => typeof(MatchAbortedPayload),
        MatchEventType.PossessionStarted => typeof(PossessionStartedPayload),
        MatchEventType.PossessionEnded => typeof(PossessionEndedPayload),
        MatchEventType.ActionCompleted => typeof(ActionCompletedPayload),
        MatchEventType.ShotAttempt => typeof(ShotAttemptPayload),
        MatchEventType.ShotMade => typeof(ShotMadePayload),
        MatchEventType.ShotMissed => typeof(ShotMissedPayload),
        MatchEventType.Block => typeof(BlockPayload),
        MatchEventType.Rebound => typeof(ReboundPayload),
        MatchEventType.Turnover => typeof(TurnoverPayload),
        MatchEventType.Foul => typeof(FoulPayload),
        MatchEventType.FreeThrowAttempt => typeof(FreeThrowAttemptPayload),
        MatchEventType.FreeThrowMade => typeof(FreeThrowMadePayload),
        MatchEventType.FreeThrowMissed => typeof(FreeThrowMissedPayload),
        MatchEventType.TacticChanged => typeof(TacticChangedPayload),
        MatchEventType.DefenseChanged => typeof(DefenseChangedPayload),
        MatchEventType.PaceChanged => typeof(PaceChangedPayload),
        MatchEventType.Substitution => typeof(SubstitutionPayload),
        MatchEventType.Timeout => typeof(TimeoutPayload),
        MatchEventType.CommandApplied => typeof(CommandAppliedPayload),
        MatchEventType.CommandRejected => typeof(CommandRejectedPayload),
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Bilinmeyen event tipi; sema ile eslesmiyor."),
    };
}
