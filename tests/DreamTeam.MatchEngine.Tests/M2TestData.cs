using System.Collections.Immutable;
using System.Text;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M2 testleri icin kurgusal fixture'lar. Tum oyuncu ve takim adlari tamamen
/// kurgusaldir; gercek kisi, kulup veya lisansli veri yoktur.
///
/// Rating'ler test icindir, olculmus denge degildir. <c>NeutralMirror</c> iki takimi
/// birebir ayni ratinglerle kurar; boylece taraf farki yalnizca baslangic
/// protokolunden gelir. 08'in simetri deneyi M6'da bu fixture'i kullanacaktir.
/// </summary>
internal static class M2TestData
{
    public const int RosterSize = 10;
    public const int LineupSize = 5;
    public const int TopRating = 74;
    public const int RatingStep = 3;

    public static EngineConfig Config(
        RulesProfile? rules = null,
        ShotModel? shot = null,
        ActionModel? actions = null,
        FoulModel? fouls = null,
        FreeThrowModel? freeThrows = null,
        ImmutableArray<ActionProfile>? actionProfiles = null,
        int? maxActionsPerMatch = null)
    {
        return new EngineConfig
        {
            Rules = rules ?? RulesProfile.SimpleNbaInspired,
            Shot = shot ?? ShotModel.Baseline,
            Actions = actions ?? ActionModel.Baseline,
            Fouls = fouls ?? FoulModel.Baseline,
            FreeThrows = freeThrows ?? FreeThrowModel.Baseline,
            ActionProfiles = actionProfiles ?? ActionProfile.Baseline,
            MaxActionsPerMatch = maxActionsPerMatch ?? EngineConfig.Baseline.MaxActionsPerMatch,
        };
    }

    public static EngineIdentity Identity()
    {
        return new EngineIdentity
        {
            EngineVersion = EngineVersion.Current,
            RulesVersion = "rules-v0.2-simple-nba",
            BalanceConfigHash = "balance-baseline-placeholder",
            RngAlgorithm = RngIdentity.Algorithm,
            RngVersion = RngIdentity.Version,
        };
    }

    public static MatchSetup NeutralMirror(ulong seed = 12_345)
    {
        return new MatchSetup
        {
            MatchId = MatchId(1),
            Home = MirrorTeam(teamIndex: 1, "Kuzey"),
            Away = MirrorTeam(teamIndex: 2, "Guney"),
            HomeLineup = MirrorLineup(teamIndex: 1),
            AwayLineup = MirrorLineup(teamIndex: 2),
            Seed = seed,
            Engine = Identity(),
        };
    }

    /// <summary>Rating'leri verilen bonus ile ayrilmis iki takim. Yon kontrolu icin.</summary>
    public static MatchSetup QualityGap(ulong seed, int strongBonus)
    {
        return new MatchSetup
        {
            MatchId = MatchId(2),
            Home = ScaledTeam(teamIndex: 1, "Guclu", strongBonus),
            Away = ScaledTeam(teamIndex: 2, "Zayif", 0),
            HomeLineup = MirrorLineup(teamIndex: 1),
            AwayLineup = MirrorLineup(teamIndex: 2),
            Seed = seed,
            Engine = Identity(),
        };
    }

    /// <summary>Ayni kadro, istege bagli olarak ters sirada. T02 icin.</summary>
    public static MatchSetup WithRosterOrder(ulong seed, bool reverse)
    {
        return new MatchSetup
        {
            MatchId = MatchId(3),
            Home = OrderedTeam(teamIndex: 1, "Kuzey", reverse),
            Away = OrderedTeam(teamIndex: 2, "Guney", reverse),
            HomeLineup = MirrorLineup(teamIndex: 1),
            AwayLineup = MirrorLineup(teamIndex: 2),
            Seed = seed,
            Engine = Identity(),
        };
    }

    public static ImmutableArray<Guid> OnCourtIds(MatchSetup setup)
    {
        return [.. setup.HomeLineup.PlayerIds, .. setup.AwayLineup.PlayerIds];
    }

