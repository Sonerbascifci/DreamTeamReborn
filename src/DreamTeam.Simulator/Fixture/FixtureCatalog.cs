using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.Simulator.Fixture;

/// <summary>
/// A single named match template. <see cref="Build"/> is deterministic: the same
/// <paramref name="seed"/> always produces the same <see cref="MatchSetup"/>.
/// </summary>
public sealed record FixtureDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>08 S5: which experiment family this fixture belongs to.</summary>
    public required string Family { get; init; }

    public required Func<ulong, MatchSetup> Build { get; init; }
}

/// <summary>
/// A source of <see cref="MatchSetup"/> values. Two implementations exist:
/// the built-in <see cref="FixtureCatalog"/> and <see cref="JsonFixtureSource"/>.
/// The batch driver only ever sees this interface.
/// </summary>
public interface IFixtureSource
{
    /// <summary>Known fixture names, sorted. Used for help text and for the manifest.</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Resolves a fixture by name. Throws <see cref="KeyNotFoundException"/> for an
    /// unknown name: a typo must fail loudly, never silently fall back to a default.
    /// </summary>
    MatchSetup Build(string name, ulong seed);
}

/// <summary>
/// M6: built-in, entirely fictional fixtures (no real person, club, logo or
/// licensed data).
///
/// <para><b>Why the experiment families exist.</b> 08 S5 lists the families that
/// the balance report needs. Each one isolates a single variable so that a change
/// in the output can be attributed instead of guessed at.</para>
///
/// <para><b>Why ratings are shaped, not uniform.</b> A roster where every player has
/// identical ratings makes depth unobservable: rotations and bench value cannot
/// show up. <c>NeutralMirror</c> therefore steps ratings down the bench. This is a
/// fixture design choice, not a measured balance result.</para>
///
/// <para><b>Identity, not realism.</b> Player and team names are synthetic and the
/// ids are derived from a type prefix plus an index, so the same slot always has
/// the same id across runs. That is what makes two separately built command lists
/// comparable (M5 D86).</para>
/// </summary>
public sealed class FixtureCatalog : IFixtureSource
{
    public const int RosterSize = 10;
    public const int LineupSize = 5;
    public const int TopRating = 74;
    public const int RatingStep = 3;

    /// <summary>
    /// Placeholder written into the fixture's <c>BalanceConfigHash</c>. The
    /// authoritative balance hash is the engine's <c>ConfigHash</c> from the
    /// loaded config document; this field keeps the identity record complete
    /// without pretending to be a measurement.
    /// </summary>
    public const string BalanceConfigPlaceholder = "motor-tarafindan-hesaplanir";

    public const string RulesVersion = "rules-v0.2-simple-nba";

    private readonly IReadOnlyDictionary<string, FixtureDefinition> _byName;

    public FixtureCatalog()
    {
        var definitions = new List<FixtureDefinition>
        {
            new()
            {
                Name = "neutral-mirror",
                Family = "mirror",
                Description = "Iki takim birebir ayni rating/taktik/tempo. Ev/deplasman farki yalniz baslangic protokolunden gelir.",
                Build = NeutralMirror,
            },
            new()
            {
                Name = "neutral-mirror-swapped",
                Family = "mirror",
                Description = "neutral-mirror ile ayni kadrolar, yerleri degisik. 08 S5 home/away yer degistirme esigi bunu ister.",
                Build = NeutralMirrorSwapped,
            },
            new()
            {
                Name = "quality-gap",
                Family = "quality",
                Description = "Ev kadrosu +8 rating tasiyor, baska her sey ayni. Guclu takim avantajli olmali.",
                Build = seed => QualityGap(seed, 8),
            },
            new()
            {
                Name = "roster-fit",
                Family = "roster",
                Description = "Ev kadrosu disaridan iyi, depasman ici iyi. Taktik etkisi kadroya bagli olmali.",
                Build = RosterFit,
            },
            new()
            {
                Name = "pace-slow",
                Family = "pace",
                Description = "Her iki takim Slow tempo. Yalnizca tempo degisir.",
                Build = seed => WithPace(seed, Pace.Slow),
            },
            new()
            {
                Name = "pace-normal",
                Family = "pace",
                Description = "Her iki takim Normal tempo. Yalnizca tempo degisir.",
                Build = seed => WithPace(seed, Pace.Normal),
            },
            new()
            {
                Name = "pace-fast",
                Family = "pace",
                Description = "Her iki takim Fast tempo. Yalnizca tempo degisir.",
                Build = seed => WithPace(seed, Pace.Fast),
            },
        };

        _byName = definitions.ToDictionary(definition => definition.Name, StringComparer.Ordinal);
        Definitions = definitions;
    }

    /// <summary>All built-in definitions, in declaration order.</summary>
    public IReadOnlyList<FixtureDefinition> Definitions { get; }

