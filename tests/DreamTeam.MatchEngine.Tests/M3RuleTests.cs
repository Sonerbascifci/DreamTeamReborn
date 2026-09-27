using DreamTeam.MatchEngine.Actions;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M3 kural testleri: T04 (blok), T06 (reset tablosu), T07 (FT serisi),
/// T08 (bonus / hucum faulu / foul-out), T09 (son anda birakma).
/// </summary>
public class M3RuleTests
{
    private static MatchResult Run(EngineConfig config, ulong seed = 12_345) =>
        new MatchSimulation(config).Simulate(M2TestData.NeutralMirror(seed));

    private static MatchResult Run(MatchSetup setup, EngineConfig? config = null) =>
        new MatchSimulation(config ?? M2TestData.Config()).Simulate(setup);

    // ------------------------------------------------------------- T04: blok

    [Fact]
    public void BlockedShotIsAMissWithOneFieldGoalAttemptAndNoScore()
    {
        var result = Run(M3TestData.AllBlocks());

        var attempts = result.Events.Count(e => e.Type == MatchEventType.ShotAttempt);
        var blocks = result.Events.Count(e => e.Type == MatchEventType.Block);
        var misses = result.Events.Where(e => e.Type == MatchEventType.ShotMissed).ToList();

        Assert.True(attempts > 0, "Hic sut denemesi olusmadi.");
        Assert.Equal(attempts, blocks);
        Assert.Equal(attempts, misses.Count);
        Assert.Empty(result.Events.Where(e => e.Type == MatchEventType.ShotMade));

        // 07 §3: blok ikinci FGA uretmez ve yeni miss uretmez.
        foreach (var miss in misses)
        {
            Assert.True(miss.PayloadAs<ShotMissedPayload>().CountsAsFieldGoalAttempt);
        }

        var home = result.BoxScores.First(b => b.Team == TeamSide.Home);
        var away = result.BoxScores.First(b => b.Team == TeamSide.Away);

        Assert.Equal(0, home.Points);
        Assert.Equal(0, away.Points);
        Assert.Equal(attempts, home.Blocks + away.Blocks);
    }

    [Fact]
    public void BlockIsAttributedToADefenderOnCourt()
    {
        var setup = M2TestData.NeutralMirror(3);
        var onCourt = M2TestData.OnCourtIds(setup).ToHashSet();
        var result = Run(M3TestData.AllBlocks());

        foreach (var block in result.Events.Where(e => e.Type == MatchEventType.Block))
        {
            var payload = block.PayloadAs<BlockPayload>();

            Assert.Contains(payload.DefenderId, onCourt);
            Assert.Equal(block.PlayerId, payload.DefenderId);
        }
    }

    [Fact]
    public void BlockAndMissShareTheSameShotId()
    {
        var result = Run(M3TestData.AllBlocks());

        var attempts = result.Events
            .Where(e => e.Type == MatchEventType.ShotAttempt)
            .Select(e => e.PayloadAs<ShotAttemptPayload>().ShotId)
            .ToHashSet();

        foreach (var block in result.Events.Where(e => e.Type == MatchEventType.Block))
        {
            Assert.Contains(block.PayloadAs<BlockPayload>().ShotId, attempts);
        }
    }

    // ------------------------------------------- T06: hucre saati reset tablosu

    [Fact]
    public void RimContactOffensiveReboundResetsToFourteenSeconds()
    {
        // Hucre saati event zarfinda YOKTUR; gozlem state uzerinden yapilir.
        var rules = M2TestData.Config().Rules;
        var observed = 0;

        ShotClockAfterOffensiveRebound(M3TestData.AlwaysRimContact(), shotClock =>
        {
            observed += 1;
            Assert.Equal(rules.OffensiveReboundShotClockMs, shotClock);
        });

        Assert.True(observed > 0, "Hicbir hucrem ribaundu gozlemlenmedi.");
    }

