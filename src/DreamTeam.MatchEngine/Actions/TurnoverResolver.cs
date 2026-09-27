using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

public sealed record TurnoverOutcome(bool IsTurnover, TurnoverKind Kind);

/// <summary>
/// Aksiyon başına top kaybı çözümü. 05 §9.
///
/// 05, turnover'ı bir hücum sonucu, steal'i savunma atfı olarak ayırır. M2'de savunma
/// tarafları henüz üretken olmadığı için her turnover'in savunma atfı yoktur ve
/// yalnız <see cref="TurnoverKind.LostBall"/> ile
/// <see cref="TurnoverKind.ShotClockViolation"/> üretilir. Steal M3'te gelecektir;
/// o zaman attribution ikincil aktör alanına yazılır ve turnover sayısı artmaz.
///
/// M2'de hücum ribaundu da üretmez: 05 §10, OREB'nin transition defense maliyetiyle
/// birlikte tanımlanmasını ister; bu, M3'ün konusudur.
/// </summary>
public sealed class TurnoverResolver
{
    private readonly ActionModel _model;

    public TurnoverResolver(ActionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public TurnoverOutcome Resolve(IRandomSource random)
    {
        if (random.NextDouble() < _model.TurnoverProbability)
        {
            return new TurnoverOutcome(true, TurnoverKind.LostBall);
        }

        return new TurnoverOutcome(false, TurnoverKind.LostBall);
    }
}
