using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Projection;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// 08_MATCHING_AND_BALANCE.md §2'deki zorunlu invariant'lar. Bunlar "tekrar testi"
/// değildir: her biri kural ihlaline karşı somut bir yapısal güvence verir.
/// </summary>
public class BoxScoreInvariantTests
{
    private static readonly ulong[] Seeds = [1, 2, 3, 12_345, 99, 4_242, 777_777];

    [Fact]
    public void ScoreEqualsTwoPointersPlusThreePointers()
    {
        foreach (var seed in Seeds)
        {
            var result = Run(seed);
            var home = result.BoxScores.First(b => b.Team == TeamSide.Home);
            var away = result.BoxScores.First(b => b.Team == TeamSide.Away);

            Assert.Equal(
                home.Points,
                (2 * home.TwoPointersMade) + (3 * home.ThreePointersMade));
            Assert.Equal(
                away.Points,
                (2 * away.TwoPointersMade) + (3 * away.ThreePointersMade));
        }
    }

    [Fact]
    public void ShotCountersAreInternallyConsistent()
    {
        foreach (var seed in Seeds)
        {
            foreach (var box in AllBoxes(seed))
            {
                Assert.InRange(box.ThreePointersMade, 0, box.ThreePointersAttempted);
                Assert.InRange(box.FieldGoalsMade, 0, box.FieldGoalsAttempted);
                Assert.True(box.ThreePointersAttempted <= box.FieldGoalsAttempted);
                Assert.True(box.TwoPointersAttempted <= box.FieldGoalsAttempted);
                Assert.True(box.ThreePointersMade <= box.FieldGoalsMade);
                Assert.Equal(
                    box.FieldGoalsAttempted,
                    box.TwoPointersAttempted + box.ThreePointersAttempted);
                Assert.Equal(
                    box.FieldGoalsMade,
                    box.TwoPointersMade + box.ThreePointersMade);
            }
        }
    }

    [Fact]
    public void EveryLiveMissProducesReboundOpportunityExceptAtTheHorn()
    {
        // 06 section 5: a rebound is not distributed to a player automatically for
        // every missed attempt, and at the end of a period no rebound is produced.
        // In M2 the shot resolves synchronously, so every miss is a live rebound
        // opportunity unless the game clock expired during the shot flight.
        foreach (var seed in Seeds)
        {
            var result = Run(seed);
            var misses = result.Events.Where(e => e.Type == MatchEventType.ShotMissed).ToList();
            var rebounds = result.Events.Count(e => e.Type == MatchEventType.Rebound);
            var hornMisses = misses.Count(m => m.GameClockMs == 0);

            Assert.True(misses.Count > 0, "Test verisinde hic kacan sut yok; test degersizlesir.");
            Assert.Equal(misses.Count - hornMisses, rebounds);
            Assert.True(rebounds > 0, "Hic ribaund firsati olusmadi.");
        }
    }

    [Fact]
    public void ReboundOwnershipIsNeverAmbiguous()
    {
        // 08 §2: takım ribaundu oyuncuya rastgele dağıtılamaz. M2'de bu yol
        // üretilmediği için her ribaund tek bir oyuncuya aittir.
        foreach (var seed in Seeds)
        {
            foreach (var rebound in Run(seed).Events.Where(e => e.Type == MatchEventType.Rebound))
            {
                var payload = rebound.PayloadAs<ReboundPayload>();

                Assert.False(payload.IsTeamRebound);
                Assert.NotNull(rebound.PlayerId);
            }
        }
    }