    /// <summary>All definitions of one experiment family, declaration order.</summary>
    public IReadOnlyList<FixtureDefinition> Family(string family) =>
        [.. Definitions.Where(definition => string.Equals(definition.Family, family, StringComparison.Ordinal))];

    public IReadOnlyList<string> Names => [.. _byName.Keys.OrderBy(name => name, StringComparer.Ordinal)];

    public MatchSetup Build(string name, ulong seed)
    {
        if (!_byName.TryGetValue(name, out var definition))
        {
            throw new KeyNotFoundException(
                $"Bilinmeyen fixture adi: '{name}'. Bilinen adlar: {string.Join(", ", Names)}.");
        }

        return definition.Build(seed);
    }

    /// <summary>
    /// M6: the on-disk form of one built-in fixture. <c>export-fixtures</c> writes
    /// these, and <c>EveryCatalogFixtureRoundTripsThroughItsFile</c> checks that
    /// writing and re-reading produces the same setup — so a checked-in file can
    /// never drift away from the code that generated it.
    ///
    /// <para>The match id is overwritten on load with one derived from the name, so
    /// the catalog's own match index and the file's derived id are allowed to
    /// differ. Everything else must be identical.</para>
    /// </summary>
    public FixtureFile ToFile(string name)
    {
        if (!_byName.TryGetValue(name, out var definition))
        {
            throw new KeyNotFoundException(
                $"Bilinmeyen fixture adi: '{name}'. Bilinen adlar: {string.Join(", ", Names)}.");
        }

        return FixtureFile.FromSetup(
            name, definition.Family, definition.Description, definition.Build(0));
    }

    // ------------------------------------------------------------------ builders

    public static MatchSetup NeutralMirror(ulong seed) => Mirror(seed, swapped: false);

    public static MatchSetup NeutralMirrorSwapped(ulong seed) => Mirror(seed, swapped: true);

    /// <summary>Home is stronger by <paramref name="strongBonus"/> rating points.</summary>
    public static MatchSetup QualityGap(ulong seed, int strongBonus)
    {
        var home = TeamMatchSetup.Default(
            Team(1, "Guclu Yildizlar", Scaled(1, strongBonus, RoleShape.Balanced)),
            LineupFor(1));

        var away = TeamMatchSetup.Default(
            Team(2, "Zayif Yildizlar", Scaled(2, 0, RoleShape.Balanced)),
            LineupFor(2));

        return Compose(2, seed, home.WithTactics(OffensiveTactic.Balanced, DefensiveTactic.ManToMan, Pace.Normal), away);
    }

    /// <summary>
    /// Home shoots, Away posts. Same overall rating budget, different shape, so a
    /// tactic effect has to be roster dependent to show up at all.
    /// </summary>
    public static MatchSetup RosterFit(ulong seed)
    {
        var home = TeamMatchSetup.Default(
            Team(1, "Disarci Yildizlar", Scaled(1, 0, RoleShape.Perimeter)),
            LineupFor(1));

        var away = TeamMatchSetup.Default(
            Team(2, "Icci Yildizlar", Scaled(2, 0, RoleShape.Inside)),
            LineupFor(2));

        return Compose(3, seed, home.WithTactics(OffensiveTactic.Balanced, DefensiveTactic.ManToMan, Pace.Normal), away);
    }

    public static MatchSetup WithPace(ulong seed, Pace pace)
    {
        var baseSetup = Mirror(seed, swapped: false);

        return baseSetup with
        {
            Home = baseSetup.Home.WithTactics(baseSetup.Home.Offensive, baseSetup.Home.Defense, pace),
            Away = baseSetup.Away.WithTactics(baseSetup.Away.Offensive, baseSetup.Away.Defense, pace),
        };
    }

    /// <summary>
    /// Overrides the tactics of both sides. Used by the defense matrix experiment:
    /// four offense tactics x four defense policies is 16 pairs, and the CLI
    /// selects a pair rather than the catalog defining 16 fixtures.
    /// </summary>
    public static MatchSetup WithTactics(
        ulong seed,
        OffensiveTactic homeOffense,
        DefensiveTactic homeDefense,
        Pace homePace,
        OffensiveTactic awayOffense,
        DefensiveTactic awayDefense,
        Pace awayPace)
    {
        var baseSetup = Mirror(seed, swapped: false);

        return baseSetup with
        {
            Home = baseSetup.Home.WithTactics(homeOffense, homeDefense, homePace),
            Away = baseSetup.Away.WithTactics(awayOffense, awayDefense, awayPace),
        };
    }

    private static MatchSetup Mirror(ulong seed, bool swapped)
    {
        var first = TeamMatchSetup.Default(
            Team(1, "Kuzey Yildizlari", Scaled(1, 0, RoleShape.Balanced)), LineupFor(1));

        var second = TeamMatchSetup.Default(
            Team(2, "Guney Yildizlari", Scaled(2, 0, RoleShape.Balanced)), LineupFor(2));

        var home = first.WithTactics(OffensiveTactic.Balanced, DefensiveTactic.ManToMan, Pace.Normal);
        var away = second.WithTactics(OffensiveTactic.Balanced, DefensiveTactic.ManToMan, Pace.Normal);

        return Compose(1, seed, swapped ? away : home, swapped ? home : away);
    }

