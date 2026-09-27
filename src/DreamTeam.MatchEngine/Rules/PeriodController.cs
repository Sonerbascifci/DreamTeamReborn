using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Periyot ve uzatma kontrolü (06 §8, D42).
///
/// Saf fonksiyonlardan oluşur; motorun saat ilerletme kodu buraya danışmadan
/// karar veremez. Böylece "hangi periyotta kaç dakika var", "uzatma açılır mı"
/// ve "faul sayacı sıfırlanır mı" sorularının tek cevabı olur.
/// </summary>
public static class PeriodController
{
    /// <summary>1..PeriodCount arası normal süre, sonrası uzatma süresi.</summary>
    public static long DurationMsForPeriod(RulesProfile rules, int period) =>
        period <= rules.PeriodCount ? rules.PeriodDurationMs : rules.OvertimeDurationMs;

    public static bool IsOvertime(RulesProfile rules, int period) => period > rules.PeriodCount;

    /// <summary>
    /// Takım faulu <b>periyot sayacıdır</b>: <c>TeamFoulsThisPeriod</c> her
    /// periyot başında sıfırlanır. D42'nin uzatma şartı ("her uzatmada faul
    /// sayacı sıfırlanır") bunun doğal sonucudur; ayrı bir kural gerekmez.
    ///
    /// Bu, NBA'daki periyot içi bonus kuralıyla da aynıdır. Önceki sürüm
    /// yalnız uzatmada sıfırlıyordu ve normal periyotta 4. periyottan devreden
    /// sayı 5. periyotta bonusa yol açıyordu; bu bir uygulama hatasıydı.
    /// </summary>
    public static bool ResetsTeamFouls(RulesProfile rules, int period) => period >= 1;

    /// <summary>
    /// Uzatma açılmalı mı? Son normal periyot bitti ve skor eşitse evet.
    /// M3'te rastgele kazanan seçilmez; uzatma oynanır (06 §8).
    /// </summary>
    public static bool ShouldStartOvertime(RulesProfile rules, int finishedPeriod, int homeScore, int awayScore) =>
        finishedPeriod >= rules.PeriodCount && homeScore == awayScore;

    /// <summary>Sonraki periyot numarası.</summary>
    public static int NextPeriod(int finishedPeriod) => finishedPeriod + 1;
}