    [Fact]
    public void TeamTotalsEqualTheSumOfTheirPlayers()
    {
        foreach (var seed in Seeds)
        {
            var result = Run(seed);

            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                var team = result.BoxScores.First(b => b.Team == side);
                var players = result.PlayerBoxScores.Where(p => p.Team == side).ToList();

                Assert.Equal(team.Points, players.Sum(p => p.Points));
                Assert.Equal(team.FieldGoalsMade, players.Sum(p => p.FieldGoalsMade));
                Assert.Equal(team.FieldGoalsAttempted, players.Sum(p => p.FieldGoalsAttempted));
                Assert.Equal(team.ThreePointersMade, players.Sum(p => p.ThreePointersMade));
                Assert.Equal(team.ThreePointersAttempted, players.Sum(p => p.ThreePointersAttempted));
                Assert.Equal(team.Assists, players.Sum(p => p.Assists));
                Assert.Equal(team.Turnovers, players.Sum(p => p.Turnovers));
                Assert.Equal(team.OffensiveRebounds, players.Sum(p => p.OffensiveRebounds));
                Assert.Equal(team.DefensiveRebounds, players.Sum(p => p.DefensiveRebounds));
            }
        }
    }

    [Fact]
    public void OnlyOnCourtPlayersAppearInTheBoxScore()
    {
        foreach (var seed in Seeds)
        {
            var setup = M2TestData.NeutralMirror(seed);
            var onCourt = M2TestData.OnCourtIds(setup).ToHashSet();
            var result = Run(seed);

            foreach (var player in result.PlayerBoxScores)
            {
                Assert.Contains(player.PlayerId, onCourt);
            }
        }
    }

    [Fact]
    public void EveryEventAttributedToAPlayerWasOnCourt()
    {
        // M2'de substitution yoktur, bu yüzden sahadaki beş maç boyunca sabittir.
        // Yanlış kimlik atfı bu testle yakalanır.
        foreach (var seed in Seeds)
        {
            var setup = M2TestData.NeutralMirror(seed);
            var onCourt = M2TestData.OnCourtIds(setup).ToHashSet();
            var result = Run(seed);

            foreach (var matchEvent in result.Events)
            {
                if (matchEvent.PlayerId is { } playerId)
                {
                    Assert.Contains(playerId, onCourt);
                }

                if (matchEvent.SecondaryPlayerId is { } secondaryId)
                {
                    Assert.Contains(secondaryId, onCourt);
                }
            }
        }
    }

    [Fact]
    public void NoCounterIsNegative()
    {
        foreach (var seed in Seeds)
        {
            var result = Run(seed);

            foreach (var box in result.PlayerBoxScores)
            {
                AssertPlayerCountersNonNegative(box);
            }

            foreach (var box in result.BoxScores)
            {
                AssertTeamCountersNonNegative(box);
            }
        }
    }

    [Fact]
    public void FieldGoalPercentageIsNullWhenThereAreNoAttempts()
    {
        // 08 §6: sıfır denominator için null, sıfır değil.
        var empty = new ProjectionResult(
            new TeamBoxScore { Team = TeamSide.Home, TeamName = "x" },
            new TeamBoxScore { Team = TeamSide.Away, TeamName = "y" },
            []);

        Assert.Null(empty.Home.FieldGoalPercentage);
    }

    private static void AssertTeamCountersNonNegative(TeamBoxScore box)
    {
        Assert.True(box.FieldGoalsMade >= 0);
        Assert.True(box.FieldGoalsAttempted >= 0);
        Assert.True(box.TwoPointersMade >= 0);
        Assert.True(box.TwoPointersAttempted >= 0);
        Assert.True(box.ThreePointersMade >= 0);
        Assert.True(box.ThreePointersAttempted >= 0);
        Assert.True(box.Assists >= 0);
        Assert.True(box.Turnovers >= 0);
        Assert.True(box.OffensiveRebounds >= 0);
        Assert.True(box.DefensiveRebounds >= 0);
        Assert.True(box.TeamRebounds >= 0);
        Assert.True(box.Points >= 0);
    }

    private static void AssertPlayerCountersNonNegative(PlayerBoxScore box)
    {
        Assert.True(box.FieldGoalsMade >= 0);
        Assert.True(box.FieldGoalsAttempted >= 0);
        Assert.True(box.TwoPointersMade >= 0);
        Assert.True(box.TwoPointersAttempted >= 0);
        Assert.True(box.ThreePointersMade >= 0);
        Assert.True(box.ThreePointersAttempted >= 0);
        Assert.True(box.Assists >= 0);
        Assert.True(box.Turnovers >= 0);
        Assert.True(box.OffensiveRebounds >= 0);
        Assert.True(box.DefensiveRebounds >= 0);
        Assert.True(box.Points >= 0);
    }

    private static MatchResult Run(ulong seed) =>
        new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(seed));

    private static TeamBoxScore[] AllBoxes(ulong seed) => Run(seed).BoxScores.ToArray();
}
