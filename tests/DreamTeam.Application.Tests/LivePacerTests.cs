using DreamTeam.Application.Runner;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Replay;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7, D109: pacer testleri.
///
/// <para><b>Bunlar SAHTE saat ve SAHTE bekleme ile yazilir</b>; gercek duvar
/// saati hic cagrilmaz. Boylece testler hizlidir ve <b>belirlenimcidir</b>.</para>
/// </summary>
public class LivePacerTests
{
    [Fact]
    public void TheSpeedupIsSixForAnEightMinuteMatch()
    {
        // 48 simule dakika / 8 gercek dakika. D109.
        var pacer = new LivePacer(new ManualClock(), new RecordingDelay());

        Assert.Equal(6.0, pacer.Speedup);
    }

    [Fact]
    public async Task PacerSleepsWhenTheEngineIsBehindSchedule()
    {
        // Cap genis tutulur ki temel formul olculuyor olsun; cap'in kendisi
        // asagida ayrica olculuyor.
        var clock = new ManualClock();
        var delay = new RecordingDelay();
        var pacer = new LivePacer(
            clock, delay, new LivePacerOptions { Speedup = 6.0, MaxSingleDelayMs = 60_000 });

        clock.Advance(1_000);

        // Motor 3 saniyelik ilerleme yapti; hedef 6 saniye. Beklenen 3 sn.
        var waited = await pacer.WaitAfterStepAsync(3_000, CancellationToken.None);

        Assert.Equal(3_000, waited);
        Assert.Equal(3_000, Assert.Single(delay.Waits));
    }

    [Fact]
    public async Task PacerNeverSleepsWhenTheEngineIsBehind()
    {
        var clock = new ManualClock();
        var delay = new RecordingDelay();
        var pacer = new LivePacer(clock, delay);

        clock.Advance(1_000);

        // Motor 9 saniyelik ilerleme yapti; hedef 6 saniye. Motor ONDE.
        var waited = await pacer.WaitAfterStepAsync(9_000, CancellationToken.None);

        Assert.Equal(0, waited);
        Assert.Empty(delay.Waits);
    }

    [Fact]
    public async Task PacerDoesNotSleepWhenExactlyOnTarget()
    {
        var clock = new ManualClock();
        var delay = new RecordingDelay();
        var pacer = new LivePacer(clock, delay);

        clock.Advance(1_000);

        var waited = await pacer.WaitAfterStepAsync(6_000, CancellationToken.None);

        Assert.Equal(0, waited);
    }

    [Fact]
    public async Task ASingleWaitIsCappedSoOneLongStepCannotBlockTheMatch()
    {
        var clock = new ManualClock();
        var delay = new RecordingDelay();
        var pacer = new LivePacer(
            clock, delay, new LivePacerOptions { Speedup = 6.0, MaxSingleDelayMs = 500 });

        clock.Advance(10_000);

        // Hedef 60 sn, motor 0'da. Tek bekleme 500 ms ile sinirli.
        var waited = await pacer.WaitAfterStepAsync(0, CancellationToken.None);

        Assert.Equal(500, waited);
    }

    [Fact]
    public async Task LagIsCarriedForwardRatherThanLost()
    {
        // Kritik ozellik: motor geride kaldiginda bostaki fark SILINMEZ, sonraki
        // adimda yakalanir. Aksi halde mac 8 dakikayi asardi.
        //
        // Cap bu testte kaldirilir; "fark korunuyor" iddiasi ancak tam farkin
        // istenebildigi durumda olculur. Cap'li davranis bir onceki testte
        // ayrica olculuyor.
        var clock = new ManualClock();
        var delay = new RecordingDelay();
        var pacer = new LivePacer(
            clock, delay, new LivePacerOptions { Speedup = 6.0, MaxSingleDelayMs = 60_000 });

        clock.Advance(1_000);

        // 1 sn duvar saati -> hedef 6 sn simule. Motor 6 sn'de yani TAM
        // hedefte: hic bekleme, hicbir sey kaybolmaz.
        Assert.Equal(0, await pacer.WaitAfterStepAsync(6_000, CancellationToken.None));

        // Motor ayni yerde (6 sn) ama hedef ilerledi: motor ARTIK geride,
        // birikmis fark 6 sn.
        clock.Advance(1_000);
        var caught = await pacer.WaitAfterStepAsync(6_000, CancellationToken.None);

        Assert.Equal(6_000, caught);
    }