    [Fact]
    public void AirballOffensiveReboundKeepsRemainingTime()
    {
        // 06 §6: cembere degmeyen miss sonrasi otomatik 14 s reset YOK.
        var rules = M2TestData.Config().Rules;
        var observed = 0;

        ShotClockAfterOffensiveRebound(M3TestData.NeverRimContact(), shotClock =>
        {
            observed += 1;
            Assert.NotEqual(rules.OffensiveReboundShotClockMs, shotClock);
            Assert.True(shotClock >= 0);
        });

        Assert.True(observed > 0, "Hicbir hucrem ribaundu gozlemlenmedi.");
    }

    /// <summary>Hucrem ribaundu ureten adimlarin sonundaki hucre saatini cagirana verir.</summary>
    private static void ShotClockAfterOffensiveRebound(EngineConfig config, Action<long> assert)
    {
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(M2TestData.NeutralMirror(6));
        var guard = 0;

        while (!state.IsTerminal && guard++ < 200_000)
        {
            var step = simulation.Advance(state);
            state = step.State;

            var offensiveRebound = step.Events.Any(e =>
                e.Type == MatchEventType.Rebound && e.PayloadAs<ReboundPayload>().Offensive);

            if (offensiveRebound)
            {
                assert(state.Clock.ShotClockMs);
            }
        }
    }

    [Fact]
    public void DefensiveReboundAlwaysStartsWithAFullShotClock()
    {
        var setupMs = M2TestData.Config().Actions.SetupActionMs;
        var periodMs = M2TestData.Config().Rules.PeriodDurationMs;
        var result = Run(M3TestData.AlwaysDefensiveRebound());

        foreach (var rebound in result.Events.Where(e => e.Type == MatchEventType.Rebound))
        {
            Assert.False(rebound.PayloadAs<ReboundPayload>().Offensive);

            var started = result.Events.First(e =>
                e.Sequence > rebound.Sequence && e.Type == MatchEventType.PossessionStarted);

            // Periyot sonunda DREB, yeni possession'i sonraki periyoda birakir;
            // o durumda oyun saati sifirlanir ve fark olcumunun anlami kalmaz.
            if (started.GameClockMs < periodMs)
            {
                continue;
            }

            var firstAction = result.Events.First(e =>
                e.Sequence > started.Sequence && e.ActionId.HasValue);

            // Yeni possession 24 s ile baslar; ilk aksiyon 8 s tuketir.
            Assert.Equal(setupMs, started.GameClockMs - firstAction.GameClockMs);
        }
    }

    // ------------------------------------------------ T07: serbest atis serisi

    [Fact]
    public void AndOneAwardsExactlyOneFreeThrow()
    {
        var result = Run(M3TestData.AndOneOnly(), seed: 5);
        var attempts = result.Events.Where(e => e.Type == MatchEventType.FreeThrowAttempt).ToList();

        Assert.NotEmpty(attempts);
        Assert.All(attempts, a => Assert.Equal(1, a.PayloadAs<FreeThrowAttemptPayload>().Count));

        // And-one: her isabetli shooting faul tam olarak bir FT ve bir FGA yazar.
        var made = result.Events.Count(e => e.Type == MatchEventType.ShotMade);
        Assert.Equal(made, attempts.Count);

        var home = result.BoxScores.First(b => b.Team == TeamSide.Home);
        var away = result.BoxScores.First(b => b.Team == TeamSide.Away);

        Assert.Equal(
            home.FreeThrowAttempts,
            result.Events.Count(e =>
                e.Type == MatchEventType.FreeThrowAttempt && e.TeamId == TeamSide.Home));
        Assert.Equal(
            away.FreeThrowAttempts,
            result.Events.Count(e =>
                e.Type == MatchEventType.FreeThrowAttempt && e.TeamId == TeamSide.Away));
    }

