using System.Text.Json;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.UseCases;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7, 07 §8: yeniden baglanma ve mesaj sirasi.
///
/// <para><b>Atomiklik KURALI:</b> "Snapshot ile event'lerin ortusme/bosluk
/// siniri ATOMIK tanimlanir." Yanit, <i>tek bir kilitli okumanin</i> sonucu
/// olmalidir. Iki ayri okuma yapilsaydi araya yeni bir adimin event'i girer ve
/// istemci hicbir bosluk gormeden kayardi. Ispat: asagidaki eszamanli okuma
/// testi.</para>
///
/// <para><b>Neden snapshot gerekli?</b> Cunku event araligi sinirli olsa bile
/// cok uzun bir macin tamamini tek istekte tasimak makul degil. Snapshot
/// "buradan devam et" noktasidir ve <c>Sequence</c>'i ile birlikte gelir.</para>
/// </summary>
public class ReconnectTests
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task AReconnectAfterTheLastEventReturnsNothingAndTheSameSequence()
    {
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 1234);

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);
        await session.StepAsync(CancellationToken.None);

        var first = await session.CaptureForAsync(0, includeSnapshot: false, CancellationToken.None);

        Assert.NotEmpty(first.Events);

        // Istemci HEPESINI ALDIGINI soyluyor.
        var second = await session.CaptureForAsync(first.CurrentSequence, false, CancellationToken.None);

        Assert.Empty(second.Events);
        Assert.Equal(first.CurrentSequence, second.CurrentSequence);
    }

    [Fact]
    public async Task AReconnectWithASnapshotReturnsEventsStrictlyAfterTheSnapshot()
    {
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 1235);

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);

        for (var i = 0; i < 5; i++)
        {
            await session.StepAsync(CancellationToken.None);
        }

        var capture = await session.CaptureForAsync(0, includeSnapshot: true, CancellationToken.None);

        Assert.NotNull(capture.SnapshotJson);

        // ASIL KURAL: snapshot'in sequence'indan sonraki eventler gelir.
        // Ortusme (overlap) ve bosluk (gap) YOK.
        Assert.Equal(capture.CurrentSequence, capture.SnapshotSequence);
        Assert.All(capture.Events, e => Assert.True(e.Sequence > capture.SnapshotSequence));

        // Istege bagli snapshot istenmediyse sifirdan gelir.
        var withoutSnapshot = await session.CaptureForAsync(0, false, CancellationToken.None);
        Assert.Equal(-1, withoutSnapshot.SnapshotSequence);
        Assert.Null(withoutSnapshot.SnapshotJson);
    }

    [Fact]
    public async Task ASequenceFromTheFutureIsRejectedRatherThanSilentlyIgnored()
    {
        // Istemci bize olmadik bir sequence soyluyor. Sessizce bos donmek
        // onu sonsuza kadar bekletir; hata vermek dogru davranis.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 1236);

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);
        await session.StepAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.CaptureForAsync(999_999, false, CancellationToken.None));
    }

    [Fact]
    public async Task TheSnapshotIsValidJsonThatCarriesTheSequence()
    {
        // 07 §8: istemci snapshot'i cozebilmeli ve sequence'i ornebilmeli.
        // Aksi halde "bu snapshot'tan sonra ne var" sorusu cevapsiz kalir.
        //
        // DIKKAT: motorun serializer'i PascalCase uretir
        // (PropertyNamingPolicy = null, M5 D88) ve sequence'i
        // <c>StateSequence</c> adiyla tasir. Test bu GERCEK sozlesmeye
        // gore yazilmistir; client'in ayri bir bicim varsaymamasi icin
        // API katmani bunu donusturmez, oldugu gibi sunar.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 1237);

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);
        await session.StepAsync(CancellationToken.None);

        var capture = await session.CaptureForAsync(0, true, CancellationToken.None);

        using var document = JsonDocument.Parse(capture.SnapshotJson!);
        var root = document.RootElement;

        Assert.True(
            root.TryGetProperty("StateSequence", out var sequence),
            "Snapshot StateSequence icermeli.");

        // MOTORUN "SONRAKI" ISARETCISI. Bizim sinirimiz "son teslim edilen"
        // oldugu icin arada tam 1 fark vardir; bu fark M5 formatinda
        // belgelenmistir ve API katmaninda tek yerde hesaplanir.
        Assert.Equal(capture.RawSnapshotNextSequence, sequence.GetInt64());
        Assert.Equal(capture.SnapshotSequence + 1, sequence.GetInt64());

        // Config hash, engine/rules surumu ve RNG durumu tasinir: 03
        // "Surum ve tekr uretilebilirlik".
        Assert.Equal(capture.ConfigHash, root.GetProperty("ConfigHash").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("EngineVersion").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("RulesVersion").GetString()));

        // RNG durumu Base64 metin olarak tasinir (ImmutableArrayByteConverter).
        // BOS OLMAMALI: 03 "Surum ve tekr uretilebilirlik" bunu ister.
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("RandomState").GetString()));

        // Geri okunabilir: snapshot tekrar cozulebilir.
        var roundTrip = DreamTeam.MatchEngine.Replay.MatchSnapshotSerializer.FromJson(capture.SnapshotJson!);
        Assert.Equal(capture.RawSnapshotNextSequence, roundTrip.StateSequence);
    }

    [Fact]
    public async Task ConcurrentCapturesNeverSeeAGapOrAnOverlap()
    {
        // CANLI KARSILIK. 2000 kez: bir yandan adim atiyoruz, bir yandan
        // reconnect yaniti aliyoruz. Her yanitta su invariants KIRILMALIDIR:
        //
        //   1) Snapshot sequence'i, donen eventlerin EN KUCUK sequence'ine
        //      esit veya buyuk olmali (ortusme yok).
        //   2) Dönen eventler ardisik olmali (bosluk yok).
        //   3) Donen son sequence, sunucudaki son sequence'yi ASMAMALI.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 1238);

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);

        var stepper = Task.Run(async () =>
        {
            while (!session.IsTerminal)
            {
                await session.StepAsync(CancellationToken.None);
            }
        });

        var failures = new List<string>();
        var lastSeen = 0L;

        while (!session.IsTerminal)
        {
            var from = lastSeen;
            var capture = await session.CaptureForAsync(from, includeSnapshot: true, CancellationToken.None);

            if (capture.Events.Length == 0)
            {
                if (capture.SnapshotSequence < from)
                {
                    failures.Add(
                        $"Snapshot ({capture.SnapshotSequence}) istemcinin konumundan "
                        + $"({from}) geride; istemci eskiye sarilir.");
                }
            }
            else
            {
                // 1) ISTEK: from'dan SONRA. Ilk event from'dan buyuk olmali.
                if (capture.Events[0].Sequence <= from)
                {
                    failures.Add(
                        $"Ortusme: istemci {from}'da idi ama {capture.Events[0].Sequence} "
                        + "tekrar gonderildi.");
                }

                // 2) ARDISIK.
                for (var i = 1; i < capture.Events.Length; i++)
                {
                    if (capture.Events[i].Sequence != capture.Events[i - 1].Sequence + 1)
                    {
                        failures.Add(
                            $"Bosluk: {capture.Events[i - 1].Sequence} sonrasi "
                            + $"{capture.Events[i].Sequence} geldi.");
                        break;
                    }
                }

                // Snapshot ile eventler arasi bosluk olmamali.
                if (capture.SnapshotSequence >= 0
                    && capture.Events[0].Sequence != capture.SnapshotSequence + 1)
                {
                    failures.Add(
                        $"Snapshot ({capture.SnapshotSequence}) ile ilk event "
                        + $"({capture.Events[0].Sequence}) arasinda bosluk var.");
                }
            }

            if (capture.CurrentSequence < from)
            {
                failures.Add(
                    $"Sunucu geri gitti: istemci {from}, sunucu {capture.CurrentSequence}.");
            }

            lastSeen = capture.CurrentSequence;
        }

        await stepper;

        Assert.Empty(failures);
    }
}

