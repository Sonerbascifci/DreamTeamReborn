namespace DreamTeam.Domain.Players;

/// <summary>
/// Kurgusal oyuncu tanımı. İlk içerik tamamen kurgusaldır; gerçek kişi verisi,
/// fotoğraf veya lisanslı veri bu sürümde yoktur.
///
/// <see cref="DisplayName"/> simülasyon olasılığına girmez: isim hiçbir RNG girdisi
/// veya sıralama anahtarı değildir.
/// </summary>
public sealed record Player
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public required Position Position { get; init; }

    public required PlayerRatings Ratings { get; init; }
}