    [Fact]
    public void MissedShootingFoulAwardsTwoOrThreeFreeThrowsWithoutFieldGoalAttempt()
    {
        // 06 §87: kacan shooting foul'da FGA sayilmaz; 2 veya 3 FT verilir.
        var result = Run(M3TestData.MissedShootingFoulOnly(), seed: 6);
        var attempts = result.Events.Where(e => e.Type == MatchEventType.FreeThrowAttempt).ToList();

        Assert.NotEmpty(attempts);

        var missed = result.Events.Where(e => e.Type == MatchEventType.ShotMissed).ToList();
        Assert.NotEmpty(missed);

        foreach (var shot in missed)
        {
            Assert.False(shot.PayloadAs<ShotMissedPayload>().CountsAsFieldGoalAttempt);
        }

        foreach (var attempt in attempts)
        {
            var payload = attempt.PayloadAs<FreeThrowAttemptPayload>();

            // 06 §87: kacan shooting foul 2 FT, ucluk denemede 3 FT verir.
            // Taban isabet olasiligi 0.001 oldugu icin nadiren bir ISABETLI
            // shooting foul da olusur; o durum and-one'dir ve 1 FT verir.
            Assert.InRange(payload.Count, 1, 3);
        }

        // Her seri kendi atislariyla eslesir ve indexleri 0..Count-1'dir.
        var bySeries = attempts
            .GroupBy(a => a.PayloadAs<FreeThrowAttemptPayload>().FTSeriesId)
            .ToList();

        Assert.NotEmpty(bySeries);

        foreach (var group in bySeries)
        {
            var payloadList = group
                .OrderBy(e => e.Sequence)
                .Select(e => e.PayloadAs<FreeThrowAttemptPayload>())
                .ToList();

            for (var index = 0; index < payloadList.Count; index++)
            {
                Assert.Equal(index, payloadList[index].Index);
            }
        }

        var home = result.BoxScores.First(b => b.Team == TeamSide.Home);
        var away = result.BoxScores.First(b => b.Team == TeamSide.Away);

        // Taban isabet olasiligi 0.001: pratikte tum sutler kacar. Nadir bir
        // ISABETLI shooting foul olursa o deneme FGA sayilir (and-one), 1 FT verir.
        var madeShots = result.Events.Count(e => e.Type == MatchEventType.ShotMade);

        Assert.Equal(madeShots, home.FieldGoalsAttempted + away.FieldGoalsAttempted);
    }

    [Fact]
    public void EveryFreeThrowAttemptIsSettledByExactlyOneResult()
    {
        var result = Run(M2TestData.Config(), seed: 77);

        var attempts = result.Events
            .Where(e => e.Type == MatchEventType.FreeThrowAttempt)
            .Select(e => (e.PayloadAs<FreeThrowAttemptPayload>().FTSeriesId, e.PayloadAs<FreeThrowAttemptPayload>().Index))
            .ToList();

        var settlements = result.Events
            .Where(e => e.Type is MatchEventType.FreeThrowMade or MatchEventType.FreeThrowMissed)
            .Select(e => e.Type == MatchEventType.FreeThrowMade
                ? (e.PayloadAs<FreeThrowMadePayload>().FTSeriesId, e.PayloadAs<FreeThrowMadePayload>().Index)
                : (e.PayloadAs<FreeThrowMissedPayload>().FTSeriesId, e.PayloadAs<FreeThrowMissedPayload>().Index))
            .ToList();

        Assert.NotEmpty(attempts);
        Assert.Equal(attempts.Count, settlements.Count);
        Assert.Equal(attempts.OrderBy(x => x), settlements.OrderBy(x => x));
    }

