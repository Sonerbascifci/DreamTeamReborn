using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Ratings;

/// <summary>
/// Oyuncu seçimi ve savunma ağırlıklarının hesaplandığı tek yer (D65).
///
/// <para><b>Seçim ağırlığı formülü</b> (05 §76):</para>
/// <code>
/// weight = max(MinWeight, 1 + SelectionSpread * Normalize(composite))
/// </code>
/// <para>Bu üç özelliği birden garanti eder:</para>
/// <list type="number">
///   <item><description>Toplam <b>asla sıfır olmaz</b> — M3'ün <c>attribute + 1</c> garantisi korunur.</description></item>
///   <item><description>Hiçbir oyuncu seçilemez hale gelmez — taban <c>1 - spread</c>'dir.</description></item>
///   <item><description>Yoğunlaşma sınırlıdır — ham rating çarpanı yerine bounded aralık.</description></item>
/// </list>
///
/// <para><b>Enerji bu sınıfa girmez.</b> D58.</para>
/// </summary>
public sealed class PlayerRatingCalculator
{
    /// <summary>Ağırlığın alt sınırı. Sıfır olması bölme hatasına yol açardı.</summary>
    public const double MinWeight = 0.01;

    private readonly double _selectionSpread;

    public PlayerRatingCalculator(double selectionSpread)
    {
        if (double.IsNaN(selectionSpread) || double.IsInfinity(selectionSpread))
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectionSpread),
                selectionSpread,
                "Yayılım sonlu olmalıdır.");
        }

        if (selectionSpread < 0.0 || selectionSpread >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectionSpread),
                selectionSpread,
                "Yayılım [0, 1) aralığında olmalıdır; 1.0'de taban sıfıra iner.");
        }

        _selectionSpread = selectionSpread;
    }

    public double SelectionSpread => _selectionSpread;

    public static PlayerRatingCalculator Baseline { get; } = new(0.6);

    /// <summary>
    /// Bir bounded composite'den seçim ağırlığı üretir. Sonuç her zaman
    /// <c>&gt; 0</c>'dır.
    /// </summary>
    public double SelectionWeight(int composite) =>
        Math.Max(MinWeight, 1.0 + (_selectionSpread * PlayerRatingTables.Normalize(composite)));

    /// <summary>Sınırlar dahilinde olduğunu doğrular. Testler ve guard'lar için.</summary>
    public static bool IsBounded(int composite) => composite is >= 0 and <= 100;

    /// <summary>
    /// Bir aksiyonun oyuncu seçim ağırlığı. <see cref="ActionProfile"/>'in
    /// composite kanalını okur; ham <c>Skill</c> okunmaz.
    /// </summary>
    public double ActionSelectionWeight(ActionProfile profile, PlayerRatings ratings) =>
        SelectionWeight(profile.SelectionComposite(ratings));
}
