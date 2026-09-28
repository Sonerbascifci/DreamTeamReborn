using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.Simulator.Fixture;

/// <summary>
/// M6: one team's data in a fixture file. The shape is the whole game content for
/// a match, so nothing here is derived — a file fully determines the setup.
/// </summary>
public sealed record FixtureTeamFile
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required OffensiveTactic Offensive { get; init; }

    public required DefensiveTactic Defense { get; init; }

    public required Pace Pace { get; init; }

    /// <summary>
    /// Exactly five ids, in lineup order. Ids rather than names: a name can be
    /// edited without changing who plays, an id cannot be silently repointed.
    /// </summary>
    public required ImmutableArray<Guid> LineupPlayerIds { get; init; }

    public required ImmutableArray<Player> Roster { get; init; }
}

/// <summary>
/// M6: a fixture on disk. Entirely fictional content; no real person, club, logo
/// or licensed data appears here or in the built-in catalog.
///
/// <para><b>Why files at all, given the catalog also exists.</b> The catalog is
/// the generator and the code source of truth, so a run never depends on the file
/// system being present. The files are the reviewable, diffable, shippable form of
/// the same content, and <c>EveryCatalogFixtureRoundTripsThroughItsFile</c> makes
/// drift between the two a failing test rather than a silent divergence.
/// </para>
/// </summary>
public sealed record FixtureFile
{
    public const int CurrentSchemaVersion = 1;

    public required int SchemaVersion { get; init; }

    public required string Name { get; init; }

    public required string Family { get; init; }

    public required string Description { get; init; }

    public required string RulesVersion { get; init; }

    public required string EngineVersion { get; init; }

    public required string RngAlgorithm { get; init; }

    public required string RngVersion { get; init; }

    public required FixtureTeamFile Home { get; init; }

    public required FixtureTeamFile Away { get; init; }

    public MatchSetup ToSetup(ulong seed) => new()
    {
        MatchId = DerivedMatchId(Name),
        Home = ToTeamSetup(Home),
        Away = ToTeamSetup(Away),
        Seed = seed,
        Engine = new EngineIdentity
        {
            EngineVersion = EngineVersion,
            RulesVersion = RulesVersion,
            BalanceConfigHash = FixtureCatalog.BalanceConfigPlaceholder,
            RngAlgorithm = RngAlgorithm,
            RngVersion = RngVersion,
        },
    };

    private static TeamMatchSetup ToTeamSetup(FixtureTeamFile file) => new()
    {
        Team = new Team { Id = file.Id, Name = file.Name, Roster = file.Roster },
        Lineup = new Lineup { PlayerIds = file.LineupPlayerIds },
        Offensive = file.Offensive,
        Defense = file.Defense,
        Pace = file.Pace,
    };

    public static FixtureFile FromSetup(
        string name,
        string family,
        string description,
        MatchSetup setup) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Name = name,
        Family = family,
        Description = description,
        RulesVersion = setup.Engine.RulesVersion,
        EngineVersion = setup.Engine.EngineVersion,
        RngAlgorithm = setup.Engine.RngAlgorithm,
        RngVersion = setup.Engine.RngVersion,
        Home = ToFile(setup.Home),
        Away = ToFile(setup.Away),
    };

    /// <summary>
    /// The match id a fixture file produces, derived from the fixture name so the
    /// same fixture always carries the same match identity and only the seed varies.
    /// </summary>
    public static Guid MatchIdFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        unchecked
        {
            var hash = 1469598103934665603UL;

            foreach (var character in name)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }

            // 8 (tur oneki) + 8 (hash) + 16 (sifir) = 32 ondalik karakter.
            return new Guid($"23a0b001{hash & 0xFFFFFFFFUL:X8}0000000000000000");
        }
    }

    private static FixtureTeamFile ToFile(TeamMatchSetup setup) => new()
    {
        Id = setup.Team.Id,
        Name = setup.Team.Name,
        Offensive = setup.Offensive,
        Defense = setup.Defense,
        Pace = setup.Pace,
        LineupPlayerIds = setup.Lineup.PlayerIds,
        Roster = setup.Team.Roster,
    };

    /// <summary>
    /// The match id a fixture file produces, derived from the fixture name so the
    /// same fixture always carries the same match identity; only the seed varies.
    ///
    /// <para><b>Neden 32 ondalik karakter?</b> Ilk deneme "23a0b" + 6 + 16 = 27
    /// karakter uretti ve <c>new Guid(string)</c> bunu FormatException ile
    /// reddetti. GUID metin bicimi tam olarak 32 ondalik karakter ister. Bu bir
    /// testin buldugu gercek hataydi; fixture dosyasi hic yuklenemiyordu.</para>
    /// </summary>
    public static Guid DerivedMatchId(string name)
    {
        unchecked
        {
            var hash = 1469598103934665603UL;

            foreach (var character in name)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }

            // 8 (tur oneki) + 8 (hash) + 16 (sifir) = 32 ondalik karakter.
            return new Guid($"23a0b001{hash & 0xFFFFFFFFUL:X8}0000000000000000");
        }
    }
}