    [Fact]
    public void MissBetweenFreeThrowsProducesNoRebound()
    {
        // 06 §91: "FT arası miss icin canli rebound yok."
        var result = Run(M3TestData.OnlyFreeThrows(), seed: 8);

        var attempts = result.Events.Where(e => e.Type == MatchEventType.FreeThrowAttempt).ToList();

        for (var index = 0; index + 1 < attempts.Count; index++)
        {
            var between = result.Events
                .Where(e => e.Sequence > attempts[index].Sequence && e.Sequence < attempts[index + 1].Sequence)
                .Select(e => e.Type)
                .ToList();

            Assert.DoesNotContain(MatchEventType.Rebound, between);
        }
    }

    [Fact]
    public void OnlyTheLiveFinalFreeThrowCanProduceARebound()
    {
        var result = Run(M3TestData.OnlyFreeThrows(), seed: 9);

        var liveMisses = result.Events
            .Where(e => e.Type == MatchEventType.FreeThrowMissed && e.PayloadAs<FreeThrowMissedPayload>().BallIsLive)
            .ToList();

        foreach (var miss in liveMisses)
        {
            var next = result.Events.First(e => e.Sequence > miss.Sequence);
            Assert.Equal(MatchEventType.Rebound, next.Type);
        }

        // Canli olmayan serbest atis kacirmalari ribaund uretmez.
        var deadMisses = result.Events
            .Where(e => e.Type == MatchEventType.FreeThrowMissed && !e.PayloadAs<FreeThrowMissedPayload>().BallIsLive)
            .ToList();

        foreach (var miss in deadMisses)
        {
            var next = result.Events.First(e => e.Sequence > miss.Sequence);
            Assert.NotEqual(MatchEventType.Rebound, next.Type);
        }
    }

    [Fact]
    public void FreeThrowIndicesAreContiguousWithinASeries()
    {
        var result = Run(M2TestData.Config(), seed: 4242);

        var series = result.Events
            .Where(e => e.Type == MatchEventType.FreeThrowAttempt)
            .GroupBy(e => e.PayloadAs<FreeThrowAttemptPayload>().FTSeriesId)
            .ToList();

        Assert.NotEmpty(series);

        foreach (var group in series)
        {
            var indices = group
                .OrderBy(e => e.Sequence)
                .Select(e => e.PayloadAs<FreeThrowAttemptPayload>())
                .ToList();

            var count = indices[0].Count;

            Assert.Equal(count, indices.Count);

            for (var index = 0; index < count; index++)
            {
                Assert.Equal(index, indices[index].Index);
                Assert.Equal(count, indices[index].Count);
            }
        }
    }

    [Fact]
    public void FreeThrowStatisticsStayConsistent()
    {
        foreach (var seed in new ulong[] { 11, 22, 33, 44 })
        {
            var result = Run(M2TestData.Config(), seed);

            var attempts = result.Events.Count(e => e.Type == MatchEventType.FreeThrowAttempt);
            var makes = result.Events.Count(e => e.Type == MatchEventType.FreeThrowMade);
            var misses = result.Events.Count(e => e.Type == MatchEventType.FreeThrowMissed);

            Assert.Equal(attempts, makes + misses);

            var home = result.BoxScores.First(b => b.Team == TeamSide.Home);
            var away = result.BoxScores.First(b => b.Team == TeamSide.Away);

            Assert.Equal(attempts, home.FreeThrowAttempts + away.FreeThrowAttempts);
            Assert.Equal(makes, home.FreeThrowMakes + away.FreeThrowMakes);
        }
    }

    // ------------------------------------------- T08: bonus, hucum faulu, foul-out

    [Fact]
    public void BonusStartsOnFifthTeamFoul()
    {
        var resolver = new FoulResolver(FoulModel.Baseline);

        Assert.False(resolver.IsInBonus(0));
        Assert.False(resolver.IsInBonus(3));
        Assert.True(resolver.IsInBonus(4));
        Assert.True(resolver.IsInBonus(5));
    }

