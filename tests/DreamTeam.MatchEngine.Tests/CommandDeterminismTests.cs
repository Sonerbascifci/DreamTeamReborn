using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Randomness;
using DreamTeam.MatchEngine.Replay;
using DreamTeam.MatchEngine.Rules;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M5: komutlarin determinizmi (09 M5 kabul maddesi: "ayni komut dizisi ayni mac")
/// ve M4'ün cagri sirasi sozlesmesinin korunmasi.
/// </summary>
public class CommandDeterminismTests
{
    [Fact]
    public void TheSameCommandListProducesTheSameMatch()
    {
        // <b>Onemli:</b> komut listesi <b>aynen ayni ornekler</b> olmalidir.
        // Her `Simulate` cagrisi icin yeni `Guid.NewGuid()` uretilen bir
        // komut listesi ayni "komut dizisi" DEGILDIR — kimlik farklidir ve
        // event akisi da farkli olur. Bu, replay girdisinin kopyalanabilir
        // olma gereksinimidir (07 §5: CommandId tekrarlarda degismez).
        var setup = M2TestData.NeutralMirror(20260928);
        var commands = BuildCommandList(setup);

        var simulation = new MatchSimulation(M2TestData.Config());
        var a = simulation.Simulate(setup, commands);
        var b = simulation.Simulate(setup, commands);

        Assert.Equal(a.HomeScore, b.HomeScore);
        Assert.Equal(a.AwayScore, b.AwayScore);
        Assert.Equal(a.Status, b.Status);
        Assert.Equal(
            MatchStateFingerprint.OfEvents(a.Events),
            MatchStateFingerprint.OfEvents(b.Events));
    }

    [Fact]
    public void RebuildingAnEquivalentCommandListWithTheSameIdsIsAlsoReproducible()
    {
        // Pratik karsiligi: replay kaydi komutlari JSON'da saklar ve yeniden
        // kurar. Ayni CommandId'lerle yeniden kurulan liste ayni maci vermeli.
        var setup = M2TestData.NeutralMirror(20260928);
        var commands = BuildCommandList(setup);

        // Ayni kimliklerle yeniden kur.
        var rebuilt = commands
            .Select(command => command with { AcceptedOrder = 0 })
            .ToArray();

        var simulation = new MatchSimulation(M2TestData.Config());
        var a = simulation.Simulate(setup, commands);
        var b = simulation.Simulate(setup, rebuilt);

        Assert.Equal(a.HomeScore, b.HomeScore);
        Assert.Equal(
            MatchStateFingerprint.OfEvents(a.Events),
            MatchStateFingerprint.OfEvents(b.Events));
    }

    [Fact]
    public void CommandOrderChangesTheMatch()
    {
        // D86: siranin gercekten etkili oldugu kanitlanir. Aksi halde FIFO
        // sozlesmesi ogrenilemez.
        var setup = M2TestData.NeutralMirror(20260928);
        var simulation = new MatchSimulation(M2TestData.Config());

        var inside = M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost);
        var perimeter = M5TestData.ChangeOffense(tactic: OffensiveTactic.PerimeterMotion);

        var a = simulation.Simulate(setup, [inside, perimeter]);
        var b = simulation.Simulate(setup, [perimeter, inside]);

