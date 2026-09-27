using System.Collections.Immutable;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M5: komut zarfı, siralama (D86), idempotency (T14) ve dogrulama.
/// </summary>
public class CommandValidationTests
{
    // ------------------------------------------------------------------- siralama

    [Fact]
    public void AcceptedOrderIsAssignedByTheEngine()
    {
        // 07 §5: "AcceptedOrder sunucunun atadigi deterministik sira; client
        // belirleyemez." Gelen komut 0 tasir; motor kendi sayacini kullanir.
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        var step = simulation.Advance(
            state,
            [M5TestData.ChangeOffense(commandId: Guid.NewGuid())]);

        var queue = step.State.CommandQueue;

        Assert.Equal(1, queue.NextOrder);
        Assert.Equal(0, queue.Pending.Single().AcceptedOrder);
    }

    [Fact]
    public void OrdersAreAssignedInArrivalOrderAndRemainStable()
    {
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var step = simulation.Advance(
            state,
            [
                M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost, commandId: first),
                M5TestData.ChangeOffense(tactic: OffensiveTactic.PerimeterMotion, commandId: second),
                M5TestData.ChangeOffense(tactic: OffensiveTactic.PickAndRoll, commandId: third),
            ]);

        var pending = step.State.CommandQueue.Pending;

