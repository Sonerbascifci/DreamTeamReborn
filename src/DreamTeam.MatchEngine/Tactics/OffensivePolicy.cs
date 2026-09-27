using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Ratings;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Tactics;

/// <summary>
/// Hücum taktiğini normalize edilmiş bir aksiyon dağılımına çevirir.
///
/// <para><b>Normalizasyon sözleşmesi (05 §5).</b> Ağırlığı sıfırdan büyük olmayan
/// aksiyonlar aday listesine <b>hiç girmez</b>; kalanlar yeniden normalize
/// edilir. Bu yüzden <see cref="WeightedSelector"/> mutlak ağırlıklarla çalışabilir
/// ve toplam 1.00'a eşitlenmesi gerekmez. Yine de <see cref="NormalizedWeights"/>
/// her zaman 1.00 döner: test edilebilir ve 09 §64'ün "geçerli bütün action
/// dağılımları normalize" kabul kriterini doğrudan karşılar.</para>
///
/// <para><b>Sıfır toplam açık hata verir.</b> Sessizce ilk oyuncuyu seçmek ya da
/// sıfıra bölmek 05 §5'in yasakladığı iki hatadır.</para>
/// </summary>
public sealed class OffensivePolicy
{
    private readonly TacticsModel _tactics;
    private readonly ImmutableArray<ActionProfile> _profiles;
    private readonly PlayerRatingCalculator _ratings;
    private readonly ImmutableDictionary<OffensiveTactic, ResolvedProfile> _resolved;

    public OffensivePolicy(
        TacticsModel tactics,
        ImmutableArray<ActionProfile> profiles,
        PlayerRatingCalculator ratings)
    {
        _tactics = tactics;
        _profiles = profiles;
        _ratings = ratings;

        var builder = ImmutableDictionary.CreateBuilder<OffensiveTactic, ResolvedProfile>();

        foreach (var tactic in Enum.GetValues<OffensiveTactic>())
        {
            builder.Add(tactic, Resolve(tactics.ProfileFor(tactic), profiles));
        }

        _resolved = builder.ToImmutable();
    }

    public ResolvedProfile For(OffensiveTactic tactic) => _resolved[tactic];

    private static ResolvedProfile Resolve(
        OffensiveTacticProfile profile,
        ImmutableArray<ActionProfile> allProfiles)
    {
        var byAction = allProfiles.ToDictionary(item => item.Action);
        var usable = new List<WeightedAction>();
        var total = 0.0;

        foreach (var weight in profile.Weights)
        {
            // 05 §5: uygun olmayan aksiyon (config'de profili yok) sessizce
            // yutulmaz; aday listesine giremez ve normalize toplam bunu yansıtır.
            if (weight.Weight <= 0.0 || !byAction.TryGetValue(weight.Action, out var actionProfile))
            {
                continue;
            }

            usable.Add(new WeightedAction(actionProfile, weight.Weight));
            total += weight.Weight;
        }

        if (total <= 0.0 || usable.Count == 0)
        {
            throw new InvalidOperationException(
                $"Taktik {profile.Tactic}: normalize edilebilir aksiyon yok. "
                + "Motor ilerleyemez; sessizce ilk aksiyonu seçmek 05 §5'in "
                + "yasakladığı hatadır.");
        }

        // 05 §5: kalan ağırlıklar yeniden normalize edilir.
        var normalized = new List<WeightedAction>(usable.Count);

        foreach (var item in usable)
        {
            normalized.Add(new WeightedAction(item.Profile, item.Weight / total));
        }

        return new ResolvedProfile
        {
            Tactic = profile.Tactic,
            Candidates = [.. normalized],
            ShotBias = profile.ShotBias,
            QualityBonus = profile.QualityBonus,
        };
    }

    /// <summary>
    /// Taktik dağılımından aksiyon ve oyuncu seçer. İki aşamalı ve deterministik:
    /// önce aksiyon, sonra oyuncu.
    /// </summary>
    public ActionSelection Select(
        OffensiveTactic tactic,
        ImmutableArray<Player> onCourt,
        IRandomSource random)
    {
        if (onCourt.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException("Sahada oyuncu yok; aksiyon seçilemez.");
        }

        var resolved = _resolved[tactic];
        var chosen = WeightedSelector.Select(resolved.Candidates, item => item.Weight, random);

        // D65: oyuncu ağırlığı bounded composite'den; ham rating degil.
        var player = WeightedSelector.Select(
            onCourt,
            candidate => _ratings.ActionSelectionWeight(chosen.Profile, candidate.Ratings),
            random);

        return new ActionSelection
        {
            Action = chosen.Profile.Action,
            ShotType = chosen.Profile.ShotType,
            PlayerId = player.Id,
            SkillRating = chosen.Profile.Skill(player.Ratings),
            Profile = chosen.Profile,
            Tactic = tactic,
        };
    }
}

/// <param name="Profile">Aksiyonun kimliği (şut türü ve beceri kanalı).</param>
/// <param name="Weight">Normalize edilmiş ağırlık; toplamı 1.00'dır.</param>
public readonly record struct WeightedAction(ActionProfile Profile, double Weight);

/// <summary>Bir taktiğin çözülmüş hali. Normalize vektör taktik başına bir kez hesaplanır.</summary>
public sealed record ResolvedProfile
{
    public required OffensiveTactic Tactic { get; init; }

    public required ImmutableArray<WeightedAction> Candidates { get; init; }

    public required double ShotBias { get; init; }

    public required double QualityBonus { get; init; }

    /// <summary>Kontrol ve test için: aday ağırlıklarının toplamı.</summary>
    public double NormalizedWeights
    {
        get
        {
            double total = 0.0;

            foreach (var candidate in Candidates)
            {
                total += candidate.Weight;
            }

            return total;
        }
    }
}