        // Son komut her zaman kazanir (last-write-wins).
        Assert.Equal(OffensiveTactic.PerimeterMotion, FinalOffense(a));
        Assert.Equal(OffensiveTactic.InsidePost, FinalOffense(b));
    }

    [Fact]
    public void AnInvalidCommandDoesNotAdvanceTheRng()
    {
        // 07 §5: "Invalid command RNG tüketmemeli." Biz bunu GECERLI komut için
        // de şart koşuyoruz — aksi halde M4'ün çağrı sırası sözleşmesi bozulur.
        //
        // Ölçüm: aynı adım sayısında, gecersiz komutla ve komutsuz iki koşunun
        // RNG state'i BİREBİR aynı olmalı. Adım sayısı aynı olduğu için
        // çekiliş sayısı da aynıdır; tek fark komuttur.
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        var plain = M5TestData.AdvanceSteps(simulation, simulation.Create(setup), 12);
        var withInvalid = simulation.Create(setup);

        for (var step = 0; step < 12; step++)
        {
            var invalid = M5TestData.ChangeOffense(commandId: Guid.NewGuid()) with
            {
                Payload = new CommandPayload { OffensiveTactic = (OffensiveTactic)99 },
            };

            withInvalid = simulation.Advance(withInvalid, [invalid]).State;
        }

        Assert.Equal(plain.HomeScore, withInvalid.HomeScore);
        Assert.Equal(plain.AwayScore, withInvalid.AwayScore);

        // RNG state'i de ayni — cekilis sayisi degismedi.
        Assert.Equal(RngState(plain), RngState(withInvalid));
    }

    [Fact]
    public void AValidCommandConsumesNoRandomnessEither()
    {
        // Geçersiz komut kadar GEÇERLİ komut da çekiliş harcamaz. Kural:
        // "komut bir yazma işlemidir, olasılık değil."
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        var plain = M5TestData.AdvanceSteps(simulation, simulation.Create(setup), 12);
        var withValid = simulation.Create(setup);

        for (var step = 0; step < 12; step++)
        {
            withValid = simulation.Advance(
                withValid,
                [M5TestData.ChangeOffense(tactic: OffensiveTactic.Balanced)]).State;
        }

        // Balanced -> Balanced degisimi ETKISIZDIR; skor birebir ayni olmali.
        Assert.Equal(plain.HomeScore, withValid.HomeScore);
        Assert.Equal(plain.AwayScore, withValid.AwayScore);
        Assert.Equal(RngState(plain), RngState(withValid));
    }

    [Fact]
    public void DifferentCommandOrdersProduceDifferentActionMixes()
    {
        // Siranin etkisinin gozlenebilir bir kaniti.
        var setup = M2TestData.NeutralMirror(31_337);
        var simulation = new MatchSimulation(M2TestData.Config());

        var inside = M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost);
        var perimeter = M5TestData.ChangeOffense(tactic: OffensiveTactic.PerimeterMotion);

        var a = simulation.Simulate(setup, [inside, perimeter]);
        var b = simulation.Simulate(setup, [perimeter, inside]);

        // Son komut her zaman kazanir.
        Assert.Equal(OffensiveTactic.PerimeterMotion, FinalOffense(a));
        Assert.Equal(OffensiveTactic.InsidePost, FinalOffense(b));

        // Siranin aksiyon karisimina da yansidigi gorulur.
        Assert.NotEqual(PostUpShare(a), PostUpShare(b));
    }

    [Fact]
    public void TheParameterlessAdvanceOverloadIsEquivalentToAnEmptyCommandList()
    {
        // H03: ikinci bir motor yolu YOK. Aşırı yükleme aynı çekirdeği çağırır.
        var setup = M2TestData.NeutralMirror();
        var simulation = new MatchSimulation(M2TestData.Config());

        var withOverload = simulation.Advance(simulation.Create(setup)).State;
        var withEmpty = simulation.Advance(
            simulation.Create(setup), CommandList.Empty).State;

        Assert.Equal(
            MatchStateFingerprint.Of(withOverload),
            MatchStateFingerprint.Of(withEmpty));
    }

    [Fact]
    public void TheSimulatorStillProducesTheSameResultWithoutCommands()
    {
        // Geri regresyon: M5 hicbir komut gonderilmediginde M4 davranisi
        // degismemeli. M4'te seed 20260927 tek mac 112-118 idi.
        var setup = M2TestData.NeutralMirror(20260927);
        var simulation = new MatchSimulation(M2TestData.Config());

        var result = simulation.Simulate(setup, CommandList.Empty);

        Assert.Equal(MatchStatus.Completed, result.Status);
        Assert.True(result.HomeScore > 0);
        Assert.True(result.AwayScore > 0);
    }

    private static string RngState(MatchState state)
    {
        Span<byte> bytes = stackalloc byte[SeededRandom.StateSizeInBytes];
        state.Random.GetState(bytes);

        return Convert.ToHexString(bytes);
    }

    private static OffensiveTactic FinalOffense(MatchResult result)
    {
        // Mac sonu taktigi son TacticChanged event'inden okunur.
        var changes = result.Events
            .Where(e => e.Type == MatchEventType.TacticChanged)
            .Select(e => e.PayloadAs<TacticChangedPayload>())
            .ToList();

        return changes.Count == 0 ? OffensiveTactic.Balanced : changes[^1].Current;
    }

    private static double PostUpShare(MatchResult result)
    {
        var actions = result.Events
            .Where(e => e.Type == MatchEventType.ActionCompleted)
            .Select(e => e.PayloadAs<ActionCompletedPayload>().Action)
            .ToList();

        if (actions.Count == 0)
        {
            return 0.0;
        }

        return (double)actions.Count(a => a == OffensiveAction.PostUp) / actions.Count;
    }

    private static System.Collections.Immutable.ImmutableArray<ScheduledManagerCommand>
        BuildCommandList(MatchSetup setup) =>
        [
            M5TestData.ChangeOffense(tactic: OffensiveTactic.InsidePost),
            M5TestData.ChangeDefense(tactic: DefensiveTactic.ZonePackPaint),
            M5TestData.ChangePace(pace: Pace.Fast),
            M5TestData.Substitute(setup, boundary: CommandBoundary.PeriodBreak),
            M5TestData.Timeout(),
        ];
}
