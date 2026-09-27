using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Ratings;

/// <summary>
/// Takım OVR'si. <b>GÖSTERİM AMAÇLIDIR</b> (D23/D24, T03).
///
/// <para><b>Bu tip hiçbir çözüm girdisi değildir.</b> Hiçbir resolver, hiçbir
/// ağırlık hesabı ve hiçbir olasılık OVR'ı okumaz. OVR yalnız
/// <c>MatchResult</c> ve rapor çıktısında görünür, çünkü kullanıcıya "bu kadro
/// ne kadar güçlü" özetini verir. 00 §33, tek OVR karşılaştırmasıyla otomatik
/// taktik galibiyeti üretilmesini yasaklar; burada öyle bir yol yoktur.</para>
///
/// <para><b>T03 nasıl kanıtlanır:</b> OVR ağırlık seti değiştirilip aynı fixture
/// ve seed ile oynandığında maç sonucu ve event akışı bit düzeyinde aynı kalır.
/// <c>OverallRatingChangeDoesNotAlterMatchOutcome</c> testi bunu ölçer.</para>
///
/// <para>Ağırlıklar <see cref="EngineConfig"/>'de yaşar ve <see cref="PlayerRatingCalculator"/>'dan
/// <b>bilinçli olarak ayrıdır</b> — aynı ağırlıkları paylaşmak, OVR'yi dolaylı
/// yoldan çözüm girdisi yapardı.</para>
/// </summary>
public sealed class TeamRatingCalculator
{
    private readonly ImmutableArray<OverallWeight> _weights;
    private readonly PlayerRatingCalculator _composites;

    public TeamRatingCalculator(
        ImmutableArray<OverallWeight> weights,
        PlayerRatingCalculator composites)
    {
        if (weights.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "OVR ağırlık seti boş olamaz.",
                nameof(weights));
        }

        _weights = weights;
        _composites = composites;
    }

    /// <summary>
    /// Sahadaki ilk beşin ağırlıklı composite ortalaması, <c>[0,100]</c>'e
    /// kırpılmış. Yedekler **girermez**: OVR bir maç başlangıç beşini tanımlar.
    /// </summary>
    public int Overall(ImmutableArray<Player> onCourt)
    {
        if (onCourt.IsDefaultOrEmpty)
        {
            return 0;
        }

        double total = 0.0;
        var applied = 0.0;

        foreach (var weight in _weights)
        {
            var composite = weight.Composite(onCourt);

            total += weight.Weight * composite;
            applied += weight.Weight;
        }

        if (applied <= 0.0)
        {
            return 0;
        }

        return (int)Math.Round(Math.Clamp(total / applied, 0.0, 100.0), MidpointRounding.AwayFromZero);
    }

    /// <summary>Verilen lineup için OVR; <see cref="Team"/>'in ilk beşini kullanır.</summary>
    public int OverallFor(Team team, Lineup lineup)
    {
        var byId = team.Roster.ToDictionary(player => player.Id);
        var onCourt = ImmutableArray.CreateBuilder<Player>(lineup.PlayerIds.Length);

        foreach (var playerId in lineup.PlayerIds)
        {
            if (byId.TryGetValue(playerId, out var player))
            {
                onCourt.Add(player);
            }
        }

        return Overall(onCourt.ToImmutable());
    }

    public static TeamRatingCalculator Baseline { get; } = new(
        [
            new("Scoring", 0.25, lineup => Average(lineup, PlayerRatingTables.Scoring)),
            new("PerimeterDefense", 0.20, lineup => Average(lineup, PlayerRatingTables.PerimeterDefense)),
            new("InteriorDefense", 0.20, lineup => Average(lineup, PlayerRatingTables.InteriorDefense)),
            new("Rebounding", 0.15, lineup => Average(lineup, PlayerRatingTables.Rebounding)),
            new("Handle", 0.20, lineup => Average(lineup, PlayerRatingTables.Handle)),
        ],
        PlayerRatingCalculator.Baseline);

    private static int Average(ImmutableArray<Player> lineup, Func<PlayerRatings, int> project)
    {
        if (lineup.IsDefaultOrEmpty)
        {
            return 0;
        }

        long total = 0;

        foreach (var player in lineup)
        {
            total += project(player.Ratings);
        }

        return (int)Math.Clamp((double)total / lineup.Length, 0.0, 100.0);
    }
}

/// <param name="Label">Rapor çıktısında görünen ad.</param>
/// <param name="Weight">OVR ağırlığı. Çözüm girdisi <b>değildir</b>.</param>
/// <param name="Composite">Lineup üzerinden uygulanacak composite.</param>
public readonly record struct OverallWeight(
    string Label,
    double Weight,
    Func<ImmutableArray<Player>, int> Composite);
