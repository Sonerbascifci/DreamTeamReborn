using System.Collections.Immutable;
using System.Reflection;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Tests;

public class SetupValidationTests
{
    [Fact]
    public void ValidSetupWithFiveDistinctPlayersIsAccepted()
    {
        var result = MatchSetupValidator.Validate(TestData.Setup());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void LineupMayTakeAnyFiveDistinctRosterPlayers()
    {
        // Beş oyunculu kadro değil, dokuz oyunculu kadro da geçerli olmalıdır:
        // lineup kadronun alt kümesidir, kadronun kendisi değil.
        var setup = TestData.Setup(
            home: TestData.Team(1, rosterSize: 9),
            away: TestData.Team(2, rosterSize: 9),
            homeLineup: TestData.Lineup(0, 2, 4, 6, 8),
            awayLineup: TestData.Lineup(5, 3, 1, 7, 0));

        var result = MatchSetupValidator.Validate(setup);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [Fact]
    public void SamePlayerInTwoSlotsIsRejected()
    {
        var setup = TestData.Setup(homeLineup: TestData.Lineup(0, 1, 2, 3, 3));

        var result = MatchSetupValidator.Validate(setup);

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.DuplicateLineupPlayerId));
    }

    [Fact]
    public void LineupPlayerOutsideRosterIsRejected()
    {
        // 999 numaralı oyuncu hiçbir kadroda bulunmuyor.
        var setup = TestData.Setup(homeLineup: TestData.Lineup(0, 1, 2, 3, 999));

        var result = MatchSetupValidator.Validate(setup);

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.LineupPlayerNotInRoster));
        Assert.False(result.Contains(MatchSetupErrorCode.DuplicateLineupPlayerId));
    }

    [Fact]
    public void FewerThanFivePlayersIsRejected()
    {
        var setup = TestData.Setup(homeLineup: TestData.Lineup(0, 1, 2, 3));

        var result = MatchSetupValidator.Validate(setup);

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.LineupSizeInvalid));
    }

    [Fact]
    public void MoreThanFivePlayersIsRejected()
    {
        var setup = TestData.Setup(homeLineup: TestData.Lineup(0, 1, 2, 3, 4, 5, 6));

        var result = MatchSetupValidator.Validate(setup);

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.LineupSizeInvalid));
    }

    [Fact]
    public void RatingBoundsZeroAndHundredAreAccepted()
    {
        var home = TestData.Team(1) with
        {
            Roster = [TestData.Player(0, 0), .. Enumerable.Range(1, 9).Select(index => TestData.Player(index, 100))],
        };

        var away = TestData.Team(2) with
        {
            Roster = [TestData.Player(0, 100), .. Enumerable.Range(1, 9).Select(index => TestData.Player(index, 0))],
        };

        var result = MatchSetupValidator.Validate(TestData.Setup(home: home, away: away));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void RatingOutsideZeroHundredIsRejected(int invalidValue)
    {
        var player = TestData.Player(0) with
        {
            Ratings = TestData.RatingsWith(ratings => ratings with { ThreePoint = invalidValue }),
        };

        var home = TestData.Team(1) with { Roster = [player, .. TestData.Roster(10).Skip(1)] };
        var result = MatchSetupValidator.Validate(TestData.Setup(home: home));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.RatingOutOfRange));
        Assert.Contains(result.Errors, e => e.Code == MatchSetupErrorCode.RatingOutOfRange && e.Field.EndsWith(".Ratings.ThreePoint", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryRatingAttributeIsCoveredByValidation()
    {
        // Doğrulama tablosu elle yazılmıştır. Yeni bir attribute eklendiğinde
        // sessizce doğrulanmamış kalmaması için kapsam burada sabitlenir.
        var properties = typeof(PlayerRatings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(int))
            .Select(property => property.Name)
            .ToList();

        Assert.Equal(18, properties.Count);

        var home = TestData.Team(1) with
        {
            Roster = [TestData.Player(0) with { Ratings = TestData.Ratings(-1) }],
        };

        var result = MatchSetupValidator.Validate(TestData.Setup(home: home));

        var reported = result.Errors
            .Where(error => error.Code == MatchSetupErrorCode.RatingOutOfRange)
            .Select(error => error.Field.Split('.')[^1])
            .ToList();

        Assert.Equal(18, reported.Count);
        Assert.Equal(
            properties.OrderBy(name => name, StringComparer.Ordinal),
            reported.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void SetupIsIsolatedFromExternalCollectionMutation()
    {
        var players = Enumerable.Range(0, 10).Select(TestData.Player).ToList();
        var team = new Team
        {
            Id = Ids.Team(7),
            Name = "Kurgusal Takim 7",
            Roster = [.. players],
        };

        var lineupIds = players.Take(5).Select(player => player.Id).ToList();
        var lineup = new Lineup { PlayerIds = [.. lineupIds] };
        var setup = TestData.Setup(home: team, homeLineup: lineup);

        // Kaynak listeler oluşturulduktan sonra değiştiriliyor.
        players.Clear();
        lineupIds.Clear();
        players.Add(TestData.Player(42));

        Assert.Equal(10, setup.Home.Roster.Length);
        Assert.Equal(5, setup.HomeLineup.PlayerIds.Length);
        Assert.True(MatchSetupValidator.Validate(setup).IsValid);
    }

    [Fact]
    public void EmptyMatchIdIsRejected()
    {
        var result = MatchSetupValidator.Validate(TestData.Setup(matchId: Guid.Empty));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.MatchIdMissing));
    }

    [Fact]
    public void SameTeamOnBothSidesIsRejected()
    {
        var team = TestData.Team(1);

        var result = MatchSetupValidator.Validate(TestData.Setup(home: team, away: team));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.SameTeamOnBothSides));
    }

    [Fact]
    public void DuplicateRosterPlayerIdIsRejected()
    {
        var duplicated = TestData.Player(3);
        var home = TestData.Team(1) with
        {
            Roster = [.. TestData.Roster().Append(duplicated)],
        };

        var result = MatchSetupValidator.Validate(TestData.Setup(home: home));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.DuplicateRosterPlayerId));
    }

    [Fact]
    public void EmptyRosterIsReportedWithoutThrowing()
    {
        var home = new Team { Id = Ids.Team(1), Name = "Kurgusal Takim 1", Roster = default };

        var result = MatchSetupValidator.Validate(TestData.Setup(home: home));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.RosterMissing));
    }

    [Fact]
    public void UninitializedLineupIsReportedWithoutThrowing()
    {
        var lineup = new Lineup { PlayerIds = default };

        var result = MatchSetupValidator.Validate(TestData.Setup(homeLineup: lineup));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.LineupMissing));
    }

    [Theory]
    [InlineData("engine")]
    [InlineData("rules")]
    [InlineData("balanceHash")]
    [InlineData("rngAlgorithm")]
    [InlineData("rngVersion")]
    public void IncompleteEngineIdentityIsRejected(string field)
    {
        var identity = TestData.Identity() with
        {
            EngineVersion = field == "engine" ? " " : EngineVersion.Current,
            RulesVersion = field == "rules" ? string.Empty : "rules-v0.1",
            BalanceConfigHash = field == "balanceHash" ? null! : "balance-v0.1-hash-placeholder",
            RngAlgorithm = field == "rngAlgorithm" ? null! : RngIdentity.Algorithm,
            RngVersion = field == "rngVersion" ? null! : RngIdentity.Version,
        };

        var result = MatchSetupValidator.Validate(TestData.Setup(engine: identity));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.EngineIdentityIncomplete));
    }

    [Fact]
    public void EngineVersionMismatchIsRejected()
    {
        var result = MatchSetupValidator.Validate(
            TestData.Setup(engine: TestData.Identity(engineVersion: "0.0.1-not-this-engine")));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.EngineIdentityIncomplete));
    }

    [Fact]
    public void RngIdentityMismatchIsRejected()
    {
        // Setup, motorun üretmediği bir RNG'yi iddia ederse aynı seed maçı yeniden
        // üretemez. Bu, sessiz bir replay bozulmasıdır ve başlangıçta reddedilir.
        var result = MatchSetupValidator.Validate(
            TestData.Setup(engine: TestData.Identity(rngAlgorithm: "XorShift128Plus")));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.RngIdentityMismatch));
    }

    [Fact]
    public void ValidationReportsAllProblemsInsteadOfFailingFast()
    {
        var home = TestData.Team(1) with
        {
            Roster = [TestData.Player(0) with { Ratings = TestData.Ratings(150) }, .. TestData.Roster(10).Skip(1)],
        };

        var result = MatchSetupValidator.Validate(
            TestData.Setup(matchId: Guid.Empty, home: home, homeLineup: TestData.Lineup(0, 0, 1, 2, 3)));

        Assert.False(result.IsValid);
        Assert.True(result.Contains(MatchSetupErrorCode.MatchIdMissing));
        Assert.True(result.Contains(MatchSetupErrorCode.RatingOutOfRange));
        Assert.True(result.Contains(MatchSetupErrorCode.DuplicateLineupPlayerId));
    }

    [Fact]
    public void CanonicalRosterOrderDoesNotDependOnInputOrder()
    {
        var roster = TestData.Roster();
        var reversed = roster.Reverse().ToImmutableArray();

        var canonical = RosterOrdering.Canonical(roster);
        var canonicalFromReversed = RosterOrdering.Canonical(reversed);

        Assert.Equal(
            canonical.Select(player => player.Id),
            canonicalFromReversed.Select(player => player.Id));
        Assert.Equal(
            canonical.Select(player => player.Id).OrderBy(id => id),
            canonical.Select(player => player.Id));
    }

    [Fact]
    public void CanonicalOrderingHandlesUninitializedRoster()
    {
        Assert.Empty(RosterOrdering.Canonical(default));
    }
}
