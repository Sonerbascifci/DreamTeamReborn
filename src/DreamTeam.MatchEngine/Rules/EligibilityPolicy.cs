using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Ratings;

namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Yedekleme ve uygunluk kuralları (06 §7, D41).
///
/// M3'te yalnız <b>foul-out sonrası zorunlu</b> değişiklik yapılır. Kullanıcı
/// değişikliği, substitution pencereleri ve timeout M5'in işidir ve burada yok.
///
/// <para><b>M4'te yedek seçimi composite'e dayanır (D65).</b> M3'te
/// "en uygun yedek" iki ham attribute (BasketballIQ, Stamina) ile tanımlıydı ve
/// 18 attribute'luk ikinci bir tablo kopyalamamak gerekmişti. M4'te tek
/// kanonik tablo (<see cref="PlayerRatingTables"/>) var ve yedek seçimi bunun
/// üzerinden yapılır. Sıralama: genel composite azalan, sonra BasketballIQ
/// azalan, sonra kanonik kadro sırası — son iki kriter eşitlikte tek ve
/// deterministik sonuç verir.</para>
///
/// <para>Bu hâlâ bir <b>yer tutucudur</b>: rol uyumu, kadro derinliği ve
/// oyuncu kısıtı mantığı M5'in substitution işidir.</para>
/// </summary>
public static class EligibilityPolicy
{
    /// <summary>
    /// Sahada olmayan ve faulden çıkmamış oyuncular arasından yedek seçer.
    /// Yasal yedek yoksa <c>null</c> döner; çağıran taraf terminal policy uygular.
    /// </summary>
    public static Player? SelectReplacement(TeamMatchState team)
    {
        ArgumentNullException.ThrowIfNull(team);

        var onCourt = new HashSet<Guid>(team.OnCourt.Select(player => player.Id));

        return team.Roster
            .Where(player => !onCourt.Contains(player.Id))
            .Where(player => !team.FoulOutPlayerIds.Contains(player.Id))
            .OrderByDescending(player => OverallComposite(player))
            .ThenByDescending(player => player.Ratings.BasketballIQ)
            .ThenBy(player => player.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// Yedek sıralamasında kullanılan genel composite. Enerji bilerek
    /// <b>okunmaz</b> (D58): bu saf bir statik kalite ölçüsüdür ve maç içi
    /// yorgunluğa göre sıralama yapmaz. Foul-out bir anda olay olduğundan
    /// oyuncunun enerjisi o kararda zaten anlamlı bir ölçüt değildir.
    /// </summary>
    private static int OverallComposite(Player player)
    {
        var r = player.Ratings;

        return (PlayerRatingTables.Handle(r)
            + PlayerRatingTables.PerimeterDefense(r)
            + PlayerRatingTables.InteriorDefense(r)
            + PlayerRatingTables.Rebounding(r)
            + PlayerRatingTables.Athleticism(r)
            + PlayerRatingTables.Scoring(r)
            + PlayerRatingTables.Interior(r)) / 7;
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
