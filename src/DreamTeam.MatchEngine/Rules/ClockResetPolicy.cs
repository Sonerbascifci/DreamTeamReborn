using DreamTeam.MatchEngine.Config;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// 06_RULES_AND_STATE_MACHINE.md §6'daki hücum saati reset tablosunun tamamı,
/// tek yerde ve saf fonksiyonlar olarak. Motorın saat kuralları dağınık
/// dallarda yazılmaz; her adım buradan geçer.
///
/// Tablodaki "değil" hükümler de kodla sabitlenmiştir:
/// - Timeout reset sebebi <b>değildir</b> (M5'te uygulanacak, burada yok).
/// - Çembere değmeyen miss + aynı hücum otomatik 14 s <b>almaz</b>.
/// - Savunma deflection hücumu sürdürürse reset <b>yoktur</b>.
/// </summary>
public static class ClockResetPolicy
{
    /// <summary>Rakip kontrolü ile yeni possession: 24 s (periyot başı da bu değer).</summary>
    public static long ForNewPossession(RulesProfile rules) => rules.ShotClockMs;

    /// <summary>
    /// Hücum ribaundu: yalnız <b>çembere değen</b> miss'te 14 s.
    /// Çembere değmeyen miss'te kalan süre aynen korunur.
    /// </summary>
    public static long? ForOffensiveRebound(RulesProfile rules, bool rimContact) =>
        rimContact ? rules.OffensiveReboundShotClockMs : null;

    /// <summary>
    /// Savunma non-shooting faulü ve aynı hücumun devamı: 14 s, ancak hücum saati
    /// hiçbir koşulda <b>artmaz</b>. Kalan süre 14 saniyeden büyükse olduğu gibi
    /// bırakılır.
    /// </summary>
    public static long ForDefensiveNonShootingFoul(RulesProfile rules, long currentShotClockMs) =>
        Math.Min(currentShotClockMs, rules.OffensiveReboundShotClockMs);
}
