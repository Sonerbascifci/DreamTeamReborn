using System.Buffers.Binary;

namespace DreamTeam.MatchEngine.Randomness;

/// <summary>
/// SplitMix64 tabanlı deterministik oyun RNG'si.
///
/// Referans tanım: Sebastiano Vigna, <c>splitmix64.c</c> (public domain),
/// https://prng.di.unimi.it/splitmix64.c — Java 8 <c>SplittableRandom</c>'ın
/// sabit artımlı türevi. Algoritma adı ve sürümü <see cref="RngIdentity"/> ile
/// sabitlenir; platformun varsayılan <see cref="System.Random"/> davranışına
/// hiçbir güven yüklenmez.
///
/// Aynı seed ve aynı çağrı sırası her zaman aynı diziyi üretir. State tek bir
/// 64-bit tam sayıdır; snapshot'a yalnız başlangıç seed'i değil, devam için
/// mevcut state olarak yazılır.
/// </summary>
public sealed class SeededRandom : IRandomSource
{
    /// <summary>Serileştirilen state'in bayt cinsinden boyutu.</summary>
    public const int StateSizeInBytes = sizeof(ulong);

    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong MixMultiplier1 = 0xBF58476D1CE4E5B9UL;
    private const ulong MixMultiplier2 = 0x94D049BB133111EBUL;

    /// <summary>2^-53. Üst 53 bit doğrudan bu çarpanla ölçeklenir.</summary>
    private const double InverseTwoPow53 = 1.0 / 9007199254740992.0;

    private ulong _state;

    public SeededRandom(ulong seed)
    {
        _state = seed;
    }

    public ulong NextUInt64()
    {
        unchecked
        {
            _state += GoldenGamma;
            ulong z = _state;
            z = (z ^ (z >> 30)) * MixMultiplier1;
            z = (z ^ (z >> 27)) * MixMultiplier2;
            return z ^ (z >> 31);
        }
    }

    public double NextDouble()
    {
        // Üst 53 bit kullanılır: sonlu, platformdan bağımsız ve bölme hatası taşımayan
        // tek dönüşüm. Tüm bitler / (ulong.MaxValue) ölçeği veya Random.NextDouble()
        // kopyaları bu garantiyi vermez.
        return (NextUInt64() >> 11) * InverseTwoPow53;
    }

    public void GetState(Span<byte> destination)
    {
        if (destination.Length != StateSizeInBytes)
        {
            throw new ArgumentException(
                $"State tam olarak {StateSizeInBytes} bayt olmalıdır; alınan {destination.Length}.",
                nameof(destination));
        }

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _state);
    }

    public void SetState(ReadOnlySpan<byte> state)
    {
        if (state.Length != StateSizeInBytes)
        {
            throw new ArgumentException(
                $"State tam olarak {StateSizeInBytes} bayt olmalıdır; alınan {state.Length}.",
                nameof(state));
        }

        _state = BinaryPrimitives.ReadUInt64LittleEndian(state);
    }
}
