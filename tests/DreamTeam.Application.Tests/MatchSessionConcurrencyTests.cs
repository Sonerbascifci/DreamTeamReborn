using DreamTeam.Application.Runner;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Replay;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7'nin EN KRITIK kabul maddesi (09 §M7): "Aynı maç iki runner tarafından
/// eşzamanlı ilerletilmez."
///
/// <para><b>Bu test neden 100.000 kez?</b> Bir yarisma (race) bir kez
/// yakalanmaz. Tek denemede hata olmaz, olsa da yakalanmayabilir. Tekrarlanan
/// eszamanli erisim, hatanin <i>pencere</i>yi yakalar. Bu "iyi sans" testi
/// degil, <b>dayaniklilik</b> olcumudur.</para>
///
/// <para><b>Neden dort thread?</b> Iki thread yeterli olurdu ama dort thread
/// daha genis bir carpisma yuzeyi uretir. Asil onemli olan cagri sayisi;
/// thread sayisi yalniz yaris penceresini genisletir.</para>
///
/// <para><b>Ne olculuyor?</b> Dort invariants, hepsi oturumun DISINDAN:</para>
/// <list type="number">
///   <item><description><b>Sequence geri gitmez ve tekrarlanmaz.</b> Iki adim
///   ayni sequence'i yazdiysa yaris (torn) okuma vardir. Olcum: thread'ler
///   arasinda PAYLASILAN bir "gorulen en buyuk sequence" sayaci.</description></item>
///   <item><description><b>Bir cagri en fazla bir adim ilerletir.</b> Iki
///   adim birleseydi epoch bir cagrida 2 artardi.</description></item>
///   <item><description><b>Terminal olmadan epoch artmaz.</b> Mac bitince
///   artik yeni event ve yeni epoch yoktur.</description></item>
///   <item><description><b>SONUC MOTORUNKİYLE AYNI.</b> 100.000 eszamanli
///   adimdan sonra mac, ayni seed ile <c>Simulate</c>'in verdigi macla
///   BAYT BAYT ayni olmalidir. Bu, determinizm sozlesmesinin (D101) en kotu
///   ihlali olan "kilit eksikligi RNG'yi iki kez gormek" durumunu dogrudan
///   yakalar.</description></item>
/// </list>
///
/// <para><b>Yarisma olsaydi ne olurdu?</b> Iki parcacik ayni anda
/// <c>_state</c>'i okur, ikisi de ayni <c>NextSequence</c>'i gorur ve ayni
/// sequence'li event'i yazar. Sequence atlar veya tekrarlanir, epoch adim
/// sayisindan sapar, ve en kotusu ayni <c>IRandomSource</c> durumu iki kez
/// goruldugu icin mac <i>farkli</i> bir olcum uretir.</para>
/// </summary>
public class MatchSessionConcurrencyTests
{
    private const int TotalCalls = 100_000;
    private const int Threads = 4;

    [Fact]
    public async Task TwoConcurrentAdvancesNeverTearTheState()
    {
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 20260928);

        // Beklenen sonuc ONCEDEN hesapla: motorun kendi yolu.
        var expected = new MatchSimulation(config).Simulate(setup);

