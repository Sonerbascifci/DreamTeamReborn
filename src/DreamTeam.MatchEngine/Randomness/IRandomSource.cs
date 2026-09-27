namespace DreamTeam.MatchEngine.Randomness;

/// <summary>
/// Oyun RNG'sinin sözleşmesi. Üretimde yalnız <see cref="SeededRandom"/> kullanılır.
///
/// Arayüzün varlık gerekçesi: 05_MATCH_ENGINE_SPEC.md ve 08_TESTING_AND_BALANCE.md
/// dallanan kuralların kontrollü test double ile sınanmasını şart koşar. Bu arayüz
/// o değişken sınırıdır. <c>new Random()</c> veya <see cref="System.Random"/>
/// motor içinde kullanılmaz.
/// </summary>
public interface IRandomSource
{
    /// <summary>Ham 64-bit çıktı verir.</summary>
    ulong NextUInt64();

    /// <summary>[0.0, 1.0) aralığında çift hassasiyetli çıktı verir.</summary>
    double NextDouble();

    /// <summary>Devam için gereken mevcut state'i <paramref name="destination"/> içine yazar.</summary>
    void GetState(Span<byte> destination);

    /// <summary>State'i <paramref name="state"/> içinden geri yükler.</summary>
    void SetState(ReadOnlySpan<byte> state);
}
