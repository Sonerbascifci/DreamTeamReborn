using System.Collections.Immutable;
using DreamTeam.Domain.Players;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Yedekleme ve uygunluk kuralları (06 §7, D41).
///
/// M3'te yalnız <b>foul-out sonrası zorunlu</b> değişiklik yapılır. Kullanıcı
/// değişikliği, substitution pencereleri ve timeout M5'in işidir ve burada yok.
///
/// "En uygun yedek" seçimi bilinçli olarak <b>yer tutucudur</b>: 18 attribute'ün
/// türetilmiş composite'i, rol uyumu ve kadro derinliği mantığı M4'te gelir.
/// Buradaki ölçüt açıkça belgelenmiştir ve deterministiktir.
/// </summary>
public static class EligibilityPolicy
{
    /// <summary>
    /// Sahada olmayan ve faulden çıkmamış oyuncular arasından yedek seçer.
    /// Yasal yedek yoksa <c>null</c> döner; çağıran taraf terminal policy uygular.
    ///
    /// Sıralama: BasketballIQ azalan, Stamina azalan, kanonik kadro sırası.
    /// Son kriter, eşitlikte tek ve deterministik sonuç verir.
    /// </summary>
    public static Player? SelectReplacement(TeamMatchState team)
    {
        ArgumentNullException.ThrowIfNull(team);

        var onCourt = new HashSet<Guid>(team.OnCourt.Select(player => player.Id));

        return team.Roster
            .Where(player => !onCourt.Contains(player.Id))
            .Where(player => !team.FoulOutPlayerIds.Contains(player.Id))
            .OrderByDescending(player => player.Ratings.BasketballIQ)
            .ThenByDescending(player => player.Ratings.Stamina)
            .ThenBy(player => player.Id)
            .FirstOrDefault();
    }

    /// <summary>Bu oyuncu sahada olabilir mi? Faulden çıkmışsa hayır.</summary>
    public static bool IsLegal(TeamMatchState team, Guid playerId) =>
        !team.FoulOutPlayerIds.Contains(playerId);

    /// <summary>Takımın sahada yasal beş kişisi var mı?</summary>
    public static bool HasLegalFive(TeamMatchState team)
    {
        if (team.OnCourt.Length == 0)
        {
            return false;
        }

        return team.OnCourt.All(player => IsLegal(team, player.Id));
    }

    /// <summary>
    /// Bu oyuncu faulden çıkmış mı? (D40: kişisel sınır 6, 6. faulde çıkar.)
    /// </summary>
    public static bool IsFouledOut(FoulCounters fouls, Guid playerId, int limit) =>
        fouls.PersonalOf(playerId) >= limit;
}
