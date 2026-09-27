using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;
using DreamTeam.Simulator;

// D33: this console entry point takes no arguments. It plays one fixed fictional
// fixture. CLI flags, batch mode, JSON/CSV export and fixture files belong to M6.
//
// The fixture is "NeutralMirror": both teams have identical ratings but distinct
// player identities, so any side difference can only come from the opening
// protocol. The 08 symmetry experiment in M6 will reuse this shape.

var setup = Fixture.NeutralMirror(seed: 20260927);

var config = EngineConfig.Baseline;
var simulation = new MatchSimulation(config);
var result = simulation.Simulate(setup);

SingleMatchReport.Write(Console.Out, result, setup.Engine, simulation.ConfigHash);

return result.Status == MatchStatus.Completed ? 0 : 1;

internal static class Fixture
{
    private const int RosterSize = 10;
    private const int LineupSize = 5;

    /// <summary>
    /// Placeholder string for the balance config hash. The authoritative hash is
    /// computed by the engine from the config content and printed in the report, so
    /// the two can be compared. Risk "setup digest has no producer" is closed by
    /// <c>EngineConfig.ComputeConfigHash</c>.
    /// </summary>
    private const string BalanceConfigPlaceholder = "motor-tarafindan-hesaplanir";

    /// <summary>
    /// Two teams with the same ratings and roles but different identities.
    /// Entirely fictional: no real person, club, logo or licensed data.
    /// </summary>
    public static MatchSetup NeutralMirror(ulong seed)
    {
        return new MatchSetup
        {
            MatchId = MatchId(1),
            Home = BuildTeam("Kuzey Yildizlari", teamIndex: 1),
            Away = BuildTeam("Guney Yildizlari", teamIndex: 2),
            HomeLineup = BuildLineup(teamIndex: 1),
            AwayLineup = BuildLineup(teamIndex: 2),
            Seed = seed,
            Engine = new EngineIdentity
            {
                EngineVersion = EngineVersion.Current,
                RulesVersion = "rules-v0.2-simple-nba",
                BalanceConfigHash = BalanceConfigPlaceholder,
                RngAlgorithm = RngIdentity.Algorithm,
                RngVersion = RngIdentity.Version,
            },
        };
    }

    private static Team BuildTeam(string name, int teamIndex)
    {
        return new Team
        {
            Id = TeamId(teamIndex),
            Name = name,
            Roster = BuildRoster(teamIndex),
        };
    }

    private static ImmutableArray<Player> BuildRoster(int teamIndex)
    {
        var players = new List<Player>();

        for (var slot = 0; slot < RosterSize; slot++)
        {
            players.Add(BuildPlayer(teamIndex, slot));
        }

        return [.. players];
    }

    private static Lineup BuildLineup(int teamIndex)
    {
        var ids = new List<Guid>();

        for (var slot = 0; slot < LineupSize; slot++)
        {
            ids.Add(PlayerId(teamIndex, slot));
        }

        return new Lineup { PlayerIds = [.. ids] };
    }

    /// <summary>
    /// Ratings are spread across the roster so depth can be observed: the fifth
    /// player is stronger, the tenth weaker. This is a test fixture, not a measured
    /// balance result.
    /// </summary>
    private static Player BuildPlayer(int teamIndex, int slot)
    {
        var rating = 74 - (slot * 3);

        return new Player
        {
            Id = PlayerId(teamIndex, slot),
            DisplayName = "Oyuncu " + teamIndex + "-" + slot,
            Position = (Position)(slot % 5),
            Ratings = new PlayerRatings
            {
                Speed = rating,
                Strength = rating,
                Vertical = rating + 2,
                Stamina = rating + 4,
                Inside = rating,
                MidRange = rating,
                ThreePoint = rating - 2,
                FreeThrow = rating + 1,
                BallHandling = rating,
                Passing = rating,
                OffBall = rating - 1,
                PostOffense = rating - 3,
                PerimeterDefense = rating,
                InteriorDefense = rating + 1,
                Steal = rating - 4,
                Block = rating - 5,
                Rebounding = rating,
                BasketballIQ = rating + 3,
            },
        };
    }

    /// <summary>32 hex characters: 8-char type prefix + 8-char index + 16 zeros.</summary>
    private static Guid Id(string typePrefix, int index)
    {
        return new Guid($"{typePrefix}{index:X8}0000000000000000");
    }

    private static Guid TeamId(int index) => Id("21a0b001", index);

    private static Guid PlayerId(int teamIndex, int slot) => Id("22a0b001", (teamIndex * 100) + slot);

    private static Guid MatchId(int index) => Id("23a0b001", index);
}