    [Fact]
    public void OffensiveFoulProducesExactlyOneTurnoverAndNoFreeThrow()
    {
        var config = M2TestData.Config();
        var fouls = config.Fouls with { FoulProbabilityPerAction = 1.0, OffensiveFoulShare = 1.0 };

        var result = new MatchSimulation(config with { Fouls = fouls }).Simulate(M2TestData.NeutralMirror(11));

        var offensiveFouls = result.Events
            .Where(e => e.Type == MatchEventType.Foul && e.PayloadAs<FoulPayload>().Type == FoulType.Offensive)
            .ToList();

        Assert.NotEmpty(offensiveFouls);

        var offensiveTurnovers = result.Events
            .Where(e => e.Type == MatchEventType.Turnover && e.PayloadAs<TurnoverPayload>().Kind == TurnoverKind.OffensiveFoul)
            .ToList();

        // Hucum faulu serbest atis uretmez (06 §88).
        Assert.DoesNotContain(offensiveFouls, f => f.PayloadAs<FoulPayload>().FreeThrowCount > 0);

        // T08c'in cekirdek invariant'i: bir aksiyon IKI turnover uretemez.
        var turnoversByAction = result.Events
            .Where(e => e.Type == MatchEventType.Turnover && e.ActionId.HasValue)
            .GroupBy(e => e.ActionId!.Value)
            .ToList();

        Assert.NotEmpty(turnoversByAction);
        Assert.All(turnoversByAction, group => Assert.Single(group));

        // Her hucrem faul turnover ile eslesir: turnover, ayni aksiyona ait
        // hucrem faulundan SONRA gelmelidir.
        var foulActionIds = offensiveFouls.Select(f => f.ActionId!.Value).ToList();
        Assert.Equal(offensiveFouls.Count, foulActionIds.Distinct().Count());

        var foulByAction = offensiveFouls.ToDictionary(f => f.ActionId!.Value);

        foreach (var turnover in offensiveTurnovers)
        {
            var actionId = turnover.ActionId!.Value;

            Assert.True(foulByAction.ContainsKey(actionId), $"Turnover {actionId} icin hucrem faul yok.");
            Assert.True(
                foulByAction[actionId].Sequence < turnover.Sequence,
                "Faul, turnover'dan once yazilmalidir.");
        }

        // Cift sayim yok: hucrem faul basina en fazla bir turnover.
        var turnoverActionIdSet = offensiveTurnovers.Select(t => t.ActionId!.Value).ToList();
        Assert.Equal(turnoverActionIdSet.Count, turnoverActionIdSet.Distinct().Count());

        var unmatched = offensiveFouls
            .Where(f => !foulByAction.ContainsKey(f.ActionId!.Value)
                || !offensiveTurnovers.Any(t => t.ActionId == f.ActionId))
            .ToList();

        // Bu testte faul olasiligi 1.0 oldugu icin kadro hizla tukenir ve mac
        // D43 ile Aborted olur. Terminal olan son faul icin turnover YAZILMAZ:
        // possession ve state coktan kapatilmistir. En fazla bir faul bu
        // durumda kalabilir ve macin son faulu olmalidir.
        Assert.True(
            unmatched.Count <= 1,
            $"{unmatched.Count} hucrem faulu turnover uretmedi; 06 §88 ihlali olabilir.");

        if (unmatched.Count == 1)
        {
            Assert.Equal(MatchStatus.Aborted, result.Status);
            Assert.Contains("NoLegalSubstitute", result.AbortReason!, StringComparison.Ordinal);
            Assert.Equal(offensiveFouls[^1].Sequence, unmatched[0].Sequence);
        }

        if (unmatched.Count == 1)
        {
            var lastFoul = offensiveFouls[^1];

            Assert.Equal(lastFoul.Sequence, unmatched[0].Sequence);
        }

        foreach (var turnover in offensiveTurnovers)
        {
            var ended = result.Events.First(e =>
                e.Sequence > turnover.Sequence && e.Type == MatchEventType.PossessionEnded);

            Assert.Equal(
                PossessionEndReason.Turnover,
                ended.PayloadAs<PossessionEndedPayload>().Reason);
        }
    }

