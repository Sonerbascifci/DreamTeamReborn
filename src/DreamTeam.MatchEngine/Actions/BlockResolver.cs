using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Randomness;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Ratings;
using DreamTeam.MatchEngine.Tactics;

namespace DreamTeam.MatchEngine.Actions;

/// <summary>
/// Blok çözümü (05 §8 adım 4, T04).
///
/// <para><b>M4'te değişti.</b> M3'te blok olasılığı sabit bir config sayısıydı
/// (0.06) ve hiçbir rating'e bakmıyordu. M4'te savunmacının iç savunma
/// composite'inden türetilir; böylece blok artık kadro gücüne bağlıdır.</para>
///
/// <para>Blok, isabetten <b>ayrı</b> bir sonuçtur: bloke edilen şut kaçmış sayılır
/// ve canlı ribaund fırsatı doğar. 07 §3 gereği blok aynı <c>ShotId</c>'nin
/// niteliğidir; ikinci bir FGA yazılmaz ve isabet sayacına girmez.</para>
///
/// <para>Blok gerçekleşirse isabet çekilişi <b>tüketilmez</b>. Aynı şut iki kez
/// örneklenmesi 05 §127'nin yasakladığı çift örnekleme hatasıdır.</para>
/// </summary>
public sealed class BlockResolver
{
    private readonly DefensivePolicy _policy;

    public BlockResolver(DefensivePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    public bool IsBlocked(PlayerRatings defenderRatings, IRandomSource random) =>
        random.NextDouble() < _policy.BlockProbability(
            PlayerRatingTables.InteriorDefense(defenderRatings));
}
