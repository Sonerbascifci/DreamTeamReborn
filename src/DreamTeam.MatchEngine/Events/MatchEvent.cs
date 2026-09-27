namespace DreamTeam.MatchEngine.Events;

/// <summary>
/// Event zarfı. 07_EVENTS_COMMANDS_AND_REPLAY.md §1 tablosu birebir izlenir ve
/// <c>object? Data</c> taslağı kullanılmaz: payload ayrı, türüne özgü bir kayıttır.
///
/// Sözleşme kuralları:
/// - <see cref="Sequence"/> maç içi tekildir, 1'den başlar ve aralık bırakmaz.
///   Sıralama <c>GameClockMs</c>'ye göre değil <c>Sequence</c>'ye göre yapılır; birden
///   fazla event aynı oyun saatinde oluşabilir.
/// - <see cref="Payload"/> tipi <see cref="Type"/> ile eşleşmelidir; eşleşmezse
///   <see cref="PayloadAs{T}"/> anlaşılır bir hata verir.
/// - Diagnostic roll veya gizli state payload'a girmez.
/// - Ekranda gösterilecek metin taşınmaz; metin client'ta payload'dan üretilir.
/// - Zarfın tamamı <c>MatchEngine</c> tarafından doldurulur. Motor M2'de tek
///   üreticidir ve akış bütünlüğü testleriyle denetlenir.
/// </summary>
public sealed record MatchEvent
{
    public required Guid MatchId { get; init; }

    public required long Sequence { get; init; }

    /// <summary>Event sözleşmesinin sürümü. Şema değiştiğinde artar (M3'te artacak).</summary>
    public required int SchemaVersion { get; init; }

    public required string EngineVersion { get; init; }

    public required string ConfigHash { get; init; }

    public required MatchEventType Type { get; init; }

    public required int Period { get; init; }

    public required long GameClockMs { get; init; }

    public required long ElapsedGameTimeMs { get; init; }

    public required MatchEventPayload Payload { get; init; }

    /// <summary>İlgili hücum. Bağlam taşımayan eventlerde null olabilir.</summary>
    public int? PossessionId { get; init; }

    public long? ActionId { get; init; }

    public Core.TeamSide? TeamId { get; init; }

    /// <summary>Atfedilen birincil aktör: shooter, ribaund alan, top kaybeden.</summary>
    public Guid? PlayerId { get; init; }

    /// <summary>İkincil aktör: asist veren, çalan.</summary>
    public Guid? SecondaryPlayerId { get; init; }

    /// <summary>Payload'ı beklenen tipe güvenli biçimde açar.</summary>
    public T PayloadAs<T>()
        where T : MatchEventPayload =>
        Payload as T
        ?? throw new InvalidOperationException(
            $"Event türü {Type} için beklenen payload {typeof(T).Name} idi, "
            + $"{Payload.GetType().Name} geldi (Sequence {Sequence}).");
}