/// <summary>
/// M6: loads fixtures from files. Selected when <c>--fixture</c> names a path
/// rather than a catalog name, so an out-of-tree fixture can be measured without
/// touching the repository.
///
/// <para><b>A malformed file is refused, never repaired.</b> Missing required
/// fields, an unknown enum name, or a lineup that is not a subset of the roster
/// all raise, with the offending field named. A silently defaulted rating would
/// be a measurement of a match nobody intended to measure.</para>
/// </summary>
public sealed class JsonFixtureSource : IFixtureSource
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    static JsonFixtureSource()
    {
        // Enum-as-name, same reason as the balance document (M5 D88): the default
        // is numeric, so reordering an enum would silently change a saved file.
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly IReadOnlyDictionary<string, FixtureFile> _byName;

    public JsonFixtureSource(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var byName = new Dictionary<string, FixtureFile>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            var file = Load(path);
            byName[file.Name] = file;
        }

        _byName = byName;
    }

    public IReadOnlyList<string> Names => [.. _byName.Keys.OrderBy(name => name, StringComparer.Ordinal)];

    public MatchSetup Build(string name, ulong seed)
    {
        if (!_byName.TryGetValue(name, out var file))
        {
            throw new KeyNotFoundException(
                $"Fixture dosyasinda '{name}' adi yok. Bilinen: {string.Join(", ", Names)}.");
        }

        Validate(file);
        return file.ToSetup(seed);
    }

    public static FixtureFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture dosyasi bulunamadi: {path}.", path);
        }

        var json = File.ReadAllText(path);

        try
        {
            return JsonSerializer.Deserialize<FixtureFile>(json, Options)
                ?? throw new InvalidOperationException($"{path}: fixture dosyasi bos.");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"{path}: fixture okunamadi ({error.Message}).", error);
        }
    }

    public static void Save(FixtureFile file, string path)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(file, Options) + "\n");
    }

    /// <summary>
    /// Refuses a file that the engine's own validator would refuse, with a message
    /// that names the field. Checking at load time means a bad fixture fails before
    /// 10,000 matches are spent on it.
    /// </summary>
    public static void Validate(FixtureFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.SchemaVersion != FixtureFile.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"{file.Name}: fixture semasi {file.SchemaVersion}, beklenen "
                + $"{FixtureFile.CurrentSchemaVersion}.");
        }

        if (file.EngineVersion != EngineVersion.Current)
        {
            throw new InvalidOperationException(
                $"{file.Name}: motor surumu '{file.EngineVersion}' != '{EngineVersion.Current}'.");
        }

        Require(file.Home.Roster.Length == FixtureCatalog.RosterSize,
            $"{file.Name}: ev kadrosu {file.Home.Roster.Length}, beklenen {FixtureCatalog.RosterSize}.");

        Require(file.Away.Roster.Length == FixtureCatalog.RosterSize,
            $"{file.Name}: dep kadrosu {file.Away.Roster.Length}, beklenen {FixtureCatalog.RosterSize}.");

        RequireLineup(file.Name, "Ev", file.Home);
        RequireLineup(file.Name, "Dep", file.Away);
    }

    private static void RequireLineup(string fixtureName, string side, FixtureTeamFile team)
    {
        Require(team.LineupPlayerIds.Length == FixtureCatalog.LineupSize,
            $"{fixtureName}: {side} lineup {team.LineupPlayerIds.Length}, beklenen {FixtureCatalog.LineupSize}.");

        var roster = team.Roster.Select(player => player.Id).ToHashSet();

        foreach (var id in team.LineupPlayerIds)
        {
            Require(roster.Contains(id),
                $"{fixtureName}: {side} lineup oyuncusu kadroda yok: {id}.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