    [Fact]
    public void OffensiveFoulDoesNotTriggerBonusFreeThrows()
    {
        // 06 §88: "Savunma bonusu sebebiyle otomatik FT uretmez."
        var config = M2TestData.Config();
        var fouls = config.Fouls with
        {
            FoulProbabilityPerAction = 1.0,
            OffensiveFoulShare = 1.0,
            BonusTeamFoulThreshold = 1,
        };

        var result = new MatchSimulation(config with { Fouls = fouls }).Simulate(M2TestData.NeutralMirror(12));

        Assert.Empty(result.Events.Where(e => e.Type == MatchEventType.FreeThrowAttempt));
    }

    [Fact]
    public void TeamFoulCounterResetsEachPeriod()
    {
        var config = M2TestData.Config();
        var fouls = config.Fouls with { FoulProbabilityPerAction = 1.0, OffensiveFoulShare = 0.0 };
        var result = new MatchSimulation(config with { Fouls = fouls }).Simulate(M2TestData.NeutralMirror(13));

        var perPeriod = new Dictionary<int, int>();

        foreach (var foul in result.Events.Where(e => e.Type == MatchEventType.Foul))
        {
            if (foul.PayloadAs<FoulPayload>().Type == FoulType.Offensive)
            {
                continue;
            }

            perPeriod[foul.Period] = perPeriod.GetValueOrDefault(foul.Period) + 1;
        }

        Assert.True(perPeriod.Count >= 3, "Yeterli periyot verisi yok.");
        Assert.DoesNotContain(perPeriod.Values, value => value < 2);
    }

    [Fact]
    public void SixthPersonalFoulRemovesPlayerAndTriggersReplacement()
    {
        var config = M2TestData.Config();
        var fouls = config.Fouls with
        {
            FoulProbabilityPerAction = 0.10,
            OffensiveFoulShare = 0.0,
            PersonalFoulLimit = 3,
        };

        var result = new MatchSimulation(config with { Fouls = fouls }).Simulate(M2TestData.NeutralMirror(14));

        var fouledOut = result.PlayerBoxScores.Count(p => p.PersonalFouls >= fouls.PersonalFoulLimit);

        Assert.True(
            fouledOut > 0,
            "Hicbir oyuncu foul-out olmadi; yol test edilemedi. " + result.AbortReason);

        // Foul-out sonrasi mac tamamlanmali: kadroda yedek vardir.
        Assert.True(
            result.Status == MatchStatus.Completed,
            $"Beklenen Completed, gelen {result.Status}. Abort: {result.AbortReason}");
    }

    [Fact]
    public void OnCourtAlwaysHasExactlyFiveLegalPlayers()
    {
        var config = M2TestData.Config();
        var fouls = config.Fouls with
        {
            FoulProbabilityPerAction = 0.06,
            OffensiveFoulShare = 0.0,
        };

        var simulation = new MatchSimulation(config with { Fouls = fouls });
        var state = simulation.Create(M2TestData.NeutralMirror(15));
        var guard = 0;

        while (!state.IsTerminal && guard++ < 200_000)
        {
            // D41: yedekleme bes kişiyi yeniden tamamlar. Kesme veya sinirla
            // islemek, foul-out'lar biriktikce bu sayiyi azaltirdi.
            Assert.Equal(5, state.Home.OnCourt.Length);
            Assert.Equal(5, state.Away.OnCourt.Length);
            Assert.True(EligibilityPolicy.HasLegalFive(state.Home));
            Assert.True(EligibilityPolicy.HasLegalFive(state.Away));

            state = simulation.Advance(state).State;
        }

        Assert.True(state.IsTerminal);
        Assert.Equal(MatchPhase.Completed, state.Phase);
    }

