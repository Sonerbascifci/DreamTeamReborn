using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// Testler için kurgusal fixture üreticileri. Buradaki oyuncu ve takım adları
/// tamamen kurgusaldır; gerçek kişi veya lisanslı veri kullanılmaz.
/// Rating değerleri test içindir, denge ölçümü değildir.
/// </summary>
internal static class TestData
{
    public const int DefaultRosterSize = 10;

    /// <summary>Tüm attribute'leri aynı değere ayarlar.</summary>
    public static PlayerRatings Ratings(int value = 50) => new()
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

    /// <summary>Belirtilen attribute'ü değiştirilmiş rating üretir.</summary>
    public static PlayerRatings RatingsWith(Func<PlayerRatings, PlayerRatings> change) =>
        change(Ratings());

    public static Player Player(int index, int rating = 50) => new()
    {
        Id = Ids.Player(index),
        DisplayName = $"Kurgusal Oyuncu {index}",
        Position = (Position)(index % 5),
        Ratings = Ratings(rating),
    };

    public static ImmutableArray<Player> Roster(int size = DefaultRosterSize) =>
        [.. Enumerable.Range(0, size).Select(index => Player(index))];

    public static Team Team(int id, int rosterSize = DefaultRosterSize, int rating = 50) => new()
    {
        Id = Ids.Team(id),
        Name = $"Kurgusal Takim {id}",
        Roster = [.. Enumerable.Range(0, rosterSize).Select(index => Player(index, rating))],
    };

    public static Lineup Lineup(params int[] playerIndexes)
    {
        if (playerIndexes.Length == 0)
        {
            playerIndexes = [0, 1, 2, 3, 4];
        }

        return new Lineup { PlayerIds = [.. playerIndexes.Select(Ids.Player)] };
    }

    public static EngineIdentity Identity(
        string engineVersion = EngineVersion.Current,
        string rulesVersion = "rules-v0.1",
        string balanceConfigHash = "balance-v0.1-hash-placeholder",
        string rngAlgorithm = RngIdentity.Algorithm,
        string rngVersion = RngIdentity.Version) => new()
    {
        EngineVersion = engineVersion,
        RulesVersion = rulesVersion,
        BalanceConfigHash = balanceConfigHash,
        RngAlgorithm = rngAlgorithm,
        RngVersion = rngVersion,
    };

    /// <summary>
    /// M4'te <c>MatchSetup.Home</c>/<c>Away</c> bir <see cref="TeamMatchSetup"/>'tir
    /// (D61); bu yardımcı lineup parametrelerini korur ve doğrulama testlerinin
    /// mekanik çağrılarını değiştirmeden uyum sağlar.
    /// </summary>
    public static MatchSetup Setup(
        Team? home = null,
        Team? away = null,
        Lineup? homeLineup = null,
        Lineup? awayLineup = null,
        EngineIdentity? engine = null,
        Guid? matchId = null,
        ulong seed = 12345) => new()
    {
        MatchId = matchId ?? Ids.Match(1),
        Home = TeamMatchSetup.Default(home ?? Team(1), homeLineup ?? Lineup()),
        Away = TeamMatchSetup.Default(away ?? Team(2), awayLineup ?? Lineup()),
        Seed = seed,
        Engine = engine ?? Identity(),
    };
}

/// <summary>
/// Testlerde tekrarlanabilir ve türler arası çakışmayan kimlikler üretir.
/// Üretim kodunda kullanılmaz. Biçim: 8 hex önek + 8 hex indeks + 16 hex sıfır = 32 hex.
/// </summary>
internal static class Ids
{
    public static Guid Derive(string prefix, int index) => new($"{prefix}{index:X8}0000000000000000");

    public static Guid Player(int index) => Derive("01a0b001", index);

    public static Guid Team(int index) => Derive("02a0b002", index);

    public static Guid Match(int index) => Derive("03a0b003", index);
}