/// <summary>
/// M7, D113: sunucu yeniden baslarsa mac olmez, <b>iptal edilir</b>.
///
/// <para><b>Bu bir kurtarma testi DEGILDIR.</b> M7'de kurtarma yoktur ve
/// test de olmadigini dogrular. Onay sunucu yeniden basladiginda:</para>
/// <list type="number">
///   <item><description>Bellekteki oturumlar kapatilir.</description></item>
///   <item><description><c>Running</c> durumundaki kalici maclar
///   <c>Aborted</c> olur.</description></item>
///   <item><description>Sebep YAZILIR (neden bitti belli olmali).</description></item>
///   <item><description>Odul VERILMEZ.</description></item>
/// </list>
/// </summary>
public class ServerRestartTests
{
    [Fact]
    public async Task ARestartAbortsRunningMatchesAndRecordsTheReason()
    {
        var matches = new InMemoryMatchRepository();
        var sessions = new MatchSessionStore();

        var config = M7TestData.Config();
        var home = M7TestData.Mirror(seed: 1);
        var away = M7TestData.Mirror(seed: 2);

        sessions.GetOrAdd(home.MatchId, () => new MatchSession(home.MatchId, Guid.NewGuid(), home, config));
        sessions.GetOrAdd(away.MatchId, () => new MatchSession(away.MatchId, Guid.NewGuid(), away, config));

        matches.Seed(Running(home.MatchId));
        matches.Seed(Running(away.MatchId));

        var completed = Running(Guid.NewGuid()) with { Lifecycle = MatchLifecycle.Completed };
        matches.Seed(completed);

        var abort = new AbortOrphanedMatches(sessions, matches);
        var affected = await abort.ExecuteAsync(CancellationToken.None);

        // Iki oturum + iki kalici mac.
        Assert.Equal(4, affected);

        // Oturumlar KAPANDI: yeni komut kabul edilmez.
        Assert.Equal(0, sessions.Count);

        // Tamamlanmis mac DOKUNULMAZ.
        var finished = await matches.FindAsync(completed.Id, CancellationToken.None);
        Assert.Equal(MatchLifecycle.Completed, finished!.Lifecycle);

        // Sebep yazildi.
        var aborted = await matches.FindAsync(home.MatchId, CancellationToken.None);
        Assert.Equal(MatchLifecycle.Aborted, aborted!.Lifecycle);
        Assert.False(string.IsNullOrWhiteSpace(aborted.AbortReason));
        Assert.Contains("yeniden basladi", aborted.AbortReason, StringComparison.OrdinalIgnoreCase);

        // IYILESTIRME YOK: oturum yeniden olusmaz.
        Assert.Null(sessions.Find(home.MatchId));
    }

    [Fact]
    public async Task ASecondRestartIsHarmless()
    {
        // Idempotency: yeniden baslatma iki kez calissa da sonuc aynidir.
        var matches = new InMemoryMatchRepository();
        var sessions = new MatchSessionStore();
        var abort = new AbortOrphanedMatches(sessions, matches);

        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 3);
        matches.Seed(Running(setup.MatchId));
        sessions.GetOrAdd(setup.MatchId, () => new MatchSession(setup.MatchId, Guid.NewGuid(), setup, config));

        Assert.Equal(2, await abort.ExecuteAsync(CancellationToken.None));
        Assert.Equal(0, await abort.ExecuteAsync(CancellationToken.None));
    }

    private static MatchRecord Running(Guid id) => new(
        id,
        Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
        MatchLifecycle.Running,
        1,
        "hash",
        "engine",
        "rules",
        "digest",
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UnixEpoch,
        null,
        null,
        null);
}
