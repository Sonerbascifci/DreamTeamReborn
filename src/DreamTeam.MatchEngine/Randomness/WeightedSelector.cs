using System.Globalization;

namespace DreamTeam.MatchEngine.Randomness;

/// <summary>
/// Deterministik ağırlıklı seçim. Hem oyun RNG'si hem de testler tarafından kullanılır.
///
/// Kurallar:
/// - Ağırlık negatif veya NaN olamaz.
/// - Toplam ağırlık sıfırsa liste boş olsa bile seçim yapılamaz; sessizce ilk adayı
///   seçmek 05_MATCH_ENGINE_SPEC.md §5'in yasakladığı hatadır, bu yüzden istisna atılır.
/// - Çekiliş <see cref="IRandomSource.NextDouble"/>'ın [0,1) aralığında olduğu için
///   son adaya hiçbir zaman düşülmez ve ilk adaya asla fazla düşülmez.
/// </summary>
public static class WeightedSelector
{
    public static T Select<T>(IReadOnlyList<T> candidates, Func<T, double> weightOf, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(weightOf);
        ArgumentNullException.ThrowIfNull(random);

        var total = 0.0;

        foreach (var candidate in candidates)
        {
            var weight = weightOf(candidate);

            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0.0)
            {
                throw new InvalidOperationException(
                    $"Ağırlık negatif veya sonlu değil: {weight.ToString("R", CultureInfo.InvariantCulture)}");
            }

            total += weight;
        }

        if (total <= 0.0)
        {
            throw new InvalidOperationException(
                $"Toplam ağırlık sıfır; {candidates.Count} adaydan seçim yapılamaz.");
        }

        var roll = random.NextDouble() * total;

        foreach (var candidate in candidates)
        {
            roll -= weightOf(candidate);

            if (roll < 0.0)
            {
                return candidate;
            }
        }

        // Kayan nokta birikimi son adaya tam eşitlikte düşürebilir. Son adayı
        // döndürmek, "ilk adayı seçme" hatasının karşıtı olan güvenli kapanıştır.
        return candidates[^1];
    }
}
