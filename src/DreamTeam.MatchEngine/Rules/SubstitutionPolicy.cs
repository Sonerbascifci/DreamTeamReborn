using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Rules;

/// <summary>
/// Substitution kurallari (06 §7, D82, D85).
///
/// <para><b>Enerji buraya girmez.</b> D58/D70: yorgunluk yalniz z'ye girer.
/// "En uygun yedek" siralamasi statik kalite olcusudur; mac ici enerji
/// kararinda ayirt edici DEGILDIR ve bu yuzden okunmaz. Ayni gerekce M3'teki
/// <c>EligibilityPolicy</c> icin de gecerliydi.</para>
/// </summary>
public static class SubstitutionPolicy
{
    /// <summary>Oyuncunun kadroda olup olmadigi.</summary>
    public static bool IsInRoster(TeamMatchState team, Guid playerId)
    {
        ArgumentNullException.ThrowIfNull(team);

        foreach (var player in team.Roster)
        {
            if (player.Id == playerId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Oyuncunun sahada olup olmadigi.</summary>
    public static bool IsOnCourt(TeamMatchState team, Guid playerId)
    {
        ArgumentNullException.ThrowIfNull(team);

        foreach (var player in team.OnCourt)
        {
            if (player.Id == playerId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Oyuncunun foul-out olup olmadigi (06 §7: geri gelemez).</summary>
    public static bool IsFouledOut(TeamMatchState team, Guid playerId)
    {
        ArgumentNullException.ThrowIfNull(team);

        return team.FoulOutPlayerIds.Contains(playerId);
    }

    /// <summary>
    /// Degisiklikten SONRAKI bes ki. <b>Slot sirasi korunur</b>: cikan oyuncunun
    /// yerine giren yazar, boylece lineup sirasi degismez. Bu, sonraki tum
    /// "ilk bes" yorumlarinin (kanonik lineup sirasi) ayni kalmasi icin
    /// gereklidir.
    /// </summary>
    public static ImmutableArray<Player> ProjectLineup(
        TeamMatchState team,
        Guid incomingPlayerId,
        Guid outgoingPlayerId)
    {
        ArgumentNullException.ThrowIfNull(team);

        var builder = ImmutableArray.CreateBuilder<Player>(team.OnCourt.Length);

        foreach (var player in team.OnCourt)
        {
            builder.Add(player.Id == outgoingPlayerId ? PlayerById(team, incomingPlayerId) : player);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Sonuc lineup yasal mi: **dort degil, bes** ve hepsi yasal.
    ///
    /// <para><b>Kirpma veya sinirlama yok</b> (D53). M3'te yedekleme besi
    /// doldurmuyordu; ayni hatayi burada tekrarlamamak icin sonuc **dogrudan
    /// reddedilir**, sessizce kisaltilmaz. <c>CommandValidator</c> bu kontrolu
    /// yapar ve reddederse hicbir lineup yazilmaz.</para>
    /// </summary>
    public static bool IsLegalLineup(TeamMatchState team, ImmutableArray<Player> projected)
    {
        ArgumentNullException.ThrowIfNull(team);

        if (projected.Length != 5)
        {
            return false;
        }

        var seen = new HashSet<Guid>();

        foreach (var player in projected)
        {
            if (player is null)
            {
                return false;
            }

            if (!seen.Add(player.Id))
            {
                // Ayni oyuncu iki kez sahada olamaz (06 §7).
                return false;
            }

            if (IsFouledOut(team, player.Id))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Degisikligi **tek atomik gecis** olarak uygular (07 §3): "Substitution
    /// eski besten yeni beste tek atomik gecistir."
    ///
    /// <para>Ara durumda dort veya alti kişi olmaz; iki <c>OnCourt</c> dilimi
    /// tek <c>with</c> ifadesiyle degisir.</para>
    /// </summary>
    public static TeamMatchState Apply(TeamMatchState team, Guid incomingPlayerId, Guid outgoingPlayerId)
    {
        ArgumentNullException.ThrowIfNull(team);

        var projected = ProjectLineup(team, incomingPlayerId, outgoingPlayerId);

        if (!IsLegalLineup(team, projected))
        {
            // Uygulama yolunda bu durum OLMAMALIDIR: dogrulayici reddetmistir.
            // Yine de sessizce bozuk lineup yazmaktansa acik hata vermek daha
            // iyidir; 05 §3 "sessizce keyfi secme" yasagini bu yuzle.
            throw new InvalidOperationException(
                $"Substitution sonucu yasal degil ({team.Side}): "
                + $"{incomingPlayerId} girip {outgoingPlayerId} cikiyor.");
        }

        return team.WithOnCourt(projected);
    }

    private static Player PlayerById(TeamMatchState team, Guid playerId)
    {
        foreach (var player in team.Roster)
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        throw new InvalidOperationException($"Oyuncu kadroda bulunamadi: {playerId} ({team.Side}).");
    }
}
