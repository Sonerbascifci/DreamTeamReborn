using System.Collections.Immutable;

namespace DreamTeam.MatchEngine.Config;

/// <summary>
/// Enerji/stamina modeli (D58, D63, D64).
///
/// Maç başında her oyuncunun enerjisi <see cref="MaxEnergy"/>. Sahada geçen
/// her canlı saniye <see cref="BaselineDrainPerSecond"/> × stamina tersi kadar
/// azaltır; yedekte geçen her saniye <see cref="BaselineRecoveryPerSecond"/>
/// kadar artırır.
///
/// <para><b>Performans eğrisi veridir, formül değil.</b> 05 §12 bir tablo
/// verir ve aradaki değerlerin nasıl doldurulacağını "açık" bırakır. M4 doğrusal
/// interpolasyonu seçer; tablo <see cref="PerformanceCurve"/> alanında tutulur,
/// kodda sabit yazılmaz.</para>
///
/// <para><b>Enerji bir taba noktadır, çöküş değil.</b> D63: <c>Energy 0</c> için
/// çarpan 0.65'tir, 0 değildir. Sıfır bir noktaya inmek son anda ani ve adaletsiz
/// bir çöküş yaratırdı.</para>
/// </summary>
public sealed record FatigueModel
{
    public const int MaxEnergy = 100;

    /// <summary>Maç başı enerji (05 §12 taslağı).</summary>
    public required int StartingEnergy { get; init; }

    /// <summary>
    /// Stamina 0 olan bir oyuncu için saniyelik enerji kaybı. Hedef: bir 12
    /// dakikalık periyotta ortalama oyuncu %55-70 kaybetsin. KALİBRE DEĞİL.
    /// </summary>
    public required double BaselineDrainPerSecond { get; init; }

    /// <summary>Yedekte saniyelik toparlanma (stamina 50 için referans).</summary>
    public required double BaselineRecoveryPerSecond { get; init; }

    /// <summary>Periyot arası toparlanma hızı. 05 §149 "aralarda toparlanır" diyor.</summary>
    public required double BreakRecoveryPerSecond { get; init; }

    /// <summary>
    /// (energy, çarpan) çiftleri, enerjiye göre **azalan sırada**. Son çift
    /// taban noktadır (D63).
    /// </summary>
    public required ImmutableArray<EnergyAnchor> PerformanceCurve { get; init; }

    /// <summary>İsabet logitsindeki yorgunluk katsayısı (05 §7 <c>betaFatigue</c>).</summary>
    public required double FatigueLogitScale { get; init; }

    /// <summary>
    /// <paramref name="energy"/> için performans çarpanı. Kancalar arası doğrusal
    /// interpolasyon; kapsam dışı değerler en yakın kancaya sabitlenir.
    ///
    /// <para><b>Düzeltme (M4).</b> İlk uygulama, 100'ün altındaki <b>her</b> enerji
    /// için en üst kancanın çarpanını döndürüyordu: tarama ilk kancada
    /// <c>clamped &lt; current.Energy</c> dalına düşüp "üst kancaya sabitle" yolunu
    /// seçiyordu. Bunun sonucu, 80 üzeri enerjide yorgunluk kanalının tamamen
    /// ölü olmasıydı — <c>fatigueLoad</c> herkes için 0 çıkıyordu. M4 testleri
    /// bunu yakaladı.</para>
    /// </summary>
    public double PerformanceMultiplier(double energy)
    {
        var clamped = Math.Clamp(energy, 0.0, MaxEnergy);
        var curve = PerformanceCurve;

        if (curve.Length == 0)
        {
            return 1.0;
        }

        // Tam kanca eslesmesi.
        foreach (var anchor in curve)
        {
            if (Math.Abs(anchor.Energy - clamped) < 1e-9)
            {
                return anchor.Multiplier;
            }
        }

        // Iki kanca arasinda: dogrusal interpolasyon. Kanca sirasi enerjiye gore
        // AZALAN olmalidir; <see cref="Baseline"/> boyle siralar.
        for (var index = 0; index + 1 < curve.Length; index++)
        {
            var upper = curve[index];
            var lower = curve[index + 1];

            if (clamped < upper.Energy && clamped > lower.Energy)
            {
                return Interpolate(upper, lower, clamped);
            }
        }

        // Kapsam disi: en yakin kanca.
        return clamped > curve[0].Energy ? curve[0].Multiplier : curve[^1].Multiplier;
    }

    /// <summary>
    /// İki kanca arasında doğrusal interpolasyon. <paramref name="ratio"/> alt
    /// kancada 0, üst kancada 1 olacak şekilde hesaplanır.
    /// </summary>
    private static double Interpolate(EnergyAnchor upper, EnergyAnchor lower, double energy)
    {
        var span = upper.Energy - lower.Energy;

        if (span == 0.0)
        {
            return lower.Multiplier;
        }

        var ratio = (energy - lower.Energy) / span;

        return lower.Multiplier + ((upper.Multiplier - lower.Multiplier) * ratio);
    }

    public static FatigueModel Baseline { get; } = new()
    {
        StartingEnergy = 100,

        // Hedef: Stamina 78 olan tipik bir oyuncu 48 dakikalik tam maçı oynadiginda
        // ~50 puan kaybetsin (0.08 * 0.22 * 2880 = 50.7). Boylece final enerji
        // ~49 olur ve 05 §12 egrisinde gorunur bir yorgunluk etkisi olusur ama
        // oyun bozulmaz. KALIBRE EDILMEMIS; M6 olceumu.
        BaselineDrainPerSecond = 0.08,

        // Yedekte gecen sure icin: 48 dakika tam yedek ~+54 puan (0.012 * 1.56 *
        // 2880). Yedek, sahadakine gore belirgin sekilde toparlanir.
        BaselineRecoveryPerSecond = 0.012,

        // Periyot arasi (12 dk = 720 s) ~+29 puan.
        BreakRecoveryPerSecond = 0.04,

        // 05 §12'nin tablosu + D63'ün sifir ankraji.
        PerformanceCurve =
        [
            new(100, 1.00),
            new(80, 0.99),
            new(60, 0.97),
            new(40, 0.92),
            new(25, 0.84),
            new(10, 0.72),
            new(0, 0.65),
        ],
        FatigueLogitScale = 0.35,
    };
}

/// <param name="Energy">Enerji seviyesi, 0-100.</param>
/// <param name="Multiplier">Bu enerjideki performans çarpanı.</param>
public readonly record struct EnergyAnchor(int Energy, double Multiplier);
