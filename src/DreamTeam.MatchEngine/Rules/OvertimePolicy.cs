using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Rules;

/// <summary>
/// Uzatma ust siniri (D79).
///
/// <para><b>Neden gerekti.</b> M4 sonrasi motor uzatmayi "esitlik bozulana
/// kadar" acmaya devam ediyordu. Puan atilamayan bir fixture'da (orn.
/// <c>ShotCompletionProbability = 0</c> ve faul yok) skor hep 0-0 kaldi ve
/// <b>521 periyot</b> uretildi; yalniz eylem guard'i durdurdu. 08 T10 "guard ->
/// Aborted" dedigi icin bu spec'e uygundu, ama urunde gorunur bir kalite
/// sorunuydu.</para>
///
/// <para><b>Karar.</b> En fazla <see cref="RulesProfile.MaxOvertimePeriods"/>
/// uzatma oynanir. Sinira gelindiginde skor hala esitse mac
/// <c>Aborted</c> olur. 06 §8 geregi:</para>
/// <list type="bullet">
///   <item><description><b>Kazanan UYDURULMAZ.</description></item>
///   <item><description><c>IsTie = false</c> kalir: yarim kalan mac beraberlik
///   sayilmaz, skoru gecersizdir.</description></item>
///   <item><description><c>MatchEnded</c> YAZILMAZ; yalniz <c>MatchAborted</c>.</description></item>
/// </list></para>
///
/// <para>Bu bir <b>guvenlik siniridir</b>, denge parametresi degil. Degeri
/// <c>ConfigHash</c>'e girdigi icin iki farkli sinirli config ayrisir.</para>
/// </summary>
public static class OvertimePolicy
{
    /// <summary>
    /// Bu periyot bittikten sonra yeni bir uzatma AÇILMALI mı?
    /// </summary>
    public static bool ShouldStartOvertime(
        RulesProfile rules,
        MatchClock clock,
        int homeScore,
        int awayScore)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (homeScore != awayScore)
        {
            return false;
        }

        // Simdiye kadar oynanan uzatma sayisi.
        var overtimePlayed = Math.Max(0, clock.Period - rules.PeriodCount);

        // clock.Period henuz "bittiği periyot"tur; oynanan uzatma sayisi
        // PeriodCount'un uzerindeki fark kadardir. Sinir asildiysa yeni uzatma
        // acilmaz.
        return overtimePlayed < rules.MaxOvertimePeriods;
    }

    /// <summary>Sinir neden duruldu? <c>null</c> ise durulmadi.</summary>
    public static string? LimitReachedReason(RulesProfile rules, MatchClock clock, int homeScore, int awayScore)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (homeScore != awayScore)
        {
            return null;
        }

        var overtimePlayed = Math.Max(0, clock.Period - rules.PeriodCount);

        return overtimePlayed >= rules.MaxOvertimePeriods
            ? $"Uzatma ust siniri ({rules.MaxOvertimePeriods}) doldu ve skor hala esit "
                + $"({homeScore}-{awayScore}). Kazanan secilmedi; mac tamamlanmadi (D79)."
            : null;
    }

    /// <summary>
    /// Toplam oynanabilecek periyot sayisi: duzenleme periyotlari + ust sinirdaki
    /// uzatmalar. Testler ve rapor bunu kullanir.
    /// </summary>
    public static int MaxTotalPeriods(RulesProfile rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return rules.PeriodCount + rules.MaxOvertimePeriods;
    }
}
