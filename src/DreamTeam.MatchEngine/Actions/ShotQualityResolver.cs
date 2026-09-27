using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Ratings;
using DreamTeam.MatchEngine.Tactics;

namespace DreamTeam.MatchEngine.Actions;

/// <summary>
/// Şut kalitesi çözümü (05 §7). Sonuç <c>[0,100]</c> ve sonra
/// <c>NormalizeQuality</c> ile <c>[-1,1]</c>'e indirgenir.
///
/// <para><b>Kaliteyi kimler belirler?</b> 05 §7: "pas yaratımı, spacing, matchup,
/// IQ ve savunma baskısı." Burada dört kaynak toplanır:</para>
/// <list type="bullet">
///   <item><description>Taktik: <c>QualityBonus</c> (pas yaratımı/spacing)</description></item>
///   <item><description>Savunma: <c>DefensivePolicy.QualityPenalty</c> (baskı + matchup)</description></item>
///   <item><description>IQ: atak ve savunan oyuncunun zekası</description></item>
///   <item><description>Aksiyon tabanı: her aksiyonun kendi doğal kalitesi</description></item>
/// </list>
///
/// <para><b>Enerji buraya girmez.</b> D58: yorgunluk yalnız <c>z</c>'ye girer.
/// Kaliteyi de yorgunluktan etkilemek aynı cezayı iki kez uygulamak olurdu.</para>
///
/// <para><b>Uzay (spacing) vektörel değildir.</b> 05 §10, tam koordinat veya fizik
/// simülasyonu gerektirmediğini söyler. Burada spacing, mevcut oyuncunun kendi
/// becerisinden türetilen vektörel bir bileşendir; ayrı bir "kaç oyuncu çevrede"
/// sayımı <b>yapılmaz</b> — o, konum modeli gerektirirdi ve D35 konum cezasını
/// yasaklıyor.</para>
/// </summary>
public static class ShotQualityResolver
{
    /// <summary>Referans kalite: nötr taban. Ayar değil, tanım noktası.</summary>
    public const int BaseQuality = 50;

    public static double NormalizeQuality(int quality) =>
        (Math.Clamp(quality, 0, 100) - 50) / 50.0;

    /// <summary>
    /// Aksiyonun doğal taban kalitesi. 05 §8'deki sonuç ağacında aksiyonun
    /// kendi değeri vardır: yalın bir spot-up ile birebir post-up aynı değildir.
    /// </summary>
    private static int ActionBaseQuality(OffensiveAction action) => action switch
    {
        // Yalın, savunmacı elinde değil: en yüksek taban.
        OffensiveAction.SpotUp => 62,
        // Efsane: primer savunmacı belirsiz.
        OffensiveAction.Cut => 60,
        // Ayarlanmış, ekran ve iki savunmacı var: orta.
        OffensiveAction.PickAndRoll => 52,
        // Birebir, primer savunmacı sabit: biraz düşük.
        OffensiveAction.Isolation => 46,
        // Bit-çizgisi, kalabalık riski var.
        OffensiveAction.Drive => 48,
        // Post-up tek savunmacıya karşı ama yavaş: ara.
        OffensiveAction.PostUp => 50,
        _ => BaseQuality,
    };

    /// <summary>
    /// Şut kalitesini <c>[0,100]</c> olarak çözer. Tüm kaynaklar tek noktada
    /// toplanır; motor başka yerde kalite hesaplamaz.
    /// </summary>
    public static int Resolve(
        OffensiveAction action,
        OffensiveTactic offense,
        TacticsModel tactics,
        DefensiveTactic defense,
        DefensivePolicy policy,
        int shooterIq,
        int defenderIq)
    {
        ArgumentNullException.ThrowIfNull(tactics);
        ArgumentNullException.ThrowIfNull(policy);

        var profile = tactics.ProfileFor(offense);

        var raw = ActionBaseQuality(action)
            + (profile.QualityBonus * 100.0)
            + policy.QualityPenalty(action, defense);

        // IQ farki: pas yaratma ve karar kalitesi. Iki taraf da girer, cunku hem
        // atak hem savunma karari IQ'dan etkilenir.
        raw += IQTerm(shooterIq);
        raw -= IQTerm(defenderIq);

        return (int)Math.Round(Math.Clamp(raw, 0.0, 100.0), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// IQ'nun kaliteye etkisi, <b>kalite puani</b> olarak. Ham IQ farkini koymak
    /// yerine normalize edilmis bir katsayi kullanilir: 05 §3, etkilerin sinirli ve
    /// olculur olmasini ister. Olcek kalite puani oldugu icin 5 puanlik bir IQ
    /// farki 1 kalite puanina donusur; 0.001 olcek bir katsayi yuvarlamada
    /// tamamen kaybolurdu.
    /// </summary>
    private static double IQTerm(int iq) => 2.5 * PlayerRatingTables.Normalize(iq);
}