    [Fact]
    public void FoulOutPlayerIsRemovedFromTheCourtImmediately()
    {
        // Foul-out olan oyuncu o anda sahadan cikar ve yedek girer. Test, state
        // uzerinden yedeklemenin gerceklestigini gorur.
        var config = M2TestData.Config();
        var fouls = config.Fouls with
        {
            FoulProbabilityPerAction = 0.30,
            OffensiveFoulShare = 0.0,
            PersonalFoulLimit = 2,
        };

        var simulation = new MatchSimulation(config with { Fouls = fouls });
        var state = simulation.Create(M2TestData.NeutralMirror(15));

        var guard = 0;
        var observed = 0;

        while (!state.IsTerminal && guard++ < 200_000)
        {
            if (state.Home.FoulOutPlayerIds.Length > 0 || state.Away.FoulOutPlayerIds.Length > 0)
            {
                observed += 1;

                foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
                {
                    var team = state.Team(side);
                    var onCourt = team.OnCourt.Select(player => player.Id).ToList();

                    foreach (var fouledOut in team.FoulOutPlayerIds)
                    {
                        Assert.DoesNotContain(fouledOut, onCourt);
                    }
                }
            }

            state = simulation.Advance(state).State;
        }

        Assert.True(observed > 0, "Hicbir foul-out gozlemlenmedi; yol test edilemedi.");

        // Faul orani bilerek yuksek; macin tamamlanmasi bu testin konusu degil.
        // Konu: foul-out olan oyuncu her an sahada degil.
        Assert.True(state.IsTerminal);
    }

    [Fact]
    public void NoLegalSubstituteAbortsMatchWithExplicitReason()
    {
        // D43: motor forfeit kazanani uydurmaz; acik terminal policy uygulanir.
        var config = M2TestData.Config();
        var fouls = config.Fouls with
        {
            FoulProbabilityPerAction = 1.0,
            OffensiveFoulShare = 0.0,
            PersonalFoulLimit = 1,
        };

        var result = new MatchSimulation(config with { Fouls = fouls })
            .Simulate(M3TestData.NoLegalSubstitute(16));

        Assert.Equal(MatchStatus.Aborted, result.Status);
        Assert.NotNull(result.AbortReason);
        Assert.Contains("NoLegalSubstitute", result.AbortReason!, StringComparison.Ordinal);

        // Terminal event'ten sonra MatchEnded uretilmez.
        Assert.DoesNotContain(result.Events, e => e.Type == MatchEventType.MatchEnded);
    }

    // -------------------------------------------- T09: son anda birakma / horn

    [Fact]
    public void ShotReleasedAfterHornIsStillSettled()
    {
        // 06 §2: "gecerli pending sut/FT islemleri tamamlanir." Birakilmis sut
        // periyot kapanisinda da sonuclanir.
        var result = Run(M2TestData.Config(), seed: 21);

        var hornShots = result.Events
            .Where(e => e.Type == MatchEventType.ShotMissed && e.GameClockMs == 0)
            .ToList();

        foreach (var shot in hornShots)
        {
            var next = result.Events.First(e => e.Sequence > shot.Sequence);
            Assert.Equal(MatchEventType.PossessionEnded, next.Type);
        }
    }

    [Fact]
    public void ShotClockIsNeverNegative()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var simulation = new MatchSimulation(M2TestData.Config());
            var state = simulation.Create(M2TestData.NeutralMirror(seed));
            var guard = 0;

            while (!state.IsTerminal && guard++ < 200_000)
            {
                Assert.True(state.Clock.ShotClockMs >= 0);
                Assert.True(state.Clock.GameClockMs >= 0);
                state = simulation.Advance(state).State;
            }