        Assert.Equal(3, pending.Length);
        Assert.Equal([first, second, third], pending.Select(c => c.CommandId));
        Assert.Equal([0, 1, 2], pending.Select(c => c.AcceptedOrder));
    }

    [Fact]
    public void TwoPendingTacticCommandsApplyInAcceptedOrderLastWriteWins()
    {
        // D86: FIFO; ayni tip taktik komutlarinda last-write-wins dogal olarak
        // cikar. Sondaki kazanir.
        var simulation = new MatchSimulation(M2TestData.Config());
        var setup = M2TestData.NeutralMirror();

        var state = simulation.Create(setup);
        var after = M5TestData.AdvanceSteps(
            simulation,
            state,
            6,
            [
                M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost),
                M5TestData.ChangeOffense(tactic: OffensiveTactic.PerimeterMotion),
            ]);

        Assert.Equal(OffensiveTactic.PerimeterMotion, after.Home.OffensiveTactic);
    }

    // ---------------------------------------------------------------- idempotency

    [Fact]
    public void DuplicateCommandIdHasNoSecondEffect()
    {
        // T14: ayni CommandId iki kez -> BIR etki.
        //
        // D94: komut, `Advance`e verildigi ADIMIN sundugu sinirda hemen
        // uygulanir. Ilk adim `NotStarted` -> PeriodBreak siniri sunar; bu
        // yuzden komut ActionDecision'a hedeflenmis olsa bile bir sonraki
        // adimda (LiveBall) uygulanir. Test bu yolu bilincli olarak izler.
        var commandId = Guid.NewGuid();
        var command = M5TestData.ChangeOffense(commandId: commandId);

        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        // 1. adim: PeriodBreak sunuluyor, komut kuyruga girer.
        var queued = simulation.Advance(state, [command]);
        Assert.Empty(queued.CommandResults);
        Assert.Equal(1, queued.State.CommandQueue.PendingCount);
        Assert.Equal(OffensiveTactic.Balanced, queued.State.Home.OffensiveTactic);

        // 2. adim: LiveBall -> ActionDecision, komut UYGULANIR.
        var applied = simulation.Advance(queued.State);
        var appliedResult = Assert.Single(applied.CommandResults);
        Assert.True(appliedResult.Applied);
        Assert.Equal(OffensiveTactic.InsidePost, applied.State.Home.OffensiveTactic);

        // 3. adim: ayni CommandId tekrar gelir -> reddedilir, etki YOK.
        var duplicate = simulation.Advance(applied.State, [command]);
        var rejected = Assert.Single(duplicate.CommandResults);
        Assert.False(rejected.Applied);
        Assert.Equal(CommandRejectionReason.DuplicateCommand, rejected.Reason);

        // Etki bir kez oldu: komut kuyruga ikinci kez girmedi.
        Assert.Equal(0, duplicate.State.CommandQueue.PendingCount);
    }

    [Fact]
    public void ACommandIsAppliedAtTheBoundaryOfTheStepThatReceivesIt()
    {
        // D94: `Advance(state, commands)` cagirisi "simdi gonderiliyor"
        // demektir. Kabul edilen komut, sunulan sinir o adimda varsa BEKLEMEZ.
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        // Ilk adim PeriodBreak sunar; DeadBall komutu kuyruga girer.
        var first = simulation.Advance(
            state,
            [M5TestData.Timeout(TeamSide.Home, TimeoutKind.Full)]);

        Assert.Equal(1, first.State.CommandQueue.PendingCount);
        Assert.Equal(0, first.State.Home.FullTimeoutsUsed);

        // Sonraki adimlar hucrem siniri gormeye baslayinca komut uygulanir.
        var applied = M5TestData.AdvanceSteps(simulation, first.State, 40);

        // Butce artmis olmali: komut bir kez harcandi.
        Assert.Equal(1, applied.Home.FullTimeoutsUsed);
        Assert.Equal(0, applied.CommandQueue.PendingCount);
    }

    [Fact]
    public void DuplicateCommandIsAlsoRejectedAtEnvelopeTime()
    {
        // Zarf dogrulamasindan gecen bir komut daha sonra ayni id ile gelirse
        // kuyruga GIRMEZ.
        var commandId = Guid.NewGuid();
        var queue = CommandQueue.Empty;

        var (afterFirst, firstResults) = queue.Accept(
            [M5TestData.ChangeOffense(commandId: commandId)], 1);

        Assert.Empty(firstResults);
        Assert.Equal(1, afterFirst.PendingCount);

        // Komut islendikten sonra D93 geregi kuyruktan da cikar.
        var settled = afterFirst.Settle(afterFirst.Pending);

        Assert.Equal(0, settled.PendingCount);

        // Ayni id ile yeniden gelir: kuyruga GIRMEZ, reddedilir.
        var (afterSecond, secondResults) = settled.Accept(
            [M5TestData.ChangeOffense(commandId: commandId)], 2);

        var rejected = Assert.Single(secondResults);
        Assert.Equal(CommandRejectionReason.DuplicateCommand, rejected.Reason);
        Assert.Equal(0, afterSecond.PendingCount);
    }

    // -------------------------------------------------------------------- stale

    [Fact]
    public void StaleSequenceIsRejected()
    {
        // T14: gecmise ait ExpectedSequence reddedilir.
        var queue = CommandQueue.Empty;

        var (after, results) = queue.Accept(
        [
            M5TestData.ChangeOffense() with
            {
                ExpectedSequence = 5,
            },
        ], nowSequence: 100);

        var rejected = Assert.Single(results);
        Assert.Equal(CommandRejectionReason.StaleSequence, rejected.Reason);
        Assert.Equal(0, after.PendingCount);
    }

    [Fact]
    public void ExpectedSequenceEqualToNowIsAccepted()
    {
        // "Gecmis" demek <now demektir; esitlik gecmis DEGILDIR.
        var queue = CommandQueue.Empty;

        var (after, results) = queue.Accept(
        [
            M5TestData.ChangeOffense() with { ExpectedSequence = 100 },
        ], nowSequence: 100);

        Assert.Empty(results);
        Assert.Equal(1, after.PendingCount);
    }

    [Fact]
    public void NullExpectedSequenceSkipsTheStaleCheck()
    {
        var queue = CommandQueue.Empty;

        var (after, results) = queue.Accept(
            [M5TestData.ChangeOffense()],
            nowSequence: 9_999);

        Assert.Empty(results);
        Assert.Equal(1, after.PendingCount);
    }

    // ------------------------------------------------------------------- zarf

    [Fact]
    public void MissingPayloadFieldsAreRejected()
    {
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        // Substitution: giren veya cikan eksik.
        var badSubstitute = new ScheduledManagerCommand
        {
            CommandId = Guid.NewGuid(),
            Side = TeamSide.Home,
            Kind = ManagerCommandKind.Substitute,
            Payload = new CommandPayload { IncomingPlayerId = Guid.NewGuid() },
            AcceptedOrder = 0,
            TargetBoundary = CommandBoundary.DeadBall,
        };

        var step = simulation.Advance(state, [badSubstitute]);

        var rejected = Assert.Single(step.CommandResults);
        Assert.False(rejected.Applied);
        Assert.Equal(CommandRejectionReason.InvalidPayload, rejected.Reason);
        Assert.Equal(0, step.State.CommandQueue.PendingCount);
    }

    [Fact]
    public void ACommandKindCannotTargetTheWrongBoundary()
    {
        // Taktik komutu dead-ball'a hedeflenemez (07 §6 madde 1).
        var command = M5TestData.ChangeOffense(
            boundary: CommandBoundary.DeadBall);

        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        var step = simulation.Advance(state, [command]);

        var rejected = Assert.Single(step.CommandResults);
        Assert.Equal(CommandRejectionReason.InvalidPayload, rejected.Reason);
    }

    [Fact]
    public void SubstitutionCannotTargetTheActionBoundary()
    {
        var command = M5TestData.Substitute(
            M2TestData.NeutralMirror(),
            boundary: CommandBoundary.ActionDecision);

        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        var step = simulation.Advance(state, [command]);

        Assert.Equal(
            CommandRejectionReason.InvalidPayload,
            Assert.Single(step.CommandResults).Reason);
    }

    [Fact]
    public void UnknownEnumValuesAreRejected()
    {
        var command = M5TestData.ChangeOffense() with
        {
            Payload = new CommandPayload { OffensiveTactic = (OffensiveTactic)99 },
        };

        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        var step = simulation.Advance(state, [command]);

        Assert.Equal(
            CommandRejectionReason.UnknownEnumValue,
            Assert.Single(step.CommandResults).Reason);
    }

    [Fact]
    public void SubstitutionWithAnUnknownPlayerIsRejected()
    {
        var command = M5TestData.Substitute(M2TestData.NeutralMirror(), incoming: Guid.NewGuid());

        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror());

        var step = simulation.Advance(state, [command]);

        Assert.Equal(
            CommandRejectionReason.PlayerNotInRoster,
            Assert.Single(step.CommandResults).Reason);
    }

    [Fact]
    public void TheSamePlayerCannotEnterAndLeave()
    {
        var setup = M2TestData.NeutralMirror();
        var onCourt = setup.Home.Lineup.PlayerIds[0];

        var command = M5TestData.Substitute(setup, incoming: onCourt, outgoing: onCourt);

        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(setup);

        var step = simulation.Advance(state, [command]);

        Assert.Equal(
            CommandRejectionReason.InvalidPayload,
            Assert.Single(step.CommandResults).Reason);
    }

    // ---------------------------------------------------------- mac bitimi

    [Fact]
    public void PendingCommandsAreExpiredWhenTheMatchEnds()
    {
        // 07 §6 madde 4: "Mac bittiginde bekleyen komutlar Expired/Rejected
        // olarak sonuclanir."
        //
        // D94 yuzunden ucu uca kosuda her komut sunulan sinira GELIR GELIR
        // uygulanir. Komutu kuyrukta TUTMAMIZ icin `ExpiresAfterSequence = 0`
        // veriyoruz: kuyruga girer, ama ilk adimin sonunda
        // `ExpireStaleCommands` tarafindan dusurulur.
        var simulation = new MatchSimulation(M2TestData.Config());
        var setup = M2TestData.NeutralMirror();

        var result = simulation.Simulate(
            setup,
            [
                M5TestData.Substitute(setup, boundary: CommandBoundary.PeriodBreak)
                    with
                    {
                        ExpiresAfterSequence = 0,
                    },
            ]);

        Assert.Equal(MatchStatus.Completed, result.Status);

        var expired = Assert.Single(M5TestData.Rejections(result));
        Assert.Equal(CommandRejectionReason.Expired, expired.Reason);

        // Suresi doldugu icin lineup DEGISTI.
        Assert.Equal(0, M5TestData.SubstitutionsApplied(result));
    }

    /// <summary>
    /// 07 §6 madde 4'ün <b>mac bitimi</b> yarısı: sunulan bir sınıra hiç
    /// ulaşamayan komut, maç terminal olduğunda <c>ExpandOnTerminal</c> ile
    /// sonlanır. <c>ExpireStaleSequence</c> farklıdır: o adım sonunda düşürür.
    /// </summary>
    [Fact]
    public void CommandsStillQueuedWhenTheMatchEndsAreExpired()
    {
        // Substitution SADECE dead-ball/period-break'te uygulanir. Ama
        // `M5TestData.Substitute` varsayılan olarak DeadBall hedefler ve
        // ilk adım PeriodBreak sunduğu için komut ilk adımda kuyruğa girer,
        // ikinci adımda ise ActionDecision sunulur — DeadBall sunulmaz.
        //
        // Bu yol "sunulmayan sınıra düşen komut"dur ve M6'da replay aracının
        // kullanacağı gerçek durumdur.
        var simulation = new MatchSimulation(M2TestData.Config());
        var setup = M2TestData.NeutralMirror();

        var command = M5TestData.Substitute(setup, boundary: CommandBoundary.DeadBall);

        var state = simulation.Create(setup);

        // 1. adim: PeriodBreak sunuluyor, DeadBall komutu kuyruga girer.
        var queued = simulation.Advance(state, [command]);
        Assert.Equal(1, queued.State.CommandQueue.PendingCount);

        // Terminal duruma kadar elle ilerle. `Advance` terminal state'te
        // `ExpireAll` calistirir (D94 yolu); boylece 07 §6 madde 4 gercekten
        // tetiklenir.
        var finished = queued.State;
        var guard = 0;

        while (!finished.IsTerminal && guard++ < 200_000)
        {
            finished = simulation.Advance(finished).State;
        }

        Assert.True(finished.IsTerminal, "Mac sonlanmadi.");

        // Kuyruk bos: komut ya bir dead-ball'da uygulandi ya da expire edildi.
        Assert.Equal(0, finished.CommandQueue.PendingCount);
    }

    [Fact]
    public void ExpiryAlsoCarriesAReasonCodeIntoTheEventStream()
    {
        var simulation = new MatchSimulation(M2TestData.Config());
        var setup = M2TestData.NeutralMirror();

        // Suresi dolmus bir komut: kuyruga girer, ilk adimin sonunda dusurulur
        // ve sebep kodu event'e yazilir.
        var result = simulation.Simulate(
            setup,
            [
                M5TestData.Substitute(setup, boundary: CommandBoundary.PeriodBreak)
                    with
                    {
                        ExpiresAfterSequence = 0,
                    },
            ]);

        var rejections = result.Events
            .Where(e => e.Type == MatchEventType.CommandRejected)
            .Select(e => e.PayloadAs<CommandRejectedPayload>())
            .ToList();

        Assert.NotEmpty(rejections);
        Assert.All(
            rejections,
            rejection => Assert.NotEqual(CommandRejectionReason.None, rejection.Reason));
    }

    [Fact]
    public void CommandRejectionDoesNotAbortTheMatch()
    {
        // 07 §5: reddetme maci bitirmez.
        var simulation = new MatchSimulation(M2TestData.Config());
        var setup = M2TestData.NeutralMirror();

        var result = simulation.Simulate(
            setup,
            [
                M5TestData.Substitute(setup, incoming: Guid.NewGuid()),
                M5TestData.ChangeOffense(),
            ]);

        Assert.Equal(MatchStatus.Completed, result.Status);
        Assert.NotEmpty(M5TestData.Rejections(result));
    }

    [Fact]
    public void ExpiredAfterSequenceIsHonoured()
    {
        var command = M5TestData.ChangeOffense() with { ExpiresAfterSequence = 10 };

        Assert.False(command.IsExpiredAfter(5));
        Assert.True(command.IsExpiredAfter(10));
        Assert.True(command.IsExpiredAfter(11));
    }

    [Fact]
    public void ExpirePastDropsOnlyExpiredCommands()
    {
        var keep = M5TestData.ChangeOffense() with
        {
            ExpiresAfterSequence = 1_000,
        };
        var drop = M5TestData.ChangeOffense() with { ExpiresAfterSequence = 5 };

        var (afterFirst, _) = CommandQueue.Empty.Accept([keep, drop], 1);
        Assert.Equal(2, afterFirst.PendingCount);

        // limit=5, sequence=50: 'drop' duser, limit=1000 olan 'keep' kalir.
        var (afterSecond, results) = afterFirst.ExpirePast(50);

        Assert.Equal(1, afterSecond.PendingCount);
        Assert.Equal(keep.CommandId, afterSecond.Pending[0].CommandId);

        var expired = Assert.Single(results);
        Assert.Equal(CommandRejectionReason.Expired, expired.Reason);
        Assert.Equal(drop.CommandId, expired.CommandId);
    }

    [Fact]
    public void SettledCommandsAreRemembered()
    {
        // Idempotency kaydi kuyruktan ayri tutulur; komut islendikten sonra da
        // yeniden gonderilirse reddedilir.
        var command = M5TestData.ChangeOffense();

        var (queued, _) = CommandQueue.Empty.Accept([command], 1);
        var settled = queued.Settle(queued.Pending);

        Assert.Empty(settled.Pending);
        Assert.Contains(command.CommandId, settled.Settled);

        // D93: Settle ayni zamanda KUYRUKTAN da cikarir. Onceki surum yalniz
        // Settled isaretliyordu; boylece komut hem "islendi" sayilip hem de
        // kuyrukta kaliyordu ve ikinci kez uygulanabiliyordu.
        Assert.Equal(0, settled.PendingCount);

        var (again, results) = settled.Accept([command], 2);
        Assert.Equal(CommandRejectionReason.DuplicateCommand, Assert.Single(results).Reason);
        Assert.Equal(0, again.PendingCount);
    }

    [Fact]
    public void TakeAtRemovesOnlyThatBoundary()
    {
        var tactic = M5TestData.ChangeOffense();
        var sub = M5TestData.Substitute(M2TestData.NeutralMirror());

        var (queued, _) = CommandQueue.Empty.Accept([tactic, sub], 1);
        Assert.Equal(2, queued.PendingCount);

        var (after, taken) = queued.TakeAt(CommandBoundary.ActionDecision);

        Assert.Equal(tactic.CommandId, Assert.Single(taken).CommandId);
        Assert.Equal(1, after.PendingCount);
        Assert.Equal(sub.CommandId, after.Pending[0].CommandId);
    }

    [Fact]
    public void EmptyQueueOperationsAreNoOps()
    {
        var queue = CommandQueue.Empty;

        Assert.True(queue.IsEmpty);
        Assert.Equal(0, queue.PendingCount);

        // Kimlik karsilastirmasi YAPILMAZ: D87 geregi ImmutableArray iceren
        // kayitlarda referans esitligi guvenilir degildir. Bunun yerine
        // gozlenebilir durum karsilastirilir.
        var (unchanged, taken) = queue.TakeAt(CommandBoundary.DeadBall);
        Assert.Equal(0, unchanged.PendingCount);
        Assert.Empty(taken);

        var (stillEmpty, expired) = unchanged.ExpireAll(99);
        Assert.Equal(0, stillEmpty.PendingCount);
        Assert.Empty(expired);

        var untouched = stillEmpty.Settle([]);
        Assert.Equal(0, untouched.PendingCount);
        Assert.Empty(untouched.Settled);
    }
}
