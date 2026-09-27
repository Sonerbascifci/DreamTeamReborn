using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

/// <summary>
/// Serbest atış isabeti çözümü (M3).
///
/// 05 §7'nin logit şablonunun serbest atış karşılığı. Taban olasılığı oyuncu
/// <c>FreeThrow</c> attribute'ünden okunur; kalibre edilmemiştir ve M6'da
/// ölçülecektir.
///
/// Serbest atış canlı oyun süresi tüketmez (06 §2): bu yüzden burada saat
/// ilerletilmez.
/// </summary>
public sealed class FreeThrowResolver
{
    private readonly FreeThrowModel _model;

    public FreeThrowResolver(FreeThrowModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public bool IsMade(int freeThrowRating, IRandomSource random)
    {
        var probability = ShotMath.MakeProbability(
            _model.BaseMakeProbability,
            freeThrowRating,
            _model.SkillScale);

        return random.NextDouble() < probability;
    }
}
