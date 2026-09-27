using System.Collections.Immutable;
using DreamTeam.Domain.Teams;

namespace DreamTeam.MatchEngine.Config;

/// <summary>
/// Hücum taktiği. 02_GDD.md §6'daki dört seçenek. Her biri kendi aksiyon
/// dağılımını taşır; dağılımlar <see cref="TacticsModel"/> içinde veridir.
/// </summary>
public enum OffensiveTactic
{
    /// <summary>
    /// 05 §5'in verdiği varsayılan dağılım (D62). M3'ün tek düz vektörüyle
    /// birebir aynıdır; taktik etkisi diğer üç dağılımla ölçülür.
    /// </summary>
    Balanced,

    PickAndRoll,
    PerimeterMotion,
    InsidePost,
}

/// <summary>
/// Savunma policy'si. D57: 02 §6'daki dört seçenek, her biri kendi içinde
/// temel eşleşme + PnR coverage + paint/closeout tercihini taşıyan bir
/// <b>paket</b>tir. Ayrı bir "scheme" ve "coverage" katmanı yoktur; 05 §6'nın
/// devir önerisi budur ve 02 §6 ile de birebir uyumludur.
/// </summary>
public enum DefensiveTactic
{
    ManToMan,
    Drop,
    Switch,
    ZonePackPaint,
}

/// <summary>Tempo. 02 §6: Slow / Normal / Fast.</summary>
public enum Pace
{
    Slow,
    Normal,
    Fast,
}

/// <summary>
/// Bir takımın maç başlangıç girdisi. 04_DOMAIN_AND_DATA_MODEL.md §25'teki
/// <c>TeamMatchSetup</c> kaydının M4'e kadar olan tamamı: kimlik, kadro, lineup,
/// taktik ve tempo.
///
/// Bu tip <b>değişmez</b> bir snapshot'tır. Oynayan maç kendi dondurulmuş
/// girdisiyle ilerler; canlı taktik değişimi M5'tedir ve o zaman state'e yazılır,
/// buraya değil.
/// </summary>
public sealed record TeamMatchSetup
{
    public required Team Team { get; init; }

    public required Lineup Lineup { get; init; }

    public required OffensiveTactic Offensive { get; init; }

    public required DefensiveTactic Defense { get; init; }

    public required Pace Pace { get; init; }

    /// <summary>
    /// Tüm varsayılanlarla bir takım girdisi kurar. Test ve araç kodunun
    /// taktik söylemeden maç kurabilmesi için vardır; üretim kodunda
    /// <see cref="WithTactics"/> açıkça tercih edilmelidir.
    /// </summary>
    public static TeamMatchSetup Default(Team team, Lineup lineup) => new()
    {
        Team = team,
        Lineup = lineup,
        Offensive = OffensiveTactic.Balanced,
        Defense = DefensiveTactic.ManToMan,
        Pace = Pace.Normal,
    };

    public TeamMatchSetup WithTactics(
        OffensiveTactic offensive,
        DefensiveTactic defense,
        Pace pace) => this with
        {
            Offensive = offensive,
            Defense = defense,
            Pace = pace,
        };

    /// <summary>Kanonik kadro sırasıyla yedekler. M5'te substitution mantığı bunu kullanır.</summary>
    public ImmutableArray<Guid> ReservePlayerIds()
    {
        var onCourt = Lineup.PlayerIds.ToHashSet();

        return [.. RosterOrdering.Canonical(Team.Roster)
            .Where(player => !onCourt.Contains(player.Id))
            .Select(player => player.Id)];
    }
}
