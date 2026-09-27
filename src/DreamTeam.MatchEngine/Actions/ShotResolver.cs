using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

public sealed record ShotOutcome(bool IsMade, int Points);

/// <summary>
/// Şut sonucunun çözülmesi. 05 §7'nin logit şablonu; formül
/// <see cref="ShotMath.MakeProbability"/> içinde, saf olarak test edilebilir.
///
/// <para><b>M4'te savunma ve taktik girdi.</b> Üç kanal ayrı ayrı gelir:
/// <c>skill</c> (beceri), <c>quality</c> (savunma + taktik + IQ) ve
/// <c>fatigueLoad</c> (enerji). Savunmanın yalnız <c>quality</c>'ye girdiğini ve
/// yorgunluğun yalnız <c>fatigueLoad</c>'a girdiğini garanti eden şey
/// <see cref="ShotMath"/>'in imzasıdır (D58): bu resolver başka bir yerden
/// savunma veya enerji okumaz.</para>
///
/// <para>Bu sınıf <b>tek canonical settlement</b>'ın parçasıdır; blok tarafından
/// isabet çekilişi tüketilmişse çağrılmaz (05 §127).</para>
/// </summary>
public sealed class ShotResolver
{
    private readonly ShotModel _model;
    private readonly FatigueModel _fatigue;

    public ShotResolver(ShotModel model, FatigueModel fatigue)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(fatigue);

        _model = model;
        _fatigue = fatigue;
    }

    /// <param name="shotType">Şut türü; taban olasılığı ve puanı belirler.</param>
    /// <param name="skillRating">Shooter'ın ilgili beceri attribute'ü (0-100).</param>
    /// <param name="quality">M4: savunma + taktik + IQ'dan türeyen şut kalitesi (0-100).</param>
    /// <param name="fatigueLoad">M4: yorgunluk yükü (0-1); enerjiden <c>FatigueCalculator</c> tarafından.</param>
    public ShotOutcome Resolve(
        ShotType shotType,
        int skillRating,
        int quality,
        double fatigueLoad,
        IRandomSource random)
    {
        var baseProbability = _model.BaseFor(shotType);
        var makeProbability = ShotMath.MakeProbability(
            baseProbability,
            skillRating,
            quality,
            fatigueLoad,
            _model.SkillScale,
            _model.QualityScale,
            _fatigue.FatigueLogitScale);

        var isMade = random.NextDouble() < makeProbability;

        return new ShotOutcome(isMade, isMade ? PointsFor(shotType) : 0);
    }

    /// <summary>M3 sözleşmesi: kalite ve yorgunluk nötr.</summary>
    public ShotOutcome Resolve(ShotType shotType, int skillRating, IRandomSource random) =>
        Resolve(shotType, skillRating, ShotQualityResolver.BaseQuality, 0.0, random);

    /// <summary>Şut türünün puanı. 2 veya 3; serbest atış listede yoktur (M3).</summary>
    public static int PointsFor(ShotType shotType) => shotType switch
    {
        ShotType.ThreePoint => 3,
        ShotType.AtRim or ShotType.ClosePost or ShotType.MidRange => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(shotType), shotType, "Bilinmeyen şut türü."),
    };
}
