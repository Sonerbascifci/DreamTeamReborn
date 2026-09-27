using System.Collections.Immutable;

namespace DreamTeam.MatchEngine.Config;

/// <summary>Tek bir tempo seviyesinin iki çarpanı (D59).</summary>
public readonly record struct PaceTuning(
    Pace Pace,
    double SetupActionMultiplier,
    double EnergyDrainMultiplier);

/// <summary>
/// Tempo ayarları. D59: tempo **iki** kanaldan geçer — aksiyon süresi ve enerji
/// drain'i — ve top kaybı riskine **girermez**.
///
/// 02 §6: "Hızlı tempo otomatik avantaj değildir; daha fazla possession, farklı
/// transition fırsatları, top kaybı ve yorgunluk maliyetiyle dengelenir." M4'te
/// top kaybı kanalı savunma policy'sine aittir; tempo burada yalnız süre ve yorgunluk
/// üzerinden görünür. 05 §131'in "her durumda top kaybı yaratmak zorunda değildir"
/// uyarısı bu yolla karşılanır.
///
/// Çarpanlar <b>kalibre edilmemiştir</b>. Dikkat: <see cref="PaceTuning.SetupActionMultiplier"/>
/// hücum saatini de etkiler. Kalibre edilmeden bu çarpanların hücum saati
/// ihlalini aşırı artırdığı görülürse 1.35/0.78 yerine daha dar bir aralık
/// kullanılmalıdır (M6).
/// </summary>
public sealed record PaceModel
{
    public required ImmutableArray<PaceTuning> Tunings { get; init; }

    public PaceTuning TuningFor(Pace pace)
    {
        foreach (var tuning in Tunings)
        {
            if (tuning.Pace == pace)
            {
                return tuning;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(pace),
            pace,
            "Config bu tempo seviyesini içermiyor.");
    }

    public static PaceModel Baseline { get; } = new()
    {
        Tunings =
        [
            new(Pace.Slow, SetupActionMultiplier: 1.35, EnergyDrainMultiplier: 0.88),
            new(Pace.Normal, SetupActionMultiplier: 1.00, EnergyDrainMultiplier: 1.00),
            new(Pace.Fast, SetupActionMultiplier: 0.78, EnergyDrainMultiplier: 1.15),
        ],
    };
}
