using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>Possession kimlik muhasebesi. 06 §4 tablosu ve 08'in T05 testi.</summary>
public class PossessionIdentityTests
{
    private static MatchResult Run(ulong seed = 12_345) =>
        new MatchSimulation(M2TestData.Config()).Simulate(M2TestData.NeutralMirror(seed));

    [Fact]
    public void OffensiveReboundKeepsPossessionIdentity()
    {
        var result = Run();
        var rebounds = result.Events.Where(e => e.Type == MatchEventType.Rebound).ToList();

        Assert.NotEmpty(rebounds);
        Assert.Contains(rebounds, r => r.PayloadAs<ReboundPayload>().Offensive);

        foreach (var rebound in rebounds.Where(r => r.PayloadAs<ReboundPayload>().Offensive))
        {
            // OREB bir ribaund event'i üretir; sonraki event hâlâ aynı possession'da
            // olmalıdır. Yani OREB hiçbir zaman PossessionEnded üretmez.
            var next = result.Events.First(e => e.Sequence > rebound.Sequence);

            if (next.Type == MatchEventType.PossessionEnded)
            {
                Assert.Fail(
                    $"OREB sonrası possession kapandı (Sequence {next.Sequence}). "
                    + "OREB possession kimliğini korumalıdır.");
            }
        }
    }

    [Fact]
    public void OffensiveReboundIsFollowedByAnActionInTheSamePossession()
    {
        var result = Run();
        var offensive = result.Events
            .Where(e => e.Type == MatchEventType.Rebound && e.PayloadAs<ReboundPayload>().Offensive)
            .ToList();

        Assert.NotEmpty(offensive);

        foreach (var rebound in offensive)
        {
            var nextAction = result.Events.First(e =>
                e.Sequence > rebound.Sequence
                && e.ActionId.HasValue
                && e.PossessionId == rebound.PossessionId);

            Assert.Equal(rebound.PossessionId, nextAction.PossessionId);
        }
    }

    [Fact]
    public void DefensiveReboundStartsANewPossession()
    {
        var result = Run();
        var defensive = result.Events
            .Where(e => e.Type == MatchEventType.Rebound && !e.PayloadAs<ReboundPayload>().Offensive)
            .ToList();

        Assert.NotEmpty(defensive);

        foreach (var rebound in defensive)
        {
            var ended = result.Events.First(e =>
                e.Sequence > rebound.Sequence && e.Type == MatchEventType.PossessionEnded);

            var payload = ended.PayloadAs<PossessionEndedPayload>();

            Assert.Equal(PossessionEndReason.DefensiveRebound, payload.Reason);
            Assert.Equal(rebound.PossessionId, payload.EndedPossessionId);

            var started = result.Events.First(e =>
                e.Sequence > ended.Sequence && e.Type == MatchEventType.PossessionStarted);

            Assert.Equal(
                payload.EndedPossessionId + 1,
                started.PayloadAs<PossessionStartedPayload>().NewPossessionId);
        }
    }

    [Fact]
    public void PossessionIdentityIsNeverReused()
    {
        var result = Run();
        var ids = result.Events
            .Where(e => e.Type == MatchEventType.PossessionStarted)
            .Select(e => e.PayloadAs<PossessionStartedPayload>().NewPossessionId)
            .ToList();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(1, ids[0]);

        for (var index = 1; index < ids.Count; index++)
        {
            Assert.Equal(ids[index - 1] + 1, ids[index]);
        }
    }

    [Fact]
    public void EveryPossessionHasMatchingStartAndEndReasons()
    {
        var result = Run();

        var opened = result.Events
            .Where(e => e.Type == MatchEventType.PossessionStarted)
            .Select(e => e.PayloadAs<PossessionStartedPayload>().NewPossessionId)
            .ToHashSet();

        var closed = result.Events
            .Where(e => e.Type == MatchEventType.PossessionEnded)
            .Select(e => e.PayloadAs<PossessionEndedPayload>().EndedPossessionId)
            .ToHashSet();

        Assert.Equal(opened, closed);
    }

    [Fact]
    public void PossessionAlwaysChangesSidesWithinAPeriod()
    {
        // Top devri tarafi degistirir, ancak periyot basi possession'ı D32'ye gore
        // yeniden cekilir; ayni taraf arka arka baslayabilir. Bu yuzden karsilastirma
        // periyot ici yapilir.
        var result = Run();
        var started = result.Events
            .Where(e => e.Type == MatchEventType.PossessionStarted)
            .ToList();

        for (var index = 1; index < started.Count; index++)
        {
            if (started[index].Period != started[index - 1].Period)
            {
                continue;
            }

            var previousOffense = started[index - 1].PayloadAs<PossessionStartedPayload>().Offense;
            var currentOffense = started[index].PayloadAs<PossessionStartedPayload>().Offense;

            Assert.NotEqual(previousOffense, currentOffense);
        }
    }

    [Fact]
    public void ScoreAndTurnoverAlsoStartANewPossession()
    {
        var result = Run();

        var reasons = result.Events
            .Where(e => e.Type == MatchEventType.PossessionEnded)
            .Select(e => e.PayloadAs<PossessionEndedPayload>().Reason)
            .ToList();

        Assert.Contains(PossessionEndReason.Scored, reasons);
        Assert.Contains(PossessionEndReason.Turnover, reasons);
        Assert.Contains(PossessionEndReason.DefensiveRebound, reasons);
    }

    [Fact]
    public void FirstPossessionIsDecidedByASeededSingleBitDraw()
    {
        // D32: modulo değil, tek bit. Aynı seed aynı başlangıç; bitin değişmesi
        // başlangıç tarafını değiştirir. 06 §4, home/away bias'ını yasaklar.
        var firstOffense = new List<TeamSide>();

        for (var seed = 0UL; seed < 40UL; seed++)
        {
            var result = Run(seed);
            var first = result.Events.First(e => e.Type == MatchEventType.PossessionStarted);

            firstOffense.Add(first.PayloadAs<PossessionStartedPayload>().Offense);
        }

        Assert.Contains(TeamSide.Home, firstOffense);
        Assert.Contains(TeamSide.Away, firstOffense);

        // Aynı seed tekrarlandığında başlangıç değişmemelidir.
        var repeat = Run(7);
        var repeatFirst = repeat.Events.First(e => e.Type == MatchEventType.PossessionStarted);

        Assert.Equal(
            Run(7).Events.First(e => e.Type == MatchEventType.PossessionStarted).PayloadAs<PossessionStartedPayload>().Offense,
            repeatFirst.PayloadAs<PossessionStartedPayload>().Offense);
    }
}