    private static MatchSetup Compose(
        int matchIndex,
        ulong seed,
        TeamMatchSetup home,
        TeamMatchSetup away) => new()
        {
            MatchId = Id("23a0b001", matchIndex),
            Home = home,
            Away = away,
            Seed = seed,
            Engine = new EngineIdentity
            {
                EngineVersion = EngineVersion.Current,
                RulesVersion = RulesVersion,
                BalanceConfigHash = BalanceConfigPlaceholder,
                RngAlgorithm = RngIdentity.Algorithm,
                RngVersion = RngIdentity.Version,
            },
        };

    // ------------------------------------------------------------------- rosters

    private enum RoleShape
    {
        Balanced,
        Perimeter,
        Inside,
    }

    private static Team Team(int index, string name, ImmutableArray<PlayerRatings> ratings) => new()
    {
        Id = Id("21a0b001", index),
        Name = name,
        Roster = [.. ratings.Select((values, slot) => Player(index, slot, values))],
    };

    /// <summary>
    /// Ratings step down the bench so depth is observable, then the shape is
    /// applied. The shape moves the budget between perimeter and inside ratings;
    /// it does not inflate the total, so "quality gap" stays the only thing that
    /// changes overall strength.
    /// </summary>
    private static ImmutableArray<PlayerRatings> Scaled(int teamIndex, int bonus, RoleShape shape)
    {
        var builder = ImmutableArray.CreateBuilder<PlayerRatings>(RosterSize);

        for (var slot = 0; slot < RosterSize; slot++)
        {
            builder.Add(Ratings(TopRating - (slot * RatingStep) + bonus, shape));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The M2-M5 fixture shape, preserved exactly. <see cref="RoleShape.Balanced"/>
    /// is NOT a flat profile: each attribute carries its own fixed offset from the
    /// slot rating, which is what M2 introduced and what every golden result from
    /// M2 to M5 was produced with.
    ///
    /// <para><b>This is not cosmetic.</b> Replacing it with a flat profile changed
    /// the default match from 112-118 over 5 periods to 102-110 over 4. That is a
    /// silent break of the D33 argument-less regression, found by diffing the
    /// report against the M5 output rather than by reasoning about it. Kept
    /// verbatim so M6's numbers stay comparable with everything measured before.</para>
    /// </summary>
    private static PlayerRatings Ratings(int value, RoleShape shape)
    {
        var baseRatings = new PlayerRatings
        {
            Speed = value,
            Strength = value,
            Vertical = value + 2,
            Stamina = value + 4,
            Inside = value,
            MidRange = value,
            ThreePoint = value - 2,
            FreeThrow = value + 1,
            BallHandling = value,
            Passing = value,
            OffBall = value - 1,
            PostOffense = value - 3,
            PerimeterDefense = value,
            InteriorDefense = value + 1,
            Steal = value - 4,
            Block = value - 5,
            Rebounding = value,
            BasketballIQ = value + 3,
        };

        return shape switch
        {
            // Roster-fit only. Same rating budget, different shape, so a tactic
            // effect has to be roster dependent to show up at all.
            RoleShape.Perimeter => baseRatings with
            {
                ThreePoint = value + 12,
                BallHandling = value + 8,
                OffBall = value + 6,
                Speed = value + 5,
                Inside = value - 8,
                PostOffense = value - 8,
                Rebounding = value - 5,
            },
            RoleShape.Inside => baseRatings with
            {
                Inside = value + 12,
                PostOffense = value + 8,
                Rebounding = value + 8,
                Strength = value + 6,
                InteriorDefense = value + 5,
                ThreePoint = value - 8,
                BallHandling = value - 8,
                PerimeterDefense = value - 5,
            },
            _ => baseRatings,
        };
    }

    private static Player Player(int teamIndex, int slot, PlayerRatings ratings) => new()
    {
        Id = Id("22a0b001", (teamIndex * 100) + slot),
        DisplayName = $"Oyuncu {teamIndex}-{slot}",
        Position = (Position)(slot % LineupSize),
        Ratings = ratings,
    };

    private static Lineup LineupFor(int teamIndex)
    {
        var ids = new List<Guid>(LineupSize);

        for (var slot = 0; slot < LineupSize; slot++)
        {
            ids.Add(Id("22a0b001", (teamIndex * 100) + slot));
        }

        return new Lineup { PlayerIds = [.. ids] };
    }

    /// <summary>32 hex characters: 8-char type prefix + 8-char index + 16 zeros.</summary>
    private static Guid Id(string typePrefix, int index) =>
        new($"{typePrefix}{index:X8}0000000000000000");
}