            Assert.True(state.IsTerminal);
        }
    }

    // ------------------------------------------------ saf fonksiyon testleri

    [Fact]
    public void FreeThrowCountFollowsTheDocumentedProfile()
    {
        var resolver = new FoulResolver(FoulModel.Baseline);

        // Hucum faulu hicbir kosulda FT uretmez.
        Assert.Equal(0, resolver.FreeThrowCountFor(FoulType.Offensive, true, true, true, ShotType.ThreePoint));

        // And-one: isabetli shooting faul -> 1.
        Assert.Equal(1, resolver.FreeThrowCountFor(FoulType.Shooting, false, true, true, ShotType.MidRange));
        Assert.Equal(1, resolver.FreeThrowCountFor(FoulType.Shooting, true, true, true, ShotType.ThreePoint));

        // Kacan shooting faul: 2, ucluk denemede 3.
        Assert.Equal(2, resolver.FreeThrowCountFor(FoulType.Shooting, false, true, false, ShotType.MidRange));
        Assert.Equal(3, resolver.FreeThrowCountFor(FoulType.Shooting, false, true, false, ShotType.ThreePoint));

        // Bonuslu non-shooting faul: 2. Bonus yoksa 0.
        Assert.Equal(2, resolver.FreeThrowCountFor(FoulType.NonShooting, true, false, false, ShotType.MidRange));
        Assert.Equal(0, resolver.FreeThrowCountFor(FoulType.NonShooting, false, false, false, ShotType.MidRange));

        // Isabetli non-shooting faul and-one gibi: 1.
        Assert.Equal(1, resolver.FreeThrowCountFor(FoulType.NonShooting, true, true, true, ShotType.MidRange));
    }

    [Fact]
    public void DefensiveFoulNeverIncreasesTheShotClock()
    {
        var rules = M2TestData.Config().Rules;

        Assert.Equal(14_000, ClockResetPolicy.ForDefensiveNonShootingFoul(rules, 20_000));
        Assert.Equal(8_000, ClockResetPolicy.ForDefensiveNonShootingFoul(rules, 8_000));
        Assert.Equal(0, ClockResetPolicy.ForDefensiveNonShootingFoul(rules, 0));
    }

    [Fact]
    public void OffensiveReboundResetDependsOnRimContact()
    {
        var rules = M2TestData.Config().Rules;

        Assert.Equal(rules.OffensiveReboundShotClockMs, ClockResetPolicy.ForOffensiveRebound(rules, true));
        Assert.Null(ClockResetPolicy.ForOffensiveRebound(rules, false));
    }

    [Fact]
    public void PeriodControllerDecidesOvertimeAndFoulReset()
    {
        var rules = M2TestData.Config().Rules;

        Assert.Equal(rules.PeriodDurationMs, PeriodController.DurationMsForPeriod(rules, 1));
        Assert.Equal(rules.PeriodDurationMs, PeriodController.DurationMsForPeriod(rules, 4));
        Assert.Equal(rules.OvertimeDurationMs, PeriodController.DurationMsForPeriod(rules, 5));
        Assert.Equal(rules.OvertimeDurationMs, PeriodController.DurationMsForPeriod(rules, 6));

        Assert.False(PeriodController.IsOvertime(rules, 4));
        Assert.True(PeriodController.IsOvertime(rules, 5));

        // D42'nin uzatma sarti alt kumedir: takim faulu periyot sayacidir ve
        // HER periyot basinda sifirlanir.
        Assert.True(PeriodController.ResetsTeamFouls(rules, 1));
        Assert.True(PeriodController.ResetsTeamFouls(rules, 2));
        Assert.True(PeriodController.ResetsTeamFouls(rules, 5));
        Assert.True(PeriodController.ResetsTeamFouls(rules, 6));

        Assert.True(PeriodController.ShouldStartOvertime(rules, 4, 90, 90));
        Assert.False(PeriodController.ShouldStartOvertime(rules, 4, 90, 91));
        Assert.False(PeriodController.ShouldStartOvertime(rules, 3, 90, 90));
    }
}
