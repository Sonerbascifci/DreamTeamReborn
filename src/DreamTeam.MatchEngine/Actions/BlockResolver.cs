using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

/// <summary>
/// Blok çözümü (05 §8 adım 4, T04).
///
/// Blok, isabetten <b>ayrı</b> bir sonuçtur: bloke edilen şut kaçmış sayılır ve
/// canlı ribaund fırsatı doğar. 07 §3 gereği blok aynı <c>ShotId</c>'nin niteliğidir;
/// ikinci bir FGA yazılmaz ve isabet sayacına girmez.
///
/// Blok gerçekleşirse isabet çekilişi <b>tüketilmez</b>. Aynı şut iki kez
/// örneklenmesi 05 §127'nin yasakladığı çift örnekleme hatasıdır.
/// </summary>
public sealed class BlockResolver
{
    private readonly ShotModel _model;

    public BlockResolver(ShotModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public bool IsBlocked(IRandomSource random) => random.NextDouble() < _model.BlockProbability;
}
