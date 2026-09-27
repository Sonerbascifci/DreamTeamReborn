namespace DreamTeam.MatchEngine.Actions;

/// <summary>
/// 05_MATCH_ENGINE_SPEC.md §7'deki şut olasılığı dönüşümü, saf fonksiyon olarak.
///
/// <code>
/// skill = (shotRating - 50) / 50                      -> [-1, 1]
/// z     = logit(baseProbability) + SkillScale * skill
/// pMake = 1 / (1 + exp(-z))
/// </code>
///
/// Savunma ve taktik etkisi 05 §3 gereği bu fonksiyona <b>girmez</b>; M2'de
/// savunma tarafları henüz resolver seviyesinde etki üretmez. Zaten bir etkiyi
/// iki kez saymamak, ShotQuality kavramı M4'te eklendiğinde korunacaktır.
///
/// <c>z</c> <see cref="MaxAbsLogit"/> ile sınırlanır: sonsuz veya NaN olasılık
/// üretmektense uç değere doğru kırpmak tercih edilir (08 §82 "Extremes" maddesi).
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

    public static double MakeProbability(double baseProbability, int shotRating, double skillScale)
    {
        if (double.IsNaN(skillScale) || double.IsInfinity(skillScale))
        {
            throw new ArgumentOutOfRangeException(nameof(skillScale), skillScale, "Ölçek sonlu olmalıdır.");
        }

        var z = Logit(baseProbability) + skillScale * NormalizeSkill(shotRating);

        if (double.IsNaN(z))
        {
            return 0.5;
        }

        var clamped = Math.Clamp(z, -MaxAbsLogit, MaxAbsLogit);

        return 1.0 / (1.0 + Math.Exp(-clamped));
    }
}