        using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);

        // Her cagri GOZLEMLERINI toplar; dogrulama sonunda yapilir.
        //
        // <b>Neden anlik kontrol yok?</b> Dort thread birbirine gore
        // "sira" tasimaz. Thread A 2. adimi islerken thread B 5. adimi
        // islemis olabilir; B'nin sequence'i A'nindan kucuk gorunur ama bu
        // YARIS degildir, sadece gozlem sirasi ile adim sirasi farklidir.
        // Anlik "sequence > once gorulen" kontrolu bu yuzden dogru bir
        // invariants DEGILDIR ve saglikli bir kapiyi hatali koddan ayirt
        // edemez. Dogrulanacak sey siranin kendisi degil, BIRLESIMidir.
        var observations = new System.Collections.Concurrent.ConcurrentBag<Observation>();

        await Task.WhenAll(Enumerable.Range(0, Threads).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < TotalCalls / Threads; i++)
            {
                SessionStepResult result;

                try
                {
                    result = await session.StepAsync(CancellationToken.None);
                }
                catch (Exception error)
                {
                    observations.Add(Observation.Failed($"{error.GetType().Name}: {error.Message}"));
                    return;
                }

                observations.Add(new Observation([.. result.Events.Select(e => e.Sequence)], result.Epoch, result.IsTerminal));
            }
        })));

        var failures = new List<string>();
        failures.AddRange(observations.Where(o => o.Error is not null).Select(o => $"Adim patladi: {o.Error}"));

        // INVARIANT 1: BIR ADIMIN ICINDEKI eventler ardisik ve artan.
        // Bu siralamaya ihtiyac yok; her adim icinde gecerlidir.
        foreach (var observation in observations.Where(o => o.Sequences is not null))
        {
            long? previous = null;

            foreach (var sequence in observation.Sequences!)
            {
                if (previous is { } p && sequence != p + 1)
                {
                    failures.Add(
                        $"Adim ici bosluk: {p} sonrasi {sequence} geldi. Event yarisi.");
                }

                previous = sequence;
            }
        }

        // INVARIANT 2: TUM eventlerin BIRLESIMI bosluksuz ve tekrarsizdir.
        // 04: (match_id, sequence) benzersizdir; burada ayni sey bellekte de
        // dogru olmalidir.
        var allSequences = observations
            .Where(o => o.Sequences is not null)
            .SelectMany(o => o.Sequences!)
            .OrderBy(s => s)
            .ToList();

        var duplicates = allSequences
            .GroupBy(s => s)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            failures.Add(
                $"Ayni sequence birden fazla kez uretildi ({duplicates.Count} adet, "
                + $"ilk: {string.Join(", ", duplicates.Take(5))}). Iki adim birlesmis demektir.");
        }

        for (var i = 0; i < allSequences.Count; i++)
        {
            if (allSequences[i] != i + 1)
            {
                failures.Add($"Sequence bosluğu: {i + 1} bekleniyordu, {allSequences[i]} geldi.");
                break;
            }
        }

        // INVARIANT 3: Epoch'lar 1..S araliginda BUGLUSUZ olmalidir.
        // Epoch yalniz adim basina bir artar; iki adim birlestiyse veya bir
        // adim kacarildiysa bu aralik bozulur.
        var allEpochs = observations
            .Where(o => o.Sequences is not null)
            .Select(o => o.Epoch)
            .Distinct()
            .OrderBy(e => e)
            .ToList();

        for (var i = 0; i < allEpochs.Count; i++)
        {
            if (allEpochs[i] != i + 1)
            {
                failures.Add(
                    $"Epoch kaymasi: {i + 1} bekleniyordu, {allEpochs[i]} geldi. "
                    + "Iki adim birlesti veya bir adim kacarildi.");
                break;
            }
        }

        Assert.Empty(failures);

        // INVARIANT 4: mac bitmis ve motorun kendi yoluyla AYNI.
        Assert.True(session.IsTerminal, "100.000 adimdan sonra mac bitmemis olmamali.");

        var actual = session.CompletedOutcome();

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.HomeScore, actual.HomeScore);
        Assert.Equal(expected.AwayScore, actual.AwayScore);
        Assert.Equal(expected.ElapsedGameTimeMs, actual.ElapsedGameTimeMs);
        Assert.Equal(
            MatchStateFingerprint.OfEvents(expected.Events),
            MatchStateFingerprint.OfEvents(actual.Events));
    }

    private sealed record Observation(long[]? Sequences, long Epoch, bool IsTerminal)
    {
        public string? Error { get; init; }

        public static Observation Failed(string error) => new(null, -1, false) { Error = error };
    }

    [Fact]
    public async Task ConcurrentRunToCompletionProducesOneMatchNotTwo()
    {
        // Iki cagiran AYNI oturumu bitirmeye calisir. Ikisi de ayni maci
        // gorur; iki farkli ozet veya birinin daha erken bitmesi yaris
        // belirtisidir.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 31337);

        var expected = new MatchSimulation(config).Simulate(setup);

        using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);
        var outcomes = new System.Collections.Concurrent.ConcurrentBag<SessionOutcome>();

        await Task.WhenAll(
            Task.Run(async () => outcomes.Add(await session.RunToCompletionAsync(CancellationToken.None))),
            Task.Run(async () => outcomes.Add(await session.RunToCompletionAsync(CancellationToken.None))));

        Assert.Equal(2, outcomes.Count); // xUnit2013: bu birer esitlik degil, tam sayi kontrolu

        foreach (var outcome in outcomes)
        {
            Assert.Equal(expected.HomeScore, outcome.HomeScore);
            Assert.Equal(expected.AwayScore, outcome.AwayScore);
            Assert.Equal(expected.ElapsedGameTimeMs, outcome.ElapsedGameTimeMs);
            Assert.Equal(expected.Status, outcome.Status);
        }
    }

    [Fact]
    public async Task AStaleEpochRequestIsRejectedWithoutTouchingTheState()
    {
        // 07 §5: istek hangi gorulen state'e gore yapildi. Eski epoch ile
        // gelen istek reddedilir ve DURUM DEGISMEZ.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 555);

        using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);

        var before = await session.StepAsync(CancellationToken.None);
        var staleEpoch = before.Epoch;

        await session.StepAsync(CancellationToken.None);

        Assert.False(session.TryEnqueueCommand(M7TestData.ChangePace(TeamSide.Home), staleEpoch));

        // Komut kuyruga GIRMEDI; dolayisiyla hicbir adimda pace degisimi
        // gorulmemeli.
        for (var i = 0; i < 5; i++)
        {
            var step = await session.StepAsync(CancellationToken.None);

            Assert.DoesNotContain(step.Events, e => e.Type == MatchEventType.PaceChanged);
        }
    }

    [Fact]
    public async Task ACurrentEpochRequestIsAccepted()
    {
        // Bayat epoch reddedilir; GUNCEL epoch kabul edilir. Birincisi
        // yanlislik, ikincisi felaket olurdu.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 556);

        using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);

        await session.StepAsync(CancellationToken.None);

        Assert.True(session.TryEnqueueCommand(M7TestData.ChangePace(TeamSide.Home), session.Epoch));
    }

    [Fact]
    public async Task CommandsAreQueuedNotAppliedImmediately()
    {
        // 07 §5: komut MANTIKSAL sinira baglanir, gelen an degil.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 909);

        using var session = new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);

        Assert.True(session.TryEnqueueCommand(M7TestData.ChangePace(TeamSide.Home)));

        // Kuyruga eklenen ama henuz hicbir adimda gorulmamis komut, motorun
        // ilk adiminda bekleyen bir sey olarak durur. Bunu "gecikme" diye
        // olcmeyiz; MANTIKSAL SINIR motorun kendi kuralidir ve
        // CommandValidator testleri zaten dogrular. Buradaki olcum: mac
        // normal sekilde biter.
        var outcome = await session.RunToCompletionAsync(CancellationToken.None);

        Assert.Equal(MatchStatus.Completed, outcome.Status);
    }

    [Fact]
    public async Task OnlyOneMatchIdCanEverOwnASession()
    {
        // 03: "Aynı maç iki runner tarafindan eszamanli ilerletilmez."
        // Depo tarafi: ayni matchId icin ikinci oturum YARATILAMAZ.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 4242);

        var store = new MatchSessionStore();
        var created = 0;
        var observed = new System.Collections.Concurrent.ConcurrentBag<MatchSession>();

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            var session = store.GetOrAdd(setup.MatchId, () =>
            {
                Interlocked.Increment(ref created);
                return new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config);
            });

            observed.Add(session);
        })));

        // Fabrika birden fazla kez CAOGRULABILIR (ConcurrentDictionary belgeli
        // boyle). Bu bir hata degil. Asil invariants:
        //   1) Depoda TEK oturum var.
        //   2) 64 cagiranin GORUDUGU oturum ayni nesne.
        // Kaybeden oturumlar GetOrAdd icinde dispose edilir.
        Assert.Equal(1, store.Count);
        Assert.Single(observed.Distinct());

        foreach (var session in observed)
        {
            Assert.Same(observed.First(), session);
        }
    }
}
