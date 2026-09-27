using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Rules;

/// <summary>
/// Timeout butcesi ve yasal penceresi (D83, D84, 06 §18, 06 §6, 06 §27).
///
/// <para><b>Iki kanal etkisi ve ikisi de "yok":</b>
/// <list type="bullet">
///   <item><description>Canli saati <b>tuketmez</b> (06 §27: "substitution ve
///   dead-ball islemleri canli game clock tuketmez").</description></item>
///   <item><description>Hucrum saatini <b>baslatmaz</b> (06 §6 reset tablosu:
///   "Timeout, ayni hucrem devam - kendi basina reset sebebi degildir"). Ayni
///   hucrem kalan suresiyle devam eder.</description></item>
/// </list></para>
///
/// <para><b>D87 benzeri tuzak yok.</b> Burada enerji, skor veya baska bir
/// stokastik deger okunmaz; butce tam sayidir. Bu yuzden timeout uygulamasi
/// determinizmi bozmaz.</para>
///
/// <para><b>20 saniyelik timeout (D83).</b> Motorda yalniz <b>tipi** ve
/// butce etkisi vardir. 20 saniyenin duvar saati suresi canli runner'da (M7)
/// yasar; motor duvar saati tutmaz.</para>
/// </summary>
public static class TimeoutPolicy
{
    /// <summary>
    /// Bu takimin kullanabilecegi TAM timeout butcesi. Uzatma bonusu dahil
    /// (D84, 06 §18).
    /// </summary>
    public static int FullBudget(RulesProfile rules, MatchClock clock) =>
        rules.FullTimeoutsPerTeam
        + (OvertimeCount(rules, clock) * rules.OvertimeTimeoutBonus);

    /// <summary>Bu takimin kullanabilecegi 20 SANIYELIK timeout butcesi (D89).</summary>
    public static int ShortBudget(RulesProfile rules, MatchClock clock) =>
        rules.ShortTimeoutsPerTeam
        + (OvertimeCount(rules, clock) * rules.OvertimeTimeoutBonus);

    /// <summary>Oyunan uzatma periyodu sayisi (0, 1, 2 ...).</summary>
    public static int OvertimeCount(RulesProfile rules, MatchClock clock) =>
        Math.Max(0, clock.Period - rules.PeriodCount);

    public static int FullUsed(TeamMatchState team) => team.FullTimeoutsUsed;

    public static int ShortUsed(TeamMatchState team) => team.ShortTimeoutsUsed;

    /// <summary>
    /// Bu timeout harcanabilir mi?
    ///
    /// <para><b>D84 bütçe kuralı.</b> Toplam <c>FullTimeoutsPerTeam</c> adet
    /// tam timeout vardır; bunun <c>FullTimeoutsInFinalTwoMinutes</c> kadarı
    /// <b>yalnız</b> son iki dakikada kullanılabilir. Yani ilk
    /// <c>4 - 2 = 2</c> adet her dead-ball'da geçerlidir, 3. ve 4. yalnız
    /// son iki dakikada.</para>
    /// </summary>
    public static bool CanSpend(
        TeamMatchState team,
        RulesProfile rules,
        MatchClock clock,
        TimeoutKind kind,
        bool isFinalTwoMinutes)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(rules);

        if (kind == TimeoutKind.Short20)
        {
            return ShortUsed(team) < ShortBudget(rules, clock);
        }

        var budget = FullBudget(rules, clock);
        var used = FullUsed(team);

        if (used >= budget)
        {
            return false;
        }

        // Son iki dakikaya saklanan haklar: kullanildi mi?
        if (used < UnrestrictedFullTimeouts(rules))
        {
            return true;
        }

        return isFinalTwoMinutes;
    }

    /// <summary>
    /// Her dead-ball'da kullanilabilen tam timeout sayisi. Kalan
    /// <c>FullTimeoutsInFinalTwoMinutes</c> kadar hak son iki dakikaya saklanir.
    /// </summary>
    public static int UnrestrictedFullTimeouts(RulesProfile rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return Math.Max(0, rules.FullTimeoutsPerTeam - rules.FullTimeoutsInFinalTwoMinutes);
    }

    /// <summary>Reddetme sebebini insan-okunur hale getirir (07 §5 "reddetme sebebi kaydedilmeli").</summary>
    public static string DescribeRefusal(
        TeamMatchState team,
        RulesProfile rules,
        MatchClock clock,
        TimeoutKind kind,
        bool isFinalTwoMinutes)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(rules);

        if (kind == TimeoutKind.Short20)
        {
            return $"20 saniyelik timeout butcesi tukendi "
                + $"({ShortUsed(team)}/{ShortBudget(rules, clock)}).";
        }

        var used = FullUsed(team);
        var budget = FullBudget(rules, clock);

        if (used >= budget)
        {
            return $"Tam timeout butcesi tukendi ({used}/{budget}).";
        }

        return "Son iki dakikaya saklanan tam timeout hakki kaldi "
            + $"({used}/{budget}); bu pencere kapali. "
            + $"Serbest hak: {UnrestrictedFullTimeouts(rules)}.";
    }

    /// <summary>Timeout harcanmis takimin durumu. Sayaç <b>kümülatif</b> artar.</summary>
    public static TeamMatchState Spend(TeamMatchState team, TimeoutKind kind)
    {
        ArgumentNullException.ThrowIfNull(team);

        return kind == TimeoutKind.Short20
            ? team with { ShortTimeoutsUsed = team.ShortTimeoutsUsed + 1 }
            : team with { FullTimeoutsUsed = team.FullTimeoutsUsed + 1 };
    }
}
