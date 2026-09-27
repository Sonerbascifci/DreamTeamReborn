using System.Collections.Immutable;
using System.Linq;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Fatigue;

/// <summary>
/// Enerji tüketimi ve toparlanması (D58, D63, D64).
///
/// <para><b>On oyuncu kuralı (05 §164, T12f).</b> Enerji güncellemesi yalnız
/// aktif oyuncuya yapılmaz: her canlı süre tüketiminde sahadaki beş <c>drain</c>,
/// yedek beş <c>recovery</c> alır. <c>SecondsOnCourt</c> yalnız sahadakiler için
/// artar; böylece toplam oynama süresi 5 oyuncu × geçen süre olur ve T12c bunu
/// doğrulayabilir.</para>
///
/// <para><b>Enerji sıfır bir nokta değil, bir tabandır</b> (D63): performans çarpanı
/// <c>FatigueModel.PerformanceCurve</c>'un son kancasında biter ve oradan aşağı
/// inmez. <c>Energy</c> her zaman <c>[0,100]</c>'e kırpılır.</para>
///
/// <para><b>Enerji yalnız şuta girer</b> (D64): asist, ribaund, top kaybı, blok
/// ve faul çekilişleri enerjiden etkilenmez.</para>
/// </summary>
public static class FatigueCalculator
{
    /// <summary>Saniyelik enerji kaybı: stamina ne kadar yüksekse o kadar az.</summary>
    public static double DrainPerSecond(FatigueModel model, PlayerRatings ratings, double paceMultiplier)
    {
        ArgumentNullException.ThrowIfNull(model);

        var staminaFactor = (100 - ratings.Stamina) / 100.0;

        return Math.Max(0.0, model.BaselineDrainPerSecond * staminaFactor * paceMultiplier);
    }

    /// <summary>Saniyelik toparlanma: stamina 50 referans, 0.5-2.0 aralığında çarpan.</summary>
    public static double RecoveryPerSecond(FatigueModel model, PlayerRatings ratings) =>
        Math.Max(0.0, model.BaselineRecoveryPerSecond * (ratings.Stamina / 50.0));

    /// <summary>
    /// Bir canlı süre aralığının enerji etkisini hesaplar ve **on oyuncuya**
    /// yazar: sahadaki beş <c>drain</c>, yedek beş <c>recovery</c> (05 §164, T12f).
    /// </summary>
    /// <param name="roster">Tüm kadro; yedeklerin <c>Stamina</c>'sı da okunur.</param>
    /// <param name="onCourt">Sahadaki beş; süre sayacı yalnız bunları artırır.</param>
    public static ImmutableArray<PlayerMatchState> Advance(
        FatigueModel model,
        ImmutableArray<PlayerMatchState> states,
        ImmutableArray<Player> roster,
        ImmutableArray<Player> onCourt,
        long liveMilliseconds,
        double paceEnergyMultiplier)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (liveMilliseconds <= 0)
        {
            return states;
        }

        var seconds = liveMilliseconds / 1000.0;
        var byId = new Dictionary<Guid, Player>(roster.Length);

        foreach (var player in roster)
        {
            byId[player.Id] = player;
        }

        var onCourtIds = onCourt.Select(player => player.Id).ToHashSet();

        return states.Select(state =>
        {
            if (!byId.TryGetValue(state.PlayerId, out var player))
            {
                // Kadro dışı oyuncu: ne drain ne recovery. Savunma amaçlı sessiz
                // bir yol; <c>EligibilityPolicy</c> zaten böyle bir oyuncuya izin
                // vermez.
                return state;
            }

            if (onCourtIds.Contains(state.PlayerId))
            {
                return state with
                {
                    Energy = Clamp(
                        state.Energy
                        - (DrainPerSecond(model, player.Ratings, paceEnergyMultiplier) * seconds)),
                    SecondsOnCourt = state.SecondsOnCourt + (liveMilliseconds / 1000.0),
                };
            }

            return state with
            {
                Energy = Clamp(
                    state.Energy + (RecoveryPerSecond(model, player.Ratings) * seconds)),
            };
        }).ToImmutableArray();
    }

    /// <summary>
    /// Periyot arası toparlanma. Canlı süre <b>sayılmaz</b>; bu yüzden
    /// <c>SecondsOnCourt</c> değişmez (T12c'nin toplamı bozulmasın diye).
    /// </summary>
    public static ImmutableArray<PlayerMatchState> RecoverDuringBreak(
        FatigueModel model,
        ImmutableArray<PlayerMatchState> states,
        long breakMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (breakMilliseconds <= 0)
        {
            return states;
        }

        var seconds = breakMilliseconds / 1000.0;

        return states.Select(state => state with
        {
            Energy = Clamp(state.Energy + (model.BreakRecoveryPerSecond * seconds)),
        }).ToImmutableArray();
    }

    /// <summary>
    /// Enerjiyi gözlem/rapor için tam sayıya yuvarlar. <b>Yalnız çıktıda</b> kullanılır;
    /// hesapta <see cref="PlayerMatchState.Energy"/> kesirli kalır.
    /// </summary>
    public static int ForDisplay(double energy) =>
        (int)Math.Round(
            Math.Clamp(energy, 0.0, FatigueModel.MaxEnergy),
            MidpointRounding.AwayFromZero);

    /// <summary>
    /// <c>fatigueLoad</c>: 05 §7'nin z formülünde kullanılan <c>[0,1]</c> yorgunluk
    /// yükü. 1.0 = tam enerji, 0.0 = tükenmiş.
    /// </summary>
    public static double FatigueLoad(FatigueModel model, double energy) =>
        Math.Clamp(1.0 - model.PerformanceMultiplier(energy), 0.0, 1.0);

    /// <summary>
    /// Enerjiyi <c>[0, MaxEnergy]</c> aralığına kırpar. <b>Yuvarlama yapmaz</b>:
    /// yuvarlama hesap sırasında olursa kesirli drain geri döner ve enerji hiç
    /// düşmez. Yuvarlama yalnız görüntülemede uygulanır.
    /// </summary>
    public static double Clamp(double energy) =>
        Math.Clamp(energy, 0.0, FatigueModel.MaxEnergy);
}
