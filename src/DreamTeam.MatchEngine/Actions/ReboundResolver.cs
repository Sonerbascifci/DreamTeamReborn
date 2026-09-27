using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Actions;

/// <param name="Offensive">True ise hücum ribaundudur ve possession kimliği korunur.</param>
/// <param name="RebounderId">Ribaundu alan oyuncu.</param>
public readonly record struct ReboundOutcome(bool Offensive, Guid RebounderId);

/// <summary>
/// Canlı ribaund fırsatının çözümü. 05 §10.
///
/// Yalnız <b>canlı bir miss</b> sonrası çağrılır. Periyot sonu, ölü top ve
/// serbest atış arası durumlar ribaund üretmez; bu çağrı yeri kuralıdır, seçim
/// değildir. 06 §5, "her missed attempt'e otomatik oyuncu ribaundu dağıtma" hatasını
/// yasaklar.
///
/// Takım ribaundu M2'de <b>üretilmez</b>: 08 §2, takım ribaundu oyuncuya rastgele
/// dağıtılamaz ve M2'de sahada ribaund alacak beş oyuncu her zaman vardır.
/// Bu yüzden sonuç daima oyuncuya atfedilir; takım ribaundu sayacı M2'de sıfırdır.
///
/// Rebaund alan oyuncu, hücumun sahadaki beşi içinde <c>Rebounding + 1</c> ağırlığıyla
/// seçilir; tüm değerler sıfır olsa bile seçim yapılabilir.
/// </summary>
public sealed class ReboundResolver
{
    private readonly ActionModel _model;

    public ReboundResolver(ActionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    public ReboundOutcome Resolve(ImmutableArray<Player> offensiveOnCourt, IRandomSource random)
    {
        if (offensiveOnCourt.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException("Ribaund için hücum sahadaki beş bilinmiyor.");
        }

        var offensive = random.NextDouble() < _model.OffensiveReboundProbability;
        var rebounder = WeightedSelector.Select(
            offensiveOnCourt,
            player => player.Ratings.Rebounding + 1.0,
            random);

        return new ReboundOutcome(offensive, rebounder.Id);
    }
}
