using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Projection;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>Shot outcomes and score accounting. 06 section 5, 07 section 3, and the T04 test.</summary>
public class ShotOutcomeTests
{
    private static MatchResult Run(ulong seed = 12_345) =>
        new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(seed));

    [Fact]
    public void MadeShotAwardsItsTypePointsExactlyOnce()
    {
        var result = Run();
        var made = result.Events.Where(e => e.Type == MatchEventType.ShotMade).ToList();

        Assert.NotEmpty(made);

        foreach (var shot in made)
        {
            var payload = shot.PayloadAs<ShotMadePayload>();

            Assert.Equal(ShotResolver.PointsFor(payload.ShotType), payload.Points);
            Assert.NotNull(shot.TeamId);
            Assert.NotNull(shot.PlayerId);
        }
    }

    [Fact]
    public void MissedShotAwardsNoFieldGoalPoints()
    {
        // Tek mac'a baglanmaz: belirli bir seed'de hic kacan sut olmayabilir.
        // Kural bir macin ozelligi degil, tum akisin ozelligidir.
        var missed = 0;

        foreach (var seed in new ulong[] { 1, 2, 3, 12_345, 99, 4_242, 777_777 })
        {
            var result = Run(seed);

            var misses = result.Events.Where(e => e.Type == MatchEventType.ShotMissed).ToList();
            missed += misses.Count;

            foreach (var shot in misses)
            {
                // 06 §87: kacan SHOOTING FAUL'lu deneme FGA sayilmaz; bayrak her
                // zaman true degildir.
                if (FoulIdFor(result.Events, shot) != 0)
                {
                    Assert.False(shot.PayloadAs<ShotMissedPayload>().CountsAsFieldGoalAttempt);
                }
            }

            // Kacan sut puan uretmez. Toplam skor yalniz isabetli sut ve serbest
            // atislardan gelir; 08 §2: PTS = 2*2PM + 3*3PM + FTM.
            var fieldGoalPoints = result.Events
                .Where(e => e.Type == MatchEventType.ShotMade)
                .Sum(e => e.PayloadAs<ShotMadePayload>().Points);

            var freeThrowPoints = result.Events.Count(e => e.Type == MatchEventType.FreeThrowMade);

            Assert.Equal(result.HomeScore + result.AwayScore, fieldGoalPoints + freeThrowPoints);
        }

        Assert.True(missed > 0, "Yedi seed'de hic kacan sut yok; test degerini yitirmisti.");
    }

    [Fact]
    public void EveryShotAttemptIsSettledByExactlyOneMadeOrMissed()
    {
        // 07 §3: ShotAttempt şutu açar, settlement tek canonical yoldan gelir.
        var result = Run();

        var attempts = result.Events
            .Where(e => e.Type == MatchEventType.ShotAttempt)
            .Select(e => e.PayloadAs<ShotAttemptPayload>().ShotId)
            .ToList();

        var settlements = result.Events
            .Where(e => e.Type is MatchEventType.ShotMade or MatchEventType.ShotMissed)
            .Select(e => e.Type == MatchEventType.ShotMade
                ? e.PayloadAs<ShotMadePayload>().ShotId
                : e.PayloadAs<ShotMissedPayload>().ShotId)
            .ToList();

        Assert.NotEmpty(attempts);
        Assert.Equal(attempts.Count, settlements.Count);
        Assert.Equal(attempts.OrderBy(id => id), settlements.OrderBy(id => id));
        Assert.Equal(attempts.Count, attempts.Distinct().Count());
    }

    [Fact]
    public void ShotAttemptAloneNeverCountsAsAFieldGoalAttempt()
    {
        // 07 §3: ShotAttempt istatistik yazmaz. Yalnız ShotMade/ShotMissed
        // payload'ı sayılabilirliği taşır. Boş akış sıfır vermeli, attempt
        // event'i veriden türetilseydi sıfırdan farklı olurdu.
        var empty = new BoxScoreProjector(M2TestData.NeutralMirror()).Project([]);
        var home = empty.Home;
        var away = empty.Away;

        Assert.Equal(0, home.FieldGoalsAttempted);
        Assert.Equal(0, home.FieldGoalsMade);
        Assert.Equal(0, home.Points);
        Assert.Equal(0, away.Points);

        // Yalnız ShotAttempt içeren bir akış da hiçbir deneme yazmamalıdır.
        var attemptOnly = new List<MatchEvent>
        {
            new MatchEvent
            {
                MatchId = M2TestData.NeutralMirror().MatchId,
                Sequence = 1,
                SchemaVersion = MatchSimulation.EventSchemaVersion,
                EngineVersion = EngineVersion.Current,
                ConfigHash = "test",
                Type = MatchEventType.ShotAttempt,
                Period = 1,
                GameClockMs = 700_000,
                ElapsedGameTimeMs = 20_000,
                Payload = new ShotAttemptPayload(1, ShotType.ThreePoint, 50, 100),
                TeamId = TeamSide.Home,
                PlayerId = M2TestData.NeutralMirror().Home.Lineup.PlayerIds[0],
            },
        };

        var projected = new BoxScoreProjector(M2TestData.NeutralMirror()).Project(attemptOnly);

        Assert.Equal(0, projected.Home.FieldGoalsAttempted);
        Assert.Equal(0, projected.Home.ThreePointersAttempted);
    }

    [Fact]
    public void ScoreEqualsTheSumOfScoringEventPoints()
    {
        foreach (var seed in new ulong[] { 1, 42, 12_345, 999_999 })
        {
            var result = Run(seed);

            var homeFromEvents = result.Events
                .Where(e => e.Type == MatchEventType.ShotMade && e.TeamId == TeamSide.Home)
                .Sum(e => e.PayloadAs<ShotMadePayload>().Points)
                + result.Events.Count(
                    e => e.Type == MatchEventType.FreeThrowMade && e.TeamId == TeamSide.Home);

            var awayFromEvents = result.Events
                .Where(e => e.Type == MatchEventType.ShotMade && e.TeamId == TeamSide.Away)
                .Sum(e => e.PayloadAs<ShotMadePayload>().Points)
                + result.Events.Count(
                    e => e.Type == MatchEventType.FreeThrowMade && e.TeamId == TeamSide.Away);

            Assert.Equal(result.HomeScore, homeFromEvents);
            Assert.Equal(result.AwayScore, awayFromEvents);
        }
    }

    [Fact]
    public void BoxScoreScoreAgreesWithTheEngineScore()
    {
        // İki bağımsız yolun eşleşmesi: motorun kendi sayacı ve event türevi istatistik.
        foreach (var seed in new ulong[] { 3, 777, 54_321 })
        {
            var result = Run(seed);

            var home = result.BoxScores.First(b => b.Team == TeamSide.Home);
            var away = result.BoxScores.First(b => b.Team == TeamSide.Away);

            Assert.Equal(result.HomeScore, home.Points);
            Assert.Equal(result.AwayScore, away.Points);
        }
    }

    [Fact]
    public void EveryMadeShotHasAnAssisterWhoIsNotTheShooter()
    {
        var setup = M2TestData.NeutralMirror();
        var result = Run();
        var made = result.Events.Where(e => e.Type == MatchEventType.ShotMade).ToList();

        Assert.NotEmpty(made);

        foreach (var shot in made)
        {
            Assert.NotNull(shot.SecondaryPlayerId);
            Assert.NotEqual(shot.PlayerId, shot.SecondaryPlayerId);

            var assisterSide = SideOf(setup, shot.SecondaryPlayerId!.Value);

            Assert.Equal(shot.TeamId, assisterSide);
            Assert.Equal(shot.TeamId, SideOf(setup, shot.PlayerId!.Value));
        }
    }

    [Fact]
    public void StrongerRosterScoresMoreAcrossManySeeds()
    {
        // Tek maçta sonuç yönünü zorlamak yanıltıcı olurdu (08 §59). 60 seed'in
        // toplamı üzerinde güçlü kadronun önde olması beklenir. Bu bir denge
        // kalibrasyonu değil, yön kontrolüdür.
        var strongTotal = 0;
        var weakTotal = 0;

        for (var seed = 0UL; seed < 60UL; seed++)
        {
            var result = new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.QualityGap(seed, 20));

            strongTotal += result.HomeScore;
            weakTotal += result.AwayScore;
        }

        Assert.True(
            strongTotal > weakTotal,
            $"Güçlü kadro geride kaldı: {strongTotal} - {weakTotal} (60 seed).");
    }

    /// <summary>
    /// Verilen sutun birlikte calan faul kimligi; yoksa 0. Faul event'i ayni
    /// aksiyonla yayinlandigi icin (M3) korelasyon actionId uzerinden yapilir.
    /// </summary>
    private static long FoulIdFor(IReadOnlyList<MatchEvent> events, MatchEvent miss)
    {
        var attempt = events.FirstOrDefault(e =>
            e.Type == MatchEventType.ShotAttempt
            && e.PayloadAs<ShotAttemptPayload>().ShotId == miss.PayloadAs<ShotMissedPayload>().ShotId);

        if (attempt is null)
        {
            return 0;
        }

        var foul = events.FirstOrDefault(e =>
            e.Type == MatchEventType.Foul && e.ActionId == attempt.ActionId);

        return foul is null ? 0 : foul.PayloadAs<FoulPayload>().FoulId;
    }

    /// <summary>
    /// Bir oyuncunun tarafı. <b>Kadro</b> sınırı kullanılır, ilk beş değil:
    /// M3'ten beri foul-out yedeklemesi oyuncuyu değiştirebiliyor ve değişmeyen
    /// sınır kadrodur.
    /// </summary>
    private static TeamSide SideOf(MatchSetup setup, Guid playerId)
    {
        if (setup.Home.Team.Roster.Any(player => player.Id == playerId))
        {
            return TeamSide.Home;
        }

        Assert.Contains(playerId, setup.Away.Team.Roster.Select(player => player.Id).ToList());
        return TeamSide.Away;
    }
}
