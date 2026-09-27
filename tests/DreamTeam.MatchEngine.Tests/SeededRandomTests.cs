using System.Buffers.Binary;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// SplitMix64 golden sequence testleri.
///
/// Beklenen değerler referans tanımdan bağımsız olarak üretilmiştir:
///  - birincil kaynak: Sebastiano Vigna, splitmix64.c (public domain),
///    https://prng.di.unimi.it/splitmix64.c
///  - iki ayrı uygulama ile çapraz kontrol: Python (keyfi büyüklükte tamsayı) ve
///    Node (BigInt), her ikisi de 64-bit taşma semantiğini açıkça modelledi.
///
/// Değerler bu testlerde sabitlenmiştir; rastgele seed arayarak üretilmiş bir
/// "altın" dizi değildir. Test double'ın aksine bu test, üretim algoritmasının
/// doğruluğuna kanıttır.
/// </summary>
public class SeededRandomTests
{
    private const ulong GoldenSeedZeroStateAfterFiveDraws = 0x1715609F7C746C69UL;

    /// <summary>SplitMix64, seed = 0, ilk 10 ham çıktı.</summary>
    private static readonly ulong[] SeedZeroUInt64 =
    [
        0xE220A8397B1DCDAFUL,
        0x6E789E6AA1B965F4UL,
        0x06C45D188009454FUL,
        0xF88BB8A8724C81ECUL,
        0x1B39896A51A8749BUL,
        0x53CB9F0C747EA2EAUL,
        0x2C829ABE1F4532E1UL,
        0xC584133AC916AB3CUL,
        0x3EE5789041C98AC3UL,
        0xF3B8488C368CB0A6UL,
    ];

    /// <summary>SplitMix64, seed = 12345, ilk 10 ham çıktı.</summary>
    private static readonly ulong[] Seed12345UInt64 =
    [
        0x22118258A9D111A0UL,
        0x346EDCE5F713F8EDUL,
        0x1E9A57BC80E6721DUL,
        0x2D160E7E5C3F42CAUL,
        0x81C2E6DC980D78EBUL,
        0x5647E55AD933F62EUL,
        0x1F6622B40CB38E42UL,
        0x6E7411B06820371CUL,
        0x7AD34039583AB917UL,
        0xDE15EAB5CE53FECFUL,
    ];

    /// <summary>Seed 0 için (u >> 11) * 2^-53 dönüşümünün ilk 5 çıktısı.</summary>
    private static readonly double[] SeedZeroDouble =
    [
        0.8833108082136426,
        0.43152799704850997,
        0.026433771592597743,
        0.9708819781538285,
        0.10634669156721244,
    ];

    [Fact]
    public void SeedZeroMatchesGoldenSequence()
    {
        var random = new SeededRandom(0);

        foreach (var expected in SeedZeroUInt64)
        {
            Assert.Equal(expected, random.NextUInt64());
        }
    }

    [Fact]
    public void NonZeroSeedMatchesGoldenSequence()
    {
        var random = new SeededRandom(12345);

        foreach (var expected in Seed12345UInt64)
        {
            Assert.Equal(expected, random.NextUInt64());
        }
    }

    [Fact]
    public void NextDoubleMatchesGoldenSequence()
    {
        var random = new SeededRandom(0);

        foreach (var expected in SeedZeroDouble)
        {
            Assert.Equal(expected, random.NextDouble());
        }
    }

    [Fact]
    public void StateAfterFiveDrawsMatchesGoldenState()
    {
        var random = new SeededRandom(0);

        for (var draw = 0; draw < 5; draw++)
        {
            random.NextUInt64();
        }

        var state = new byte[SeededRandom.StateSizeInBytes];
        random.GetState(state);

        Assert.Equal(GoldenSeedZeroStateAfterFiveDraws, BinaryPrimitives.ReadUInt64LittleEndian(state));
    }

    [Fact]
    public void RestoringStateContinuesTheSameStream()
    {
        // State, snapshot'a yalnız başlangıç seed'i değil devam için mevcut durum
        // olarak yazılır. Geri yüklenen kaynak, kesilmemiş kaynakla aynı diziyi
        // üretmek zorundadır.
        var continuous = new SeededRandom(12345);
        for (var draw = 0; draw < 7; draw++)
        {
            continuous.NextUInt64();
        }

        var interrupted = new SeededRandom(12345);
        for (var draw = 0; draw < 7; draw++)
        {
            interrupted.NextUInt64();
        }

        var state = new byte[SeededRandom.StateSizeInBytes];
        interrupted.GetState(state);

        var restored = new SeededRandom(seed: 0);
        restored.SetState(state);

        for (var draw = 0; draw < 32; draw++)
        {
            Assert.Equal(continuous.NextUInt64(), restored.NextUInt64());
        }
    }

    [Fact]
    public void StateRoundTripDoesNotChangeEncoding()
    {
        var random = new SeededRandom(0);
        var buffer = new byte[SeededRandom.StateSizeInBytes];
        random.GetState(buffer);

        Assert.Equal(8, buffer.Length);

        // Little-endian, platform bağımsız.
        var clone = new SeededRandom(0);
        clone.SetState(buffer);

        for (var draw = 0; draw < 10; draw++)
        {
            Assert.Equal(SeedZeroUInt64[draw], clone.NextUInt64());
        }
    }

