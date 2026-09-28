using System.Globalization;

namespace DreamTeam.Simulator.Reporting;

/// <summary>
/// M6: a binomial proportion with a Wilson score interval (08 §6 asks for %95).
///
/// <para><b>Why Wilson and not the normal approximation.</b> Near p = 0.5 with a
/// large n the normal approximation is fine, but batch reports also print rates
/// that are close to 0 or 1 (aborts, foul-outs). There the normal interval
/// produces a bound outside [0,1], which is nonsense on its face. Wilson stays
/// inside the unit interval and behaves at the edges.</para>
///
/// <para><b>What the interval is NOT.</b> 08 §6: "Tek %95 aralığın 0,5'i
/// dışlaması tek başına kesin bug kanıtı değildir." A test failure is a signal to
/// investigate, not a diagnosis. The report labels its verdicts accordingly.</para>
/// </summary>
public readonly record struct ProportionEstimate(
    long Successes,
    long Trials,
    double Point,
    double Lower,
    double Upper)
{
    public bool HasSamples => Trials > 0;

    /// <summary>True when 0.5 lies outside the %95 interval. An ALARM, not a proof.</summary>
    public bool DeviatesFromHalf => HasSamples && (Lower > 0.5 || Upper < 0.5);

    public string ToReport(string label)
    {
        if (!HasSamples)
        {
            return $"{label,-22} n/a (ornek yok)";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label,-22} {Point.ToString("P2", CultureInfo.InvariantCulture)} "
            + $"[{Lower.ToString("P2", CultureInfo.InvariantCulture)}, "
            + $"{Upper.ToString("P2", CultureInfo.InvariantCulture)}] "
            + $"n={Trials}");
    }
}

public static class WilsonInterval
{
    /// <summary>z for a two-sided 95% interval.</summary>
    private const double Z95 = 1.959963984540054;

    public static ProportionEstimate For(long successes, long trials)
    {
        if (trials < 0 || successes < 0 || successes > trials)
        {
            throw new ArgumentOutOfRangeException(
                nameof(successes),
                successes,
                $"0 <= successes <= trials gerekir; verilen {successes}/{trials}.");
        }

        if (trials == 0)
        {
            return new ProportionEstimate(successes, 0, 0, 0, 0);
        }

        var n = (double)trials;
        var p = successes / n;
        var z2 = Z95 * Z95;
        var denominator = 1 + (z2 / n);
        var center = (p + (z2 / (2 * n))) / denominator;
        var margin = Z95 * Math.Sqrt((p * (1 - p) / n) + (z2 / (4 * n * n))) / denominator;

        return new ProportionEstimate(successes, trials, p, center - margin, center + margin);
    }
}
