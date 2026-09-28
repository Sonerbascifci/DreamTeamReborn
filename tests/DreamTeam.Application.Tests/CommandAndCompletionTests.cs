using DreamTeam.Application.Ids;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Application.UseCases;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7, 07 §5: komut yolunun tamami.
///
/// <para><b>Sirasiyla olculenler:</b> sahiplik -> idempotency -> kuyruk.
/// Her biri ayri test; biri gecerken digeri sessizce calisip gecmiyor.</para>
/// </summary>
public class SendManagerCommandTests
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Stranger = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static (SendManagerCommand UseCase, MatchSessionStore Sessions, InMemoryCommandLogRepository Log, FixedCurrentUser Current)
        Build(Guid? ownerOverride = null, bool createSession = true)
    {
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 777);
        var sessions = new MatchSessionStore();
        var log = new InMemoryCommandLogRepository();
        var current = new FixedCurrentUser(ownerOverride ?? Owner);

        if (createSession)
        {
            sessions.GetOrAdd(setup.MatchId, () => new MatchSession(setup.MatchId, Owner, setup, config));
        }

        return (
            new SendManagerCommand(sessions, log, current, new ManualClock()),
            sessions,
            log,
            current);
    }

    [Fact]
    public async Task ACommandFromTheMatchOwnerIsAccepted()
    {
        var (useCase, sessions, log, _) = Build();
        var command = M7TestData.ChangePace(TeamSide.Home);

        var result = await useCase.ExecuteAsync(sessions.ActiveMatches[0], command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.Accepted);

        // 07 §5: "ACK = alindi/kuyruga girdi. Applied = state'e islendi.
        // Bu ikisini tek basari mesajinda karistirma."
        Assert.False(result.Value.Applied);
        Assert.True(log.Has(sessions.ActiveMatches[0], command.CommandId));
    }

    [Fact]
    public async Task ACommandFromAnotherUserIsRejectedAndNeverReachesTheEngine()
    {
        // T19'in kalbi: yetkisiz komut MOTORA GIRMEZ. Kanit, komut
        // kuyrugunun bos kalmasi.
        var (useCase, sessions, log, current) = Build();
        current.UserId = Stranger;

        var result = await useCase.ExecuteAsync(
            sessions.ActiveMatches[0], M7TestData.ChangePace(TeamSide.Home), CancellationToken.None);

        Assert.False(result.Succeeded);

        // Reddedilen komut LOGA DA YAZILMAZ: kayit defteri yalniz kabul
        // edilen komutlari tutar.
        Assert.False(log.Has(sessions.ActiveMatches[0], M7TestData.ChangePace(TeamSide.Home).CommandId));

        // Ve motor hicbir sey gormez.
        var session = sessions.Find(sessions.ActiveMatches[0])!;
        var step = await session.StepAsync(CancellationToken.None);

        Assert.DoesNotContain(step.Events, e => e.Type == MatchEventType.PaceChanged);
    }

    [Fact]
    public async Task TheSameCommandIdTwiceIsIdempotent()
    {
        // 07 §5: "Ayni CommandId yeniden gelirse ayni sonuc dondurulur."
        var (useCase, sessions, log, _) = Build();
        var matchId = sessions.ActiveMatches[0];
        var command = M7TestData.ChangePace(TeamSide.Home);

        var first = await useCase.ExecuteAsync(matchId, command, CancellationToken.None);
        var second = await useCase.ExecuteAsync(matchId, command, CancellationToken.None);

        Assert.True(first.Value!.Accepted);
        Assert.True(second.Value!.Accepted);

        // IKINCISI DE AYNI SONUCU DONDURUR; hata degil, ACK.
        Assert.Equal(first.Value.Accepted, second.Value.Accepted);

        // Kayit defteri denemesi iki kez yapildi ama IKINCISI yeni kayit
        // ACAMADI: UNIQUE (match_id, command_id) yazimi basarisiz oldu.
        // Tek bir kayit var.
        Assert.Single(log.CommandsFor(sessions.ActiveMatches[0]));
    }

    [Fact]
    public async Task ADifferentCommandIdIsASeparateCommand()
    {
        // Idempotency kimlige baglidir, icerige degil. Ayni türde ama
        // farkli CommandId olan ikinci komut ayri bir komuttur.
        var (useCase, sessions, _, _) = Build();
        var matchId = sessions.ActiveMatches[0];

        var first = await useCase.ExecuteAsync(matchId, M7TestData.ChangePace(TeamSide.Home), CancellationToken.None);
        var second = await useCase.ExecuteAsync(matchId, M7TestData.Timeout(TeamSide.Home), CancellationToken.None);

        Assert.True(first.Value!.Accepted);
        Assert.True(second.Value!.Accepted);
    }

    [Fact]
    public async Task ACommandForAnUnknownMatchIsRejected()
    {
        var (useCase, _, _, _) = Build(createSession: false);

        var result = await useCase.ExecuteAsync(
            Guid.Parse("bbbbbbbb-0000-0000-0000-000000000009"),
            M7TestData.ChangePace(TeamSide.Home),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("canli degil", result.Error);
    }

    [Fact]
    public async Task ACommandAfterTheMatchEndsIsRejectedButAcknowledged()
    {
        // Mac terminal olduktan sonra gelen komut: kabul edilmez ama hata da
        // degildir. 07 §6: "Mac bittiginde bekleyen komutlar Expired."
        var (useCase, sessions, _, _) = Build();
        var matchId = sessions.ActiveMatches[0];
        var session = sessions.Find(matchId)!;

        await session.RunToCompletionAsync(CancellationToken.None);

        var result = await useCase.ExecuteAsync(matchId, M7TestData.ChangePace(TeamSide.Home), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.Applied);
    }
}

