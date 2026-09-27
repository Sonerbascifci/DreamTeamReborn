using System.Collections.Immutable;
using DreamTeam.Domain.Players;

namespace DreamTeam.MatchEngine.Ratings;

/// <summary>
/// 18 attribute'un **tek** kanonik ad tablosu ve türetilmiş composite'ler (D65).
///
/// <para><b>Neden tek tablo?</b> M3'te <c>EligibilityPolicy</c> "en uygun yedek"
/// için ikinci bir attribute listesi kopyalamamak zorunda kaldı (plan sapması 2).
/// Bu tablo o kopyanın yerini alır: hem composite'ler hem de M1'in doğrulama
/// kapsamı testi buradan okur. <c>MatchSetupValidator</c> da bu tabloyu kullanır;
/// iki ayrı attribute listesi artık yoktur.</para>
///
/// <para><b>Enerji burada yoktur.</b> D58: <c>PlayerRatingCalculator</c> imzasında
/// enerji bulunmaz ve bu sınıf hiçbir state okumaz. Yorgunluk yalnız
/// <c>ShotMath</c> içindeki <c>betaFatigue * fatigueLoad</c> terimiyle şuta girer.
/// 05 §12'nin "aynı yorgunluğu iki kez cezalandırma" yasağı bu yüzden yapısal
/// olarak sağlanır: aynı etkiyi iki kanaldan geçirecek bir kod yolu yoktur.</para>
///
/// <para>Tüm composite'ler <c>[0,100]</c> aralığına kırpılır. Ağırlıklar
/// <c>Tuning</c>'dan gelir; varsayılanlar eşit ağırlıklıdır.</para>
/// </summary>
public static class PlayerRatingTables
{
    /// <summary>Ham rating'i <c>[-1,1]</c> aralığına merkezler ve ölçekler.</summary>
    public static double Normalize(int rating) => (rating - 50) / 50.0;

    /// <summary>
    /// Composite ağırlıkları. 18 attribute'un hepsi burada listelenir; bir
    /// attribute iki composite'de kullanılabilir, ama **listedeki her ad
    /// en az bir composite'de geçer**. <c>EveryRatingAttributeIsInTheTable</c>
    /// testi bu bütünlüğü korur.
    /// </summary>
    public static ImmutableArray<RatingWeight> Tuning { get; } =
    [
        // Handle
        new(nameof(PlayerRatings.BallHandling), 1.0),
        new(nameof(PlayerRatings.Passing), 1.0),
        new(nameof(PlayerRatings.BasketballIQ), 1.0),
        // PerimeterDefense
        new(nameof(PlayerRatings.PerimeterDefense), 1.0),
        new(nameof(PlayerRatings.Steal), 1.0),
        new(nameof(PlayerRatings.Speed), 1.0),
        new(nameof(PlayerRatings.BasketballIQ), 1.0),
        // InteriorDefense
        new(nameof(PlayerRatings.InteriorDefense), 1.0),
        new(nameof(PlayerRatings.Block), 1.0),
        new(nameof(PlayerRatings.Strength), 1.0),
        new(nameof(PlayerRatings.Rebounding), 1.0),
        // Rebounding
        new(nameof(PlayerRatings.Rebounding), 1.0),
        new(nameof(PlayerRatings.Strength), 1.0),
        new(nameof(PlayerRatings.Vertical), 1.0),
        // Athleticism
        new(nameof(PlayerRatings.Speed), 1.0),
        new(nameof(PlayerRatings.Vertical), 1.0),
        new(nameof(PlayerRatings.Strength), 1.0),
        new(nameof(PlayerRatings.Stamina), 1.0),
        // Scoring
        new(nameof(PlayerRatings.MidRange), 1.0),
        new(nameof(PlayerRatings.ThreePoint), 1.0),
        new(nameof(PlayerRatings.FreeThrow), 1.0),
        new(nameof(PlayerRatings.Inside), 1.0),
        // Interior
        new(nameof(PlayerRatings.Inside), 1.0),
        new(nameof(PlayerRatings.Strength), 1.0),
        new(nameof(PlayerRatings.PostOffense), 1.0),
        // Cutting: topsuz hareket. OffBall attribute'unun TEK kullanim yeridir.
        new(nameof(PlayerRatings.OffBall), 1.0),
    ];

    /// <summary>Top kullanımı ve karar kalitesi. Handler ağırlığında okunur.</summary>
    public static int Handle(PlayerRatings r) =>
        Blend(r.BallHandling, r.Passing, r.BasketballIQ);

    public static int PerimeterDefense(PlayerRatings r) =>
        Blend(r.PerimeterDefense, r.Steal, r.Speed, r.BasketballIQ);

    public static int InteriorDefense(PlayerRatings r) =>
        Blend(r.InteriorDefense, r.Block, r.Strength, r.Rebounding);

    public static int Rebounding(PlayerRatings r) =>
        Blend(r.Rebounding, r.Strength, r.Vertical);

    public static int Athleticism(PlayerRatings r) =>
        Blend(r.Speed, r.Vertical, r.Strength, r.Stamina);

    public static int Scoring(PlayerRatings r) =>
        Blend(r.MidRange, r.ThreePoint, r.FreeThrow, r.Inside);

    /// <summary>Post up ve iç oyun. <see cref="PostUp"/> aksiyonunun ağırlığı.</summary>
    public static int Interior(PlayerRatings r) =>
        Blend(r.Inside, r.Strength, r.PostOffense);

    /// <summary>
    /// Topsuz hareket / kesme. <c>OffBall</c> attribute'ünün <b>tek</b>
    /// composite'idir; 18 attribute'un tamamının tabloda geçmesi bu composite
    /// sayesinde sağlanır.
    /// </summary>
    public static int Cutting(PlayerRatings r) => r.OffBall;

    /// <summary>
    /// Eşit ağırlıklı ortalama, <c>[0,100]</c>'e kırpılmış. Tüm attribute'lar
    /// 0-100 doğrulandığı için ortalama zaten aralıkta kalır; kırpma savunma
    /// amaçlıdır ve normalde tetiklenmez.
    /// </summary>
    private static int Blend(params int[] values)
    {
        if (values.Length == 0)
        {
            return 50;
        }

        long total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return (int)Math.Clamp((double)total / values.Length, 0.0, 100.0);
    }
}

/// <param name="Attribute">Attribute adı. Yalnız okunabilirlik ve test için.</param>
/// <param name="Weight">Composite içindeki ağırlığı.</param>
public readonly record struct RatingWeight(string Attribute, double Weight);
