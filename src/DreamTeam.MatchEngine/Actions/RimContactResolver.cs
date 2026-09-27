using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

/// <summary>
/// Çember teması çözümü (M3, 06 §6).
///
/// Taban olasılığı: atış çemberi yüzünden kaçtığında hücum ribaundu hücum saatini
/// 14 saniyeye çeker; havada kalan (airball) bir şutta bu reset <b>olmaz</b>.
///
/// Bu değer şu an yalnız saat politikasını besler. M4'te çember teması şut
/// kalitesine de girecektir; o zaman iki yerde ayrı ayrı etki yazılması
/// 05 §3'teki "iki kat sayma" hatasına dönüşür, bu yüzden tek yerde toplanır.
/// </summary>
public sealed class RimContactResolver
{
    private readonly ShotModel _model;

    public RimContactResolver(ShotModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public bool TouchedRim(IRandomSource random) => random.NextDouble() < _model.RimContactProbability;
}