/// <summary>
/// M7, 03: "Match completion idempotent olmali; ayni mac odulu yeniden
/// verilmemeli."
///
/// <para><b>Idempotency NEREDE saglaniyor?</b> <c>record.Equals</c>'te degil,
/// <b>kosullu guncellemede</b>: <c>WHERE lifecycle = 'Running'</c>. Iki komsu
/// istekten yalniz biri gunceller. Bu testin <i>kaniti</i> sahte deponun
/// kuralidir; asil kanit gercek veritabaninda yazilacak
/// (<c>TryCompleteAsync</c> testi).</para>
///
/// <para><b>D87 NEDEN ENGEL DEGIL?</b> Bozuk <c>record</c> esitligi burada
/// hic kullanilmaz. Idempotency <c>Equals</c>'e degil, veritabani kisitina
/// dayanir.</para>
/// </summary>
public class CompleteMatchTests
{
    [Fact]
    public async Task ASecondCompletionAttemptWritesNothing()
    {
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 4242);

        var matches = new InMemoryMatchRepository();
        var events = new InMemoryEventRepository();
        var sessions = new MatchSessionStore();

        matches.Seed(new MatchRecord(
            setup.MatchId, Owner, MatchLifecycle.Running, setup.Seed,
            "hash", "engine", "rules", "digest", Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UnixEpoch, null, null, null));

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);
        sessions.GetOrAdd(setup.MatchId, () => session);

        var outcome = await session.RunToCompletionAsync(CancellationToken.None);
        var complete = new CompleteMatch(sessions, matches, events);

        Assert.True(await complete.ExecuteAsync(outcome, CancellationToken.None));
        Assert.Equal(1, matches.CompletionWrites);

        // IKINCI DENEME: BIR SEY YAZMAZ.
        Assert.False(await complete.ExecuteAsync(outcome, CancellationToken.None));
        Assert.Equal(1, matches.CompletionWrites);

        // Eventler yalniz BIRA kez yazilir (UNIQUE (match_id, sequence)).
        Assert.Equal(2, events.AppendCalls);
        Assert.Equal(
            outcome.Events.Length,
            events.CountFor(setup.MatchId));
    }

    [Fact]
    public async Task WithoutTheLifecycleGuardTheSameMatchWouldBeWrittenTwice()
    {
        // KUSURLU DEPO modu. Bu testin varlik sebebi: dogru deponun
        // davranisinin bir HATA hali oldugunu gostermek. Kisis (ignore)
        // degistirilirse bu test yesil kalir ve hicbir sey bozulmaz.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 4243);

        var matches = new InMemoryMatchRepository { IgnoreLifecycleGuard = true };
        var events = new InMemoryEventRepository();
        var sessions = new MatchSessionStore();

        matches.Seed(new MatchRecord(
            setup.MatchId, Owner, MatchLifecycle.Running, setup.Seed,
            "hash", "engine", "rules", "digest", Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UnixEpoch, null, null, null));

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);
        sessions.GetOrAdd(setup.MatchId, () => session);

        var outcome = await session.RunToCompletionAsync(CancellationToken.None);
        var complete = new CompleteMatch(sessions, matches, events);

        Assert.True(await complete.ExecuteAsync(outcome, CancellationToken.None));
        Assert.True(await complete.ExecuteAsync(outcome, CancellationToken.None));

        // Iki kez yazildi. Koruma olmasaydi odul iki kez verilirdi.
        Assert.Equal(2, matches.CompletionWrites);
    }

    [Fact]
    public async Task TheClientSuppliedScoreIsNeverUsed()
    {
        // 09 §M7: "Client'in gonderdigi skor dikkate alinmaz." Oturum
        // ozeti MOTORUN eventlerinden kurulur. Bu test, ozetin skorunun
        // makul bir aralihta oldugunu ve state ile ayni oldugunu olcer.
        var config = M7TestData.Config();
        var setup = M7TestData.Mirror(seed: 555);

        using var session = new MatchSession(setup.MatchId, Owner, setup, config);
        var outcome = await session.RunToCompletionAsync(CancellationToken.None);

        Assert.Equal(outcome.State.HomeScore, outcome.HomeScore);
        Assert.Equal(outcome.State.AwayScore, outcome.AwayScore);
        Assert.Equal(outcome.State.Clock.ElapsedGameTimeMs, outcome.ElapsedGameTimeMs);
        Assert.InRange(outcome.HomeScore, 40, 160);
        Assert.InRange(outcome.AwayScore, 40, 160);

        // Possession sayimi de eventlerden; toplam possession sayisi
        // 0'dan buyuk ve makul.
        Assert.True(outcome.HomePossessions > 50);
        Assert.True(outcome.AwayPossessions > 50);
    }

    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
}
