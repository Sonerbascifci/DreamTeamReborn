using System.Collections.Immutable;
using DreamTeam.Domain.Players;

namespace DreamTeam.Domain.Teams;

/// <summary>
/// Kadro sıralamasının tek kanonik biçimi. Girdi sırası ne olursa olsun aynı
/// oyuncu kümesi aynı kanonik sırayı üretir; motor aday listelerini bu sırayla
/// gezer. Maç sırasında oyuncu adaylarının sırası oyun sonucunu değiştirmemelidir.
///
/// Birincil anahtar <see cref="Player.Id"/>, ikincil anahtar <see cref="Player.DisplayName"/>
/// (ordinal) olarak seçilir. İkincil anahtar yalnızca geçersiz girdide (yinelenen Id)
/// sıralamayı yine de tam ve tekrarlanabilir kılmak için vardır.
/// </summary>
public static class RosterOrdering
{
    public static ImmutableArray<Player> Canonical(ImmutableArray<Player> roster)
    {
        if (roster.IsDefaultOrEmpty)
        {
            return [];
        }

        return
        [
            .. roster
                .OrderBy(player => player.Id)
                .ThenBy(player => player.DisplayName, StringComparer.Ordinal),
        ];
    }
}