    [Fact]
    public void SameSeedProducesSameSequence()
    {
        var first = new SeededRandom(987654321);
        var second = new SeededRandom(987654321);

        for (var draw = 0; draw < 1000; draw++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }
    }

    [Fact]
    public void DifferentSeedsProduceDifferentSequences()
    {
        var first = new SeededRandom(1);
        var second = new SeededRandom(2);

        var firstSequence = Enumerable.Range(0, 100).Select(_ => first.NextUInt64()).ToArray();
        var secondSequence = Enumerable.Range(0, 100).Select(_ => second.NextUInt64()).ToArray();

        Assert.NotEqual(firstSequence, secondSequence);
    }

    [Fact]
    public void ConsecutiveSeedsAreNotSequentiallyCorrelated()
    {
        // Sabit artımlı üreteçte ardışık seed'ler ilk çıktılarda çok yakındır.
        // Bu, oyun RNG'si olarak kullanıldığında öngörülebilir bir açıktır.
        var first = new SeededRandom(1000).NextUInt64();
        var second = new SeededRandom(1001).NextUInt64();

        Assert.True(Math.Abs((long)(second - first)) > 1_000_000L, "Ardışık seed'ler fazla benzer çıktı üretti.");
    }

    [Fact]
    public void InterleavedDrawsDoNotAffectEachOther()
    {
        // Karışık tüketim sırası da deterministiktir: her NextDouble tam olarak bir
        // state adımı ilerletir, bu yüzden araya giren çift çekiliş ham çıktı
        // dizisini ikişer adım kaydırır, atlamaz veya çoğaltmaz.
        var reference = new SeededRandom(4242);
        var referenceSequence = Enumerable.Range(0, 100).Select(_ => reference.NextUInt64()).ToArray();

        var interleaved = new SeededRandom(4242);
        var actual = new List<ulong>();

        for (var round = 0; round < 50; round++)
        {
            actual.Add(interleaved.NextUInt64());
            interleaved.NextDouble();
        }

        Assert.Equal(
            referenceSequence.Where((_, index) => index % 2 == 0),
            actual);
    }

    [Fact]
    public void NextDoubleStaysWithinUnitInterval()
    {
        var random = new SeededRandom(20260927);
        var minimum = 1.0;
        var maximum = 0.0;

        for (var draw = 0; draw < 100_000; draw++)
        {
            var value = random.NextDouble();

            Assert.InRange(value, 0.0, 0.9999999999999999);
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        // 100.000 çekilişte uçların fiilen görülmesi, çıktının tüm [0,1) aralığını
        // kullandığını gösterir. Alt sınır tam olarak 0.0, üst sınır 1.0 olsaydı
        // test zaten daha önce patlardı.
        Assert.True(minimum < 0.01, $"Alt uç beklenmedik: {minimum}");
        Assert.True(maximum > 0.99, $"Üst uç beklenmedik: {maximum}");
    }

    [Fact]
    public void NextDoubleConsumesExactlyOneStateStepPerCall()
    {
        // NextDouble, NextUInt64'in aksine state'i iki kez ilerletmemelidir;
        // aksi halde iki farklı tüketim yolu aynı seed'i farklı hizalardan başlatır.
        var viaDouble = new SeededRandom(555);
        for (var draw = 0; draw < 5; draw++)
        {
            viaDouble.NextDouble();
        }

        var viaUInt64 = new SeededRandom(555);
        var expected = Enumerable.Range(0, 6).Select(_ => viaUInt64.NextUInt64()).ToArray();

        var state = new byte[SeededRandom.StateSizeInBytes];
        viaDouble.GetState(state);

        var replayed = new SeededRandom(0);
        replayed.SetState(state);

        // State 5 adım sonrası olduğuna göre bir sonraki ham çıktı 6. çıktıdır.
        Assert.Equal(expected[5], replayed.NextUInt64());
        Assert.Equal(viaUInt64.NextUInt64(), replayed.NextUInt64());
    }

    [Fact]
    public void StateRejectsWrongLengthBuffer()
    {
        var random = new SeededRandom(1);

        Assert.Throws<ArgumentException>(() => random.GetState(new byte[SeededRandom.StateSizeInBytes - 1]));
        Assert.Throws<ArgumentException>(() => random.GetState(new byte[SeededRandom.StateSizeInBytes + 1]));
        Assert.Throws<ArgumentException>(() => random.SetState(new byte[SeededRandom.StateSizeInBytes - 1]));
        Assert.Throws<ArgumentException>(() => random.SetState(new byte[SeededRandom.StateSizeInBytes + 1]));
    }

    [Fact]
    public void RngIdentityIsStableAndMatchesTheImplementation()
    {
        Assert.Equal("SplitMix64", RngIdentity.Algorithm);
        Assert.Equal("1", RngIdentity.Version);
        Assert.Equal(sizeof(ulong), SeededRandom.StateSizeInBytes);
    }
}
