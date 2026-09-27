namespace DreamTeam.MatchEngine.Actions;

/// <summary>
/// 05_MATCH_ENGINE_SPEC.md §7'deki şut olasılığı dönüşümü, saf fonksiyon olarak.
///
/// <code>
/// skill        = (shotRating - 50) / 50            -> [-1, 1]
/// quality      = (shotQuality - 50) / 50            -> [-1, 1]
/// fatigueLoad  = 1 - performanceMultiplier(energy)  -> [0, 1]
/// z = logit(baseProbability)
///   + SkillScale   * skill
///   + QualityScale * quality
///   - FatigueScale * fatigueLoad
/// pMake = 1 / (1 + exp(-z))
/// </code>
///
/// <para><b>Kanal ayrımı kuraldır (D58).</b> Savunma ve taktik etkisi
/// <b>yalnız</b> <c>quality</c> kanalından geçer. Yorgunluk <b>yalnız</b>
/// <c>fatigueLoad</c> kanalından geçer. 05 §7: "Savunma ve taktik ShotQuality
/// içine girdiyse z'ye aynı etkiyi tekrar ekleme"; 05 §12: "Hem rating'i çarpıp
/// hem şutta aynı yorgunluğu tekrar cezalandırma". Bu fonksiyonun imzası bu iki
/// yasağı <b>yapısal</b> kılar: <c>skill</c> taktiktten ve enerjiden bağımsız bir
/// ham rating'tir, <c>fatigueLoad</c> taktiktten ve savunmadan bağımsızdır.</para>
///
/// <para><c>z</c> <see cref="MaxAbsLogit"/> ile sınırlanır: sonsuz veya NaN olasılık
/// üretmektense uç değere doğru kırpmak tercih edilir (08 §82 "Extremes" maddesi).</para>
/// </summary>
public static class ShotMath
{
    /// <summary>Logit sınırı. exp(40) ikili hassasiyette güvenli sınırdır.</summary>
    public const double MaxAbsLogit = 40.0;

    /// <summary>Ham 0-100 rating'i [-1, 1] aralığına merkezler ve ölçekler.</summary>
    public static double NormalizeSkill(int shotRating) => (shotRating - 50) / 50.0;

    public static double Logit(double probability)
    {
        if (probability <= 0.0 || probability >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probability),
                probability,
                "Taban olasılık (0, 1) açık aralığında olmalıdır.");
        }

        return Math.Log(probability / (1.0 - probability));
    }

    /// <summary>
    /// M3 sözleşmesi: yalnız beceri. M4'te bu aşırı yüklü değil, geriye dönük
    /// uyum içindir; <see cref="MakeProbability"/> beş argümanlı hâli kullanılır.
    /// </summary>
    public static double MakeProbability(double baseProbability, int shotRating, double skillScale) =>
        MakeProbability(baseProbability, shotRating, 50, 0.0, skillScale, 0.0, 0.0);

    /// <summary>M4: beceri + kalite + yorgunluk kanalları.</summary>
    public static double MakeProbability(
        double baseProbability,
        int shotRating,
        int shotQuality,
        double fatigueLoad,
        double skillScale,
        double qualityScale,
        double fatigueScale)
    {
        if (double.IsNaN(skillScale) || double.IsInfinity(skillScale))
        {
            throw new ArgumentOutOfRangeException(nameof(skillScale), skillScale, "Ölçek sonlu olmalıdır.");
        }

        if (double.IsNaN(qualityScale) || double.IsInfinity(qualityScale))
        {
            throw new ArgumentOutOfRangeException(
                nameof(qualityScale),
                qualityScale,
                "Ölçek sonlu olmalıdır.");
        }

        if (double.IsNaN(fatigueScale) || double.IsInfinity(fatigueScale))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fatigueScale),
                fatigueScale,
                "Ölçek sonlu olmalıdır.");
        }

        var z = Logit(baseProbability)
            + (skillScale * NormalizeSkill(shotRating))
            + (qualityScale * NormalizeQuality(shotQuality))
            - (fatigueScale * Math.Clamp(fatigueLoad, 0.0, 1.0));

        if (double.IsNaN(z))
        {
            return 0.5;
        }

        var clamped = Math.Clamp(z, -MaxAbsLogit, MaxAbsLogit);

        return 1.0 / (1.0 + Math.Exp(-clamped));
    }

    /// <summary>Kalite 0-100'den [-1,1] aralığına.</summary>
    public static double NormalizeQuality(int quality) => (quality - 50) / 50.0;
}