    [Fact]
    public async Task TotalSimulatedTimeNeverExceedsTheWallClockBudget()
    {
        // 8 dakikalik butce asilir mi? Bir motor adiminin sureye bakmadan
        // ilerlemesi butceyi asabilirdi.
        var clock = new ManualClock();
        var pacer = new LivePacer(clock, new RecordingDelay());
        var simulated = 0L;

        for (var step = 0; step < 20; step++)
        {
            clock.Advance(400);
            simulated += 200;                       // motor duvar saatinden yavas
            await pacer.WaitAfterStepAsync(simulated, CancellationToken.None);
        }

        Assert.False(pacer.ExceededBudget());
    }

    [Fact]
    public void ANonPositiveSpeedupIsRefused()
    {
        // Sifir bolme hatasi uretmemek icin gecersiz carpan sessizce kabul edilmez.
        Assert.Throws<ArgumentOutOfRangeException>(() => new LivePacer(
            new ManualClock(), new RecordingDelay(), new LivePacerOptions { Speedup = 0 }));
    }
}

/// <summary>
/// M7'nin EN KRITIK testi: duvar saati eslemesi domain sonucunu degistirmez.
///
/// <para><b>Neden?</b> Pacer yalnizca <c>Advance</c> dondukten SONRA bekler.
/// Motorun girdisi degismez. Bu test bunu olcer; "pacer zararsizdir" bir
/// varsayim degil, olcumdur.</para>
/// </summary>
public class LiveEquivalenceTests
{
    [Fact]
    public async Task ALiveMatchProducesTheSameEventsAsSimulate()
    {
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 4242);

        // Yol 1: motorun kendi yolu (pacer YOK).
        var engine = new MatchSimulation(config);
        var expected = engine.Simulate(setup);
        var expectedPrint = MatchStateFingerprint.OfEvents(expected.Events);

        // Yol 2: canli yurutme, gercek pacer algoritmasi, duvar saati motoru
        // YAVASLATSIN diye her adimda 4 sn ilerleyen sahte saat.
        var clock = new ReadAdvancingClock(millisecondsPerRead: 4_000);
        var delay = new RecordingDelay();
        var pacer = new LivePacer(clock, delay);

        using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);
        var live = await session.RunLiveAsync(pacer, CancellationToken.None);

        // Bekleme gercekten olustu; yoksa test hicbir sey olcmezdi.
        Assert.True(delay.WaitCount > 0, "Test anlamli olmali: bekleme olusmadi.");

        Assert.Equal(expected.Status, live.Status);
        Assert.Equal(expected.HomeScore, live.HomeScore);
        Assert.Equal(expected.AwayScore, live.AwayScore);
        Assert.Equal(expected.ElapsedGameTimeMs, live.ElapsedGameTimeMs);
        Assert.Equal(expected.PeriodsPlayed, live.PeriodsPlayed);

        // ASIL KANIT: event akisi bayt bayt ayni.
        Assert.Equal(expectedPrint, MatchStateFingerprint.OfEvents(live.Events));
    }

    [Fact]
    public async Task LiveAndOfflineAgreeAcrossManySeeds()
    {
        var config = M7TestData.Config();

        foreach (var seed in new ulong[] { 1, 2, 3, 99, 20260927 })
        {
            var setup = M7TestData.Mirror(seed);
            var expected = new MatchSimulation(config).Simulate(setup);

            using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);
            var live = await session.RunLiveAsync(
                new LivePacer(new ReadAdvancingClock(4_000), new RecordingDelay()),
                CancellationToken.None);

            Assert.Equal(
                MatchStateFingerprint.OfEvents(expected.Events),
                MatchStateFingerprint.OfEvents(live.Events));
        }
    }

    [Fact]
    public async Task PacerConsumesNoRng()
    {
        // M4/M5 sozlesmesi: pacer bir cekilis tuketmez.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 777);

        using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);
        var live = await session.RunLiveAsync(
            new LivePacer(new ManualClock(), new RecordingDelay()),
            CancellationToken.None);

        var offline = new MatchSimulation(config).Simulate(setup);

        Assert.Equal(offline.Events.Length, live.Events.Length);
    }
}
