using System.Collections.Immutable;

namespace DreamTeam.MatchEngine.Config;

/// <summary>Tek bir aksiyonun taktik içindeki ağırlığı.</summary>
public readonly record struct ActionWeight(OffensiveAction Action, double Weight);

/// <summary>
/// Bir hücum taktiğinin aksiyon dağılımı ve taktikten türeyen şut-aksan kayması.
///
/// <see cref="Weights"/> her zaman 1.00'a normalize edilir (bkz.
/// <see cref="OffensivePolicy"/>). 05 §5'in PickAndRoll örneği dışındaki üç
/// taktığın sayısal değerleri <b>kalibre edilmemiştir</b>; yalnız yönleri
/// 05 §5'in "Motion topsuz hareket/cut/spot-up, InsidePost post/inside ağırlığını
/// artırmayı hedefler" cümlesinden gelir.
/// </summary>
public sealed record OffensiveTacticProfile
{
    public required OffensiveTactic Tactic { get; init; }

    public required ImmutableArray<ActionWeight> Weights { get; init; }

    /// <summary>
    /// Bu taktiğin şut aksanı. 0 = nötr; negatif içe, pozitif dışarı kayar.
    /// Kalibre edilmemiştir. Taktiğin dağılımı zaten şut türünü etkiliyor;
    /// bu kayma ikinci bir etki katmanıdır ve <b>küçük</b> tutulmuştur.
    /// </summary>
    public required double ShotBias { get; init; }

    /// <summary>
    /// Taktiğin oluşturduğu ek şut kalitesi. 05 §7: kalite pas yaratımı,
    /// spacing, matchup, IQ ve savunma baskısından gelir. Taktik kaliteyi
    /// yükseltir, savunma düşürür — ikisi aynı kanalda toplanır, bu yüzden
    /// ikisi arasındaki fark nettir.
    /// </summary>
    public required double QualityBonus { get; init; }
}

/// <summary>
/// Dört hücum taktiğinin dağılımları. 09 §62: "katsayıların sahibi config".
/// </summary>
public sealed record TacticsModel
{
    public required ImmutableArray<OffensiveTacticProfile> Offensive { get; init; }

    public OffensiveTacticProfile ProfileFor(OffensiveTactic tactic) =>
        Offensive.FirstOrDefault(profile => profile.Tactic == tactic)
        ?? throw new ArgumentOutOfRangeException(
            nameof(tactic),
            tactic,
            "Config bu hücum taktiğinin dağılımını içermiyor.");

    public static TacticsModel Baseline { get; } = new()
    {
        Offensive =
        [
            // 05 §5'in PickAndRoll örnek dağılımı, BIREBIR. D62: M3'un davranisi
            // bu yolla aynen korunur; "tarafsiz" bir vektor uydurmak kaynagi
            // olmayan bir tercih olurdu.
            new()
            {
                Tactic = OffensiveTactic.Balanced,
                ShotBias = 0.0,
                QualityBonus = 0.0,
                Weights =
                [
                    new(OffensiveAction.PickAndRoll, 0.45),
                    new(OffensiveAction.Drive, 0.15),
                    new(OffensiveAction.SpotUp, 0.15),
                    new(OffensiveAction.Isolation, 0.10),
                    new(OffensiveAction.Cut, 0.10),
                    new(OffensiveAction.PostUp, 0.05),
                    new(OffensiveAction.OffBallScreen, 0.00),
                ],
            },
            new()
            {
                Tactic = OffensiveTactic.PickAndRoll,
                ShotBias = 0.02,
                QualityBonus = 0.03,
                Weights =
                [
                    new(OffensiveAction.PickAndRoll, 0.45),
                    new(OffensiveAction.Drive, 0.15),
                    new(OffensiveAction.SpotUp, 0.15),
                    new(OffensiveAction.Isolation, 0.10),
                    new(OffensiveAction.Cut, 0.10),
                    new(OffensiveAction.PostUp, 0.05),
                    new(OffensiveAction.OffBallScreen, 0.00),
                ],
            },
            new()
            {
                Tactic = OffensiveTactic.PerimeterMotion,
                ShotBias = 0.04,
                QualityBonus = 0.02,
                Weights =
                [
                    new(OffensiveAction.PickAndRoll, 0.12),
                    new(OffensiveAction.Drive, 0.18),
                    new(OffensiveAction.SpotUp, 0.25),
                    new(OffensiveAction.Isolation, 0.10),
                    new(OffensiveAction.Cut, 0.22),
                    new(OffensiveAction.PostUp, 0.13),
                    new(OffensiveAction.OffBallScreen, 0.00),
                ],
            },
            new()
            {
                Tactic = OffensiveTactic.InsidePost,
                ShotBias = -0.04,
                QualityBonus = 0.02,
                Weights =
                [
                    new(OffensiveAction.PickAndRoll, 0.10),
                    new(OffensiveAction.Drive, 0.15),
                    new(OffensiveAction.SpotUp, 0.10),
                    new(OffensiveAction.Isolation, 0.20),
                    new(OffensiveAction.Cut, 0.12),
                    new(OffensiveAction.PostUp, 0.33),
                    new(OffensiveAction.OffBallScreen, 0.00),
                ],
            },
        ],
    };
}
