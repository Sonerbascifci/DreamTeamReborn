using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

/// <summary>Seçilen aksiyonun oyuncuya bağlanmış hâli.</summary>
public sealed record ActionSelection
{
    public required OffensiveAction Action { get; init; }

    public required ShotType ShotType { get; init; }

    /// <summary>Aksiyonu yürüten oyuncu.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>Bu oyuncu için okunan birincil beceri attribute'ü (0-100).</summary>
    public required int SkillRating { get; init; }

    public required ActionProfile Profile { get; init; }
}

/// <summary>
/// Aksiyon ve oyuncu seçimi. 05 §5'in M2'deki basitleştirilmiş hâli.
///
/// İki aşamalı ve deterministik:
/// 1. Aksiyon, <see cref="ActionProfile"/> ağırlıklarından seçilir.
/// 2. Oyuncu, sahadaki beş arasından aksiyonun birincil beceri attribute'üne
///    göre ağırlıklandırılır; ağırlık <c>attribute + 1</c>'dir, böylece toplam
///    sıfır olamaz ve hiçbir oyuncu asla seçilemez hale gelmez.
///
/// Bu bir <b>yer tutucudur</b>: 18 attribute'un türetilmiş composite'leri,
/// kullanım tercihi ve rol uyumu M4'te gelir. M2'de pozisyon dışı oynatma veya
/// uyumsuzluk cezası <b>yoktur</b> — 02 §5, gizli ceza uydurulmamasını ister.
/// </summary>
public sealed class ActionSelector
{
    private readonly IReadOnlyList<ActionProfile> _activeProfiles;
    private readonly IReadOnlyList<Player> _onCourt;

    public ActionSelector(
        ImmutableArray<ActionProfile> profiles,
        IReadOnlyList<Player> onCourt)
    {
        // Sıfır ağırlıklı profiller aday listesine hiç girmez. 05 §5: uygun olmayan
        // aksiyon filtrelenirse kalan ağırlıklar yeniden normalize edilir;
        // WeightedSelector zaten mutlak ağırlıklarla çalışır, normalize etmeye gerek yok.
        _activeProfiles = [.. profiles.Where(profile => profile.Weight > 0.0)];

        if (_activeProfiles.Count == 0)
        {
            throw new InvalidOperationException(
                "Aksiyon dağılımında ağırlığı sıfırdan büyük profil yok; motor ilerleyemez.");
        }

        _onCourt = onCourt;

        if (onCourt.Count == 0)
        {
            throw new InvalidOperationException("Sahada oyuncu yok; aksiyon seçilemez.");
        }
    }

    public ActionSelection Select(IRandomSource random)
    {
        var profile = WeightedSelector.Select(_activeProfiles, candidate => candidate.Weight, random);

        // Ağırlık attribute + 1: toplam sıfır olamaz, seçim her zaman mümkündür.
        var player = WeightedSelector.Select(
            _onCourt,
            candidate => profile.Skill(candidate.Ratings) + 1.0,
            random);

        return new ActionSelection
        {
            Action = profile.Action,
            ShotType = profile.ShotType,
            PlayerId = player.Id,
            SkillRating = profile.Skill(player.Ratings),
            Profile = profile,
        };
    }
}
