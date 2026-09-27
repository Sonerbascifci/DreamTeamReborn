using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Ratings;

namespace DreamTeam.MatchEngine.Tactics;

/// <summary>
/// Dört savunma policy paketinin (D57) davranışı. Her policy üç kanalı besler:
/// şut kalitesi, blok çekilişi ve top kaybı baskısı; faul disiplini ayrıdır.
///
/// <para><b>Kanal ayrımı kuraldır.</b> 05 §7: "Savunma ve taktik ShotQuality
/// içine girdiyse z'ye aynı etkiyi tekrar ekleme." Bu yüzden savunmanın şuta etkisi
/// <b>yalnız</b> kalite kanalından geçer; blok ve top kaybı <b>ayrı</b> çekilişlerdir.
/// Hiçbir etki iki kez sayılmaz.</para>
///
/// <para><b>Kalibre edilmemiştir.</b> 05 §6'daki eski yüzdeler kopyalanmamıştır;
/// 05 onların onaylı matris olmadığını açıkça söyler. Buradaki değerler M3'ün
/// gözlenen sonuçlarını bozmayacak küçük kalmalar için seçilmiştir; asıl ölçüm
/// M6'dadır.</para>
/// </summary>
public sealed class DefensivePolicy
{
    private readonly DefenseModel _model;
    private readonly PlayerRatingCalculator _ratings;

    public DefensivePolicy(DefenseModel model, PlayerRatingCalculator ratings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(ratings);

        _model = model;
        _ratings = ratings;
    }

    /// <summary>
    /// Savunma policy'sinin <c>(action, defense)</c> çifti için <b>kalite puanı</b>
    /// etkisi. Negatif = savunma kaliteyi düşürür, pozitif = yükseltir.
    ///
    /// <para><b>Ölçek neden puan?</b> <see cref="ShotQualityResolver"/> kaliteyi
    /// <c>[0,100]</c> bir tam sayı olarak döndürür. 05 §6'nın eski yüzdeleri
    /// (<c>"Drop için midrange +6%"</c>) 0-1 ölçeğinde yazılmıştı; 0-100 ölçeğine
    /// çevrilmeden, 0.07 gibi bir fark yuvarlamada kaybolur ve policy'ler arası
    /// fark <b>gözlenemez</b> hâle gelir. Bu, uygulamada ölçüldü: 0.07 ile 0.02
    /// farkı her ikisi de 52'ye yuvarlanıyordu. Ölçek doğrudan puan olarak
    /// yazıldı.</para>
    ///
    /// <para>Değerler <b>kalibre edilmemiştir</b>; 05 §6 bunları onaylı matris
    /// olarak tanımlamaz. Yönler 05 §6'nın tarifinden gelir.</para>
    /// </summary>
    public double QualityPenalty(OffensiveAction action, DefensiveTactic defense)
    {
        // 05 §6: PnR coverage türleri Drop/Switch'tir. ManToMan ve Zone daha
        // geniş şemalardır; PnR'ı özel bir kanalla ele almazlar.
        var isRoll = action == OffensiveAction.PickAndRoll;

        return defense switch
        {
            // Drop: roll oyuncunun önü açık, alıcı düşük önlem alır.
            DefensiveTactic.Drop when isRoll => -7.0,
            // Switch: her bire bir baskı, primer değişir.
            DefensiveTactic.Switch when isRoll => -2.0,
            // Switch isolation'i en çok zorlar: primer savunmacı değişir.
            DefensiveTactic.Switch when action == OffensiveAction.Isolation => -6.0,
            // ZonePackPaint: iç alan kapalı.
            DefensiveTactic.ZonePackPaint
                when action is OffensiveAction.Drive or OffensiveAction.PostUp => -5.0,
            // ... dışarı serbest.
            DefensiveTactic.ZonePackPaint when action == OffensiveAction.SpotUp => 4.0,
            // ManToMan: nötr. PnR'da topu takip etmenin maliyeti burada.
            DefensiveTactic.ManToMan when isRoll => -3.0,
            _ => 0.0,
        };
    }

    /// <summary>
    /// Blok çekiliği. Sabit 0.06 yerine savunmacının iç savunma composite'inden
    /// türetilir; böylece blok artık rating'e bağlıdır (risk 9).
    /// </summary>
    public double BlockProbability(int defenderInteriorDefense)
    {
        var value = _model.BlockBase
            + (_model.BlockFromInteriorDefense * PlayerRatingTables.Normalize(defenderInteriorDefense));

        return Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>
    /// Savunma baskısının top kaybı çekilişine eklediği pay (<c>[0,1]</c>).
    /// Tempo buraya girmez (D59).
    /// </summary>
    public double TurnoverPressure(int defenderPerimeterDefense)
    {
        var value = _model.PressureBase
            + (_model.PressureFromPerimeterDefense * PlayerRatingTables.Normalize(defenderPerimeterDefense));

        return Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Faul çekilişine savunma disiplininin eklediği pay.</summary>
    public double FoulAggression(DefensiveTactic defense) => defense switch
    {
        // Zone kendi bölgesinde agresif kalabalık yapar.
        DefensiveTactic.ZonePackPaint => _model.FoulFromAggression,
        // ManToMan bire bir: kovalama ve eklem faulleri daha fazla.
        DefensiveTactic.ManToMan => _model.FoulFromAggression,
        // Drop ve Switch daha kontrollü.
        _ => 0.0,
    };

    /// <summary>Primer savunmacıyı seçerken kullanılacak composite.</summary>
    public int RoleComposite(PlayerRatings ratings, ShotType shotType) => shotType switch
    {
        ShotType.AtRim or ShotType.ClosePost => PlayerRatingTables.InteriorDefense(ratings),
        _ => PlayerRatingTables.PerimeterDefense(ratings),
    };

    /// <summary>Primer savunmacının seçim ağırlığı (D65 formülü).</summary>
    public double SelectionWeight(int roleComposite) => _ratings.SelectionWeight(roleComposite);
}
