using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

public sealed record ShotOutcome(bool IsMade, int Points);

/// <summary>
/// Şut sonucunun çözülmesi. 05 §7'nin logit şablonu; formül
/// <see cref="ShotMath.MakeProbability"/> içinde, saf olarak test edilebilir.
///
/// Savunma ve taktik etkisi bilinçli olarak yoktur: 05 §3, aynı etkinin hem
/// ShotQuality içinde hem z'ye eklenmesini iki kat sayma hatası sayar. M2'de
/// savunma resolver'ı bulunmadığı için ikinci katman da yoktur; M3/M4'te eklendiğinde
/// tek attribution korunacaktır.
/// </summary>
public sealed class ShotResolver
{
    private readonly ShotModel _model;

    public ShotResolver(ShotModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <param name="shotType">Şut türü; taban olasılığı ve puanı belirler.</param>
    /// <param name="skillRating">Shooter'ın ilgili beceri attribute'ü (0-100).</param>
    public ShotOutcome Resolve(ShotType shotType, int skillRating, IRandomSource random)
    {
        var baseProbability = _model.BaseFor(shotType);
        var makeProbability = ShotMath.MakeProbability(baseProbability, skillRating, _model.SkillScale);
        var isMade = random.NextDouble() < makeProbability;

        return new ShotOutcome(isMade, isMade ? PointsFor(shotType) : 0);
    }

    /// <summary>Şut türünün puanı. 2 veya 3; serbest atış listede yoktur (M3).</summary>
    public static int PointsFor(ShotType shotType) => shotType switch
    {
        ShotType.ThreePoint => 3,
        ShotType.AtRim or ShotType.ClosePost or ShotType.MidRange => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(shotType), shotType, "Bilinmeyen şut türü."),
    };
}
