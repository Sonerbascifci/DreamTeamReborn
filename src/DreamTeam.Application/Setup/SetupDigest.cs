using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DreamTeam.Application.Ports;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.Domain.Teams;

namespace DreamTeam.Application.Setup;

/// <summary>
/// M7, D87: <c>MatchSetup</c> icin kalici, kanonik digest.
///
/// <para><b>Neden var?</b> D87 olcumdu: <c>ImmutableArray&lt;T&gt;.Equals</c>
/// REFERANS esitligi oldugu icin <c>record</c> ureticisi <c>Team</c>,
/// <c>TeamMatchSetup</c>, <c>MatchSetup</c> ve <c>MatchState</c> icin bozuktur.
/// M5'te <c>MatchStateFingerprint</c> ile aşıldi ama o yalniz test/replay
/// yuzeyidir. 03 §"Surum ve tekr uretilebilirlik" bir <b>setup digest</b> ister;
/// iste budur.</para>
///
/// <para><b>Bu <c>record.Equals</c> DEGILDIR.</b> Uc ozelligi:</para>
/// <list type="number">
///   <item><description><b>Sıra bağımsız.</b> Kadro <c>RosterOrdering.Canonical</c>
///   ile sıralanır; aynı kadro farklı sırada verilse digest aynıdır.</description></item>
///   <item><description><b>Alan sırası açık ve sabit.</b> 18 rating alanı isimleriyle
///   ve yazılı sırayla yazılır. Alan eklenip çıkarılırsa digest değişir —
///   bu istenen davranıştır (config sürümü değişmiştir).</description></item>
///   <item><description><b>Kültürden bağımsız.</b> Sayılar
///   <c>InvariantCulture</c> ile yazılır; Türkçe/İngilizce locale farkı digest'i
///   değiştirmez.</description></item>
/// </list>
///
/// <para><b>BILINCI OLARAK HARIC TUTULAN ALANLAR.</b> Digest, motorun girdisi
/// olan her seyi kapsar ve <i>yalnizca</i> onu. Iki alan kasten yok:</para>
/// <list type="number">
///   <item><description><b><c>DisplayName</c></b>. Oyuncu adinin domain
///   sonucu UZERINDE ETKISI YOKTUR. Ad degistirmek bir macin kimligini
///   degistirmemelidir; aksi halde kozmetik bir duzenleme yuzunden kayit
///   gecersizlesir. Test bunu olcer: <c>ARenameDoesNotChangeTheDigest</c>.</description></item>
///   <item><description><b>Roster <i>uzunlugu</i> ayri bir alan olarak.</b>
///   Oyuncular zaten kimlikleriyle tek tek yazildigi icin ayrica sayi
///   yazmak ayni bilgiyi iki kez yazmaktir ve iki yerin ayri ayri
///   degismesi riskini tasir.</description></item>
/// </list>
///
/// <para><b>Bilinçli kapsam dışı:</b> <c>MatchState</c> digest'i M5'te zaten
/// var (<c>MatchStateFingerprint</c>); burada tekrarlanmaz.</para>
/// </summary>
public sealed class SetupDigest : ISetupDigest
{
    public string Of(MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var builder = new StringBuilder(4096);

        AppendSetup(builder, "home", setup.Home);
        AppendSetup(builder, "away", setup.Away);

        Append(builder, "matchId", setup.MatchId.ToString("D", CultureInfo.InvariantCulture));
        Append(builder, "seed", setup.Seed.ToString(CultureInfo.InvariantCulture));
        Append(builder, "engine", setup.Engine.EngineVersion);
        Append(builder, "rules", setup.Engine.RulesVersion);
        Append(builder, "rng", setup.Engine.RngAlgorithm + "/" + setup.Engine.RngVersion);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendSetup(StringBuilder builder, string label, TeamMatchSetup team)
    {
        Append(builder, $"{label}.teamId", team.Team.Id.ToString("D", CultureInfo.InvariantCulture));
        Append(builder, $"{label}.name", team.Team.Name);
        Append(builder, $"{label}.offense", team.Offensive.ToString());
        Append(builder, $"{label}.defense", team.Defense.ToString());
        Append(builder, $"{label}.pace", team.Pace.ToString());

        foreach (var id in team.Lineup.PlayerIds)
        {
            Append(builder, $"{label}.lineup", id.ToString("D", CultureInfo.InvariantCulture));
        }

        // Kanonik sira: roster girdi sirasi degistirilse bile digest ayni kalir.
        foreach (var player in RosterOrdering.Canonical(team.Team.Roster))
        {
            Append(builder, $"{label}.player", player.Id.ToString("D", CultureInfo.InvariantCulture));
            Append(builder, $"{label}.position", player.Position.ToString());
            AppendRatings(builder, $"{label}", player.Ratings);
        }
    }

    /// <summary>
    /// 18 alan, acik ve sabit sirada. Siralama burada tek kaynaktir; alan
    /// eklendiginde digest degisir, bu da config surumunun degistiginin
    /// sinyalidir.
    /// </summary>
    private static void AppendRatings(StringBuilder builder, string label, PlayerRatings ratings)
    {
        Append(builder, $"{label}.r.speed", ratings.Speed);
        Append(builder, $"{label}.r.strength", ratings.Strength);
        Append(builder, $"{label}.r.vertical", ratings.Vertical);
        Append(builder, $"{label}.r.stamina", ratings.Stamina);
        Append(builder, $"{label}.r.inside", ratings.Inside);
        Append(builder, $"{label}.r.midRange", ratings.MidRange);
        Append(builder, $"{label}.r.threePoint", ratings.ThreePoint);
        Append(builder, $"{label}.r.freeThrow", ratings.FreeThrow);
        Append(builder, $"{label}.r.ballHandling", ratings.BallHandling);
        Append(builder, $"{label}.r.passing", ratings.Passing);
        Append(builder, $"{label}.r.offBall", ratings.OffBall);
        Append(builder, $"{label}.r.postOffense", ratings.PostOffense);
        Append(builder, $"{label}.r.perimeterDefense", ratings.PerimeterDefense);
        Append(builder, $"{label}.r.interiorDefense", ratings.InteriorDefense);
        Append(builder, $"{label}.r.steal", ratings.Steal);
        Append(builder, $"{label}.r.block", ratings.Block);
        Append(builder, $"{label}.r.rebounding", ratings.Rebounding);
        Append(builder, $"{label}.r.basketballIq", ratings.BasketballIQ);
    }

    private static void Append(StringBuilder builder, string key, string value) =>
        builder.Append(key).Append('=').Append(value).Append('\n');

    private static void Append(StringBuilder builder, string key, int value) =>
        Append(builder, key, value.ToString(CultureInfo.InvariantCulture));
}