    /// <summary>
    /// Event akisinin kararli metin parmak izi. Testlerde "ayni akis"i bayt bayt
    /// karsilastirmak icin kullanilir; 08 §4 uyarinca JSON canonicalization veya
    /// alan sirasi varsayilmaz - buradaki sira acikca sabittir.
    /// </summary>
    public static string Fingerprint(IReadOnlyList<MatchEvent> events)
    {
        var builder = new StringBuilder();

        foreach (var matchEvent in events)
        {
            builder.Append(matchEvent.Sequence).Append('|')
                .Append(matchEvent.Type).Append('|')
                .Append(matchEvent.Period).Append('|')
                .Append(matchEvent.GameClockMs).Append('|')
                .Append(matchEvent.ElapsedGameTimeMs).Append('|')
                .Append(matchEvent.PossessionId).Append('|')
                .Append(matchEvent.ActionId).Append('|')
                .Append(matchEvent.TeamId).Append('|')
                .Append(matchEvent.PlayerId).Append('|')
                .Append(matchEvent.SecondaryPlayerId).Append('|')
                .Append(matchEvent.Payload).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>18 attribute'un tamami verilen degerе ayarlanir; test sadeligi icin.</summary>
    public static PlayerRatings Ratings(int value)
    {
        return new PlayerRatings
        {
            Speed = value,
            Strength = value,
            Vertical = value,
            Stamina = value,
            Inside = value,
            MidRange = value,
            ThreePoint = value,
            FreeThrow = value,
            BallHandling = value,
            Passing = value,
            OffBall = value,
            PostOffense = value,
            PerimeterDefense = value,
            InteriorDefense = value,
            Steal = value,
            Block = value,
            Rebounding = value,
            BasketballIQ = value,
        };
    }

    private static Team MirrorTeam(int teamIndex, string prefix)
    {
        return new Team
        {
            Id = TeamId(teamIndex),
            Name = prefix + " Yildizlari",
            Roster = BuildRoster(teamIndex, 0),
        };
    }

    private static Team OrderedTeam(int teamIndex, string prefix, bool reverse)
    {
        var roster = BuildRoster(teamIndex, 0);

        return new Team
        {
            Id = TeamId(teamIndex),
            Name = prefix + " Yildizlari",
            Roster = reverse ? [.. roster.Reverse()] : roster,
        };
    }

    private static Team ScaledTeam(int teamIndex, string name, int bonus)
    {
        return new Team
        {
            Id = TeamId(teamIndex),
            Name = name,
            Roster = BuildRoster(teamIndex, bonus),
        };
    }

    private static ImmutableArray<Player> BuildRoster(int teamIndex, int bonus)
    {
        var players = new List<Player>();

        for (var slot = 0; slot < RosterSize; slot++)
        {
            players.Add(BuildPlayer(teamIndex, slot, bonus));
        }

        return [.. players];
    }

    private static Lineup MirrorLineup(int teamIndex)
    {
        var ids = new List<Guid>();

        for (var slot = 0; slot < LineupSize; slot++)
        {
            ids.Add(PlayerId(teamIndex, slot));
        }

        return new Lineup { PlayerIds = [.. ids] };
    }

    private static Player BuildPlayer(int teamIndex, int slot, int bonus)
    {
        var rating = TopRating - (slot * RatingStep) + bonus;

        return new Player
        {
            Id = PlayerId(teamIndex, slot),
            DisplayName = "Oyuncu " + teamIndex + "-" + slot,
            Position = (Position)(slot % 5),
            Ratings = Ratings(rating),
        };
    }

    /// <summary>32 hex karakter: 8'lik tur oneki + 8'lik indeks + 16 sifir.</summary>
    private static Guid Id(string typePrefix, int index)
    {
        return new Guid($"{typePrefix}{index:X8}0000000000000000");
    }

    private static Guid TeamId(int index) => Id("11a0b001", index);

    private static Guid PlayerId(int teamIndex, int slot) => Id("12a0b001", (teamIndex * 100) + slot);

    private static Guid MatchId(int index) => Id("13a0b001", index);
}
