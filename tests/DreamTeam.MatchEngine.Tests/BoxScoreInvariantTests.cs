using DreamTeam.MatchEngine.Config;
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
    public void ScoreEqualsTwoPointersPlusThreePointersPlusFreeThrows()
    {
        // 08 §2: PTS = 2*2PM + 3*3PM + FTM. M3'te serbest atis puani ayrica
        // sayildigi icin bu esitlik M2'den daha katidir.
        foreach (var seed in Seeds)
        {
            var result = Run(seed);
            var home = result.BoxScores.First(b => b.Team == TeamSide.Home);
            var away = result.BoxScores.First(b => b.Team == TeamSide.Away);

            Assert.Equal(
                home.Points,
                (2 * home.TwoPointersMade) + (3 * home.ThreePointersMade) + home.FreeThrowMakes);
            Assert.Equal(
                away.Points,
                (2 * away.TwoPointersMade) + (3 * away.ThreePointersMade) + away.FreeThrowMakes);
        }
    }

    [Fact]
    public void FreeThrowCountersAreInternallyConsistent()
    {
        foreach (var seed in Seeds)
        {
            var result = Run(seed);

            foreach (var box in result.BoxScores)
            {
                Assert.InRange(box.FreeThrowMakes, 0, box.FreeThrowAttempts);
                Assert.True(box.PersonalFouls >= 0);
                Assert.True(box.Blocks >= 0);
            }

            foreach (var player in result.PlayerBoxScores)
            {
                Assert.InRange(player.FreeThrowMakes, 0, player.FreeThrowAttempts);
                Assert.Equal(
                    player.Points,
                    (2 * player.TwoPointersMade) + (3 * player.ThreePointersMade) + player.FreeThrowMakes);
            }
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
    public void EveryLiveMissLeadsToReboundOrFreeThrowSeriesOrHorn()
    {
        // 06 §5: her missed attempt'e oyuncu ribaundu dagitilmaz. Bir kacan sut
        // uc yolla kapanir: canli ribaund, shooting foul -> serbest atis serisi,
        // ya da duduk sonrasi periyot kapanisi. Ikisinden fazlasi bir eksiktir.
        foreach (var seed in Seeds)
        {
            var result = Run(seed);
            var events = result.Events;

            foreach (var miss in events.Where(e => e.Type == MatchEventType.ShotMissed))
            {
                var shotId = miss.PayloadAs<ShotMissedPayload>().ShotId;
                var attempt = events.FirstOrDefault(e =>
                    e.Type == MatchEventType.ShotAttempt
                    && e.PayloadAs<ShotAttemptPayload>().ShotId == shotId);

                var nextAfterMiss = events.First(e => e.Sequence > miss.Sequence);
                var horn = miss.GameClockMs == 0;

                if (horn)
                {
                    Assert.NotEqual(MatchEventType.Rebound, nextAfterMiss.Type);
                    continue;
                }

                if (attempt is not null)
                {
                    // D69: kacan SHOOTING faulda ribaund yok, serbest atis var.
                    // Kacan NON-SHOOTING faulda ise top canlidir ve ribaund firsati
                    // devam eder. Ayrim Foul payload'indan yapilir.
                    var foul = events.FirstOrDefault(e =>
                        e.Type == MatchEventType.Foul && e.ActionId == attempt.ActionId);

                    if (foul is not null)
                    {
                        if (foul.PayloadAs<FoulPayload>().Type == FoulType.Shooting)
                        {
                            // Kacan shooting faul: serbest atis var, ribaund yok.
                            Assert.NotEqual(MatchEventType.Rebound, nextAfterMiss.Type);
                        }
                        else
                        {
                            // Non-shooting faul: bonus aktifse serbest atis, degilse
                            // canli ribaund. Ikisi de M3 kurallarina uyar.
                            Assert.True(
                                nextAfterMiss.Type
                                    is MatchEventType.Rebound
                                    or MatchEventType.FreeThrowAttempt,
                                $"Faullu kacan sutun ardindaki beklenmeyen event: "
                                + $"{nextAfterMiss.Type}");
                        }

                        continue;
                    }
                }

                Assert.Equal(MatchEventType.Rebound, nextAfterMiss.Type);
                Assert.Equal(shotId, nextAfterMiss.PayloadAs<ReboundPayload>().ShotId);
            }
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
                Assert.Equal(team.PersonalFouls, players.Sum(p => p.PersonalFouls));
                Assert.Equal(team.FreeThrowAttempts, players.Sum(p => p.FreeThrowAttempts));
                Assert.Equal(team.Assists, players.Sum(p => p.Assists));
                Assert.Equal(team.Turnovers, players.Sum(p => p.Turnovers));
                Assert.Equal(team.FreeThrowMakes, players.Sum(p => p.FreeThrowMakes));
                Assert.Equal(team.Blocks, players.Sum(p => p.Blocks));
                Assert.Equal(team.OffensiveRebounds, players.Sum(p => p.OffensiveRebounds));
                Assert.Equal(team.DefensiveRebounds, players.Sum(p => p.DefensiveRebounds));
            }
        }
    }

    [Fact]
    public void OnlyRosterPlayersAppearInTheBoxScore()
    {
        // M3'te foul-out yedeklemesi kadro disi oyuncu sokmaz. Degisen sey
        // "ilk bes" degil, sinir "kadro"dur: yedek oyuncular da istatistik alabilir.
        foreach (var seed in Seeds)
        {
            var setup = M2TestData.NeutralMirror(seed);
            var roster = setup.Home.Team.Roster.Concat(setup.Away.Team.Roster).Select(p => p.Id).ToHashSet();
            var result = Run(seed);

            foreach (var player in result.PlayerBoxScores)
            {
                Assert.Contains(player.PlayerId, roster);
            }
        }
    }

    [Fact]
    public void EveryEventAttributedToAPlayerIsInTheRoster()
    {
        // M2'de sahadaki bes sabitti. M3'te foul-out yedeklemesi oyuncuyu
        // degistirebilir; degismeyen sinir kadro mudur.
        foreach (var seed in Seeds)
        {
            var setup = M2TestData.NeutralMirror(seed);
            var roster = setup.Home.Team.Roster.Concat(setup.Away.Team.Roster).Select(p => p.Id).ToHashSet();
            var result = Run(seed);

            foreach (var matchEvent in result.Events)
            {
                if (matchEvent.PlayerId is { } playerId)
                {
                    Assert.Contains(playerId, roster);
                }

                if (matchEvent.SecondaryPlayerId is { } secondaryId)
                {
                    Assert.Contains(secondaryId, roster);
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
