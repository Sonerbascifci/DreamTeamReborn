using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Ratings;
using DreamTeam.MatchEngine.Randomness;
using DreamTeam.MatchEngine.Tactics;

namespace DreamTeam.MatchEngine.Actions;

public sealed record TurnoverOutcome(bool IsTurnover, TurnoverKind Kind);

/// <summary>
/// Aksiyon başına top kaybı çözümü. 05 §9.
///
/// <para><b>M4'te savunma baskısı eklendi.</b> 05 §131, top kaybı risk
/// faktörlerinin "baseline, savunma baskısı, tempo, handler/passer becerisi ve IQ"
/// olduğunu söyler. M4'te <b>savunma baskısı</b> kanalı açıldı. Tempo kanalı
/// bilinçli olarak <b>kapalıdır</b> (D59): tempo zaten iki kanaldan geçiyor ve
/// üçüncü bir kanal çift sayma riskini yükseltirdi. Handler/IK kanalı da M4'te
/// yoktur — 05 §'te sayılmış ama ölçülmemiştir ve M6 kalibrasyonuna bırakıldı.</para>
///
/// <para>Steal atfedimi <b>M4 kapsamı dışıdır</b> (D66). 05 §9 turnover'ı hücum
/// sonucu, steal'i savunma atfı olarak ayırır; bu ayrım M5'e bırakıldı ve
/// kayda geçirildi. Şimdilik tüm kayıplar <see cref="TurnoverKind.LostBall"/>'dır.</para>
/// </summary>
public sealed class TurnoverResolver
{
    private readonly ActionModel _model;
    private readonly DefensivePolicy _defense;

    public TurnoverResolver(ActionModel model, DefensivePolicy defense)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(defense);

        _model = model;
        _defense = defense;
    }

    public TurnoverOutcome Resolve(PlayerRatings primaryDefenderRatings, IRandomSource random)
    {
        var pressure = _defense.TurnoverPressure(
            PlayerRatingTables.PerimeterDefense(primaryDefenderRatings));

        // Baski taban olasiligi ustune eklenir. Tavan 1.0'dir: olasilik zaten
        // [0,1] araligindadir ve keyfi alt sinif koymak test kuvvetlendirmelerini
        // sessizce zayiflatir.
        var probability = Math.Clamp(_model.TurnoverProbability + pressure, 0.0, 1.0);

        return new TurnoverOutcome(random.NextDouble() < probability, TurnoverKind.LostBall);
    }
}
