using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DreamTeam.Api.Contracts;
using DreamTeam.Api.Endpoints;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.Api.Tests;

/// <summary>
/// M7: HTTP uzerinden uctan uca mac akisi.
///
/// <para><b>BU NEDEN VAR?</b> Application testleri use case'leri dogrudan
/// cagirir; API testleri yalniz yetkiyi olcer. Arada bir bosluk kalirdi:
/// endpoint, JSON sozlesmesi, hub ve yayinin birlikte calisip calismadigi
/// olculmuyordu. Burada o bosluk kapanir.</para>
///
/// <para><b>CANLI YURUTME HIZLI.</b> Test sunucusunda <c>IDelay</c> gercek
/// beklemiyor; pacer mantigi Application testlerinde sahte saatle ayrica
/// olculuyor. Buradaki amac mantigi degil <b>boru hattini</b> olcmek.</para>
/// </summary>
public class MatchLifecycleTests
{
    [Fact]
    public async Task AMatchIsCreatedWithAFrozenSetupDigest()
    {
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);

        var response = await StartRawAsync(client, teams, seed: 20260928);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var created = await response.Content.ReadFromJsonAsync<StartMatchResponse>();

        Assert.NotNull(created);

        // 03: "Surum ve tekr uretilebilirlik." Digest kalici kimliktir.
        Assert.Equal(64, created!.SetupDigest.Length);
        Assert.False(string.IsNullOrWhiteSpace(created.ConfigHash));

        // D109: canli mac 8 GERCEK dakika, hiz carpani 6.0.
        Assert.Equal(8 * 60, created.TargetWallClockSeconds);
        Assert.Equal(6.0, created.Speedup);
    }

    [Fact]
    public async Task TheSameSeedAndLineupsProduceTheSameSetupDigest()
    {
        // Ayni girdi ayni digest. Farkli seed farkli digest.
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);

        var first = await StartAsync(client, teams, seed: 555);
        var second = await StartAsync(client, teams, seed: 555);
        var different = await StartAsync(client, teams, seed: 556);

        Assert.Equal(first.SetupDigest, second.SetupDigest);
        Assert.NotEqual(first.SetupDigest, different.SetupDigest);
    }

    [Fact]
    public async Task AReconnectReturnsEventsWithNoGapAndNoOverlap()
    {
        // 07 §8: istemci kesildi, geri baglandi. Sunucudan "bu sequence'ten
        // sonrasini" ister ve BOSLUK GORMEMELIDIR.
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);
        var created = await StartAsync(client, teams, seed: 777);

        await WaitForEventsAsync(client, created.MatchId, minimum: 20);

        var first = await client.GetFromJsonAsync<StateResponse>(
            $"/api/matches/{created.MatchId}?afterSequence=0");

        Assert.NotNull(first);
        Assert.NotEmpty(first!.Events);

        // FromSequence = "bu yanitta hangi sequence'ten SONRASINI verdim".
        // Istemci sifirdan sordu, sunucu da sifirdan baslamalidir.
        //
        // Bu bir ONCEKI GERCEK HATANIN REGRESYON TESTIDIR. Eski kod
        // snapshot istendiginde olmadigi icin FromSequence yerine
        // CurrentSequence donuyordu: yanit 1..1051 eventi icerirken
        // from=1051 diyordu. Istemci nereden basladigini bilmiyordu ve
        // test "karsi istemcinin elindeki son sequence"yi dogrulayamiyordu.
        Assert.Equal(0, first.FromSequence);

        // Ilk parti 1''den baslar ve ardisiktir.
        AssertContiguous(first.Events, "reconnect-initial");
        Assert.Equal(1, first.Events[0].Sequence);

        long cursor = first.CurrentSequence;

        // <b>CANLI MAC</b> oldugu icin ikinci cagri BOS DONMEZ: araya yeni
        // eventler girer. Ilk yazimda "bos olmali" dedim ve test her zaman
        // kirmiziya dondu; hata urunde degil, varsayimdaydi.
        //
        // Dogru invariants: hicbir event cursor'DAN ONCE gelmez (ortusme
        // yok) ve gelenler ardisiktir (bosluk yok).
        var second = await client.GetFromJsonAsync<StateResponse>(
            $"/api/matches/{created.MatchId}?afterSequence={cursor}");

        Assert.NotNull(second);
        Assert.Equal(cursor, second!.FromSequence);
        Assert.All(second.Events, e => Assert.True(e.Sequence > cursor));
        AssertContiguous(second.Events, "reconnect");
    }

    [Fact]
    public async Task AReconnectWithASnapshotReturnsOnlyLaterEvents()
    {
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);
        var created = await StartAsync(client, teams, seed: 888);

        await WaitForEventsAsync(client, created.MatchId, minimum: 10);

        var withSnapshot = await client.GetFromJsonAsync<StateResponse>(
            $"/api/matches/{created.MatchId}?afterSequence=0&includeSnapshot=true");

        Assert.NotNull(withSnapshot);

        // Snapshot siniri, gelen ilk eventin bir eksi olmali: arada bosluk
        // olmamali ve hicbir event oncesi gelmemeli.
        Assert.All(withSnapshot!.Events, e => Assert.True(e.Sequence > withSnapshot.FromSequence));
        AssertContiguous(withSnapshot.Events, "snapshot-reconnect");

        // Snapshot JSON'u cozulebilir ve sequence'i tasir (07 §8).
        //
        // <b>NEDEN "varsa"?</b> Snapshot'i yalniz CANLI oturum uretir; mac
        // bitip oturum kapandiginda kalici kayittan okunur ve o yolda
        // snapshot yoktur. Bu bir bicim degil, bir durum farkidir.
        var json = withSnapshot.SnapshotJson;

        if (json is { Length: > 0 })
        {
            using var snapshot = JsonDocument.Parse(json);

            // Motorun StateSequence alani "sonraki" isaretcisidir; bizim
            // sinirimiz "son teslim edilen" (07 §8, SessionCapture).
            Assert.Equal(
                withSnapshot.FromSequence + 1,
                snapshot.RootElement.GetProperty("StateSequence").GetInt64());
        }
    }

    [Fact]
    public async Task AClientSuppliedScoreIsIgnored()
    {
        // 09 §M7: "Client'in gonderdigi skor dikkate alinmaz."
        // Istemci komut govdesine sahte skor alanlari koyuyor; sunucu bu
        // alanlari TANIMAZ ve sonuc MOTORUNUR.
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);
        var created = await StartAsync(client, teams, seed: 999);

        var response = await client.PostAsJsonAsync($"/api/matches/{created.MatchId}/commands", new
        {
            matchId = created.MatchId,
            commandId = Guid.NewGuid(),
            side = 0,
            kind = 0,
            targetBoundary = 0,
            homeScore = 999,
            awayScore = 0,
        });

        // 202: kabul edildi. Sahte skor alanlari YOK sayildi.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var state = await client.GetFromJsonAsync<StateResponse>($"/api/matches/{created.MatchId}");

        // Hicbir event 999 puan bildirmis olamaz.
        Assert.All(state!.Events, e => Assert.NotEqual(999, ExtractScore(e)));
    }

    [Fact]
    public async Task TheSameCommandIdTwiceIsAcceptedOnce()
    {
        // 07 §5: "Ayni CommandId yeniden gelirse ayni sonuc dondurulur."
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);
        var created = await StartAsync(client, teams, seed: 1010);

        var commandId = Guid.NewGuid();

        object Request() => new
        {
            matchId = created.MatchId,
            commandId,
            side = 0,
            kind = (ManagerCommandKind)ManagerCommandKind.ChangePace,
            targetBoundary = (CommandBoundary)CommandBoundary.ActionDecision,
        };

        var first = await client.PostAsJsonAsync(
            $"/api/matches/{created.MatchId}/commands", Request());
        var second = await client.PostAsJsonAsync(
            $"/api/matches/{created.MatchId}/commands", Request());

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var firstAck = await first.Content.ReadFromJsonAsync<CommandAck>();
        var secondAck = await second.Content.ReadFromJsonAsync<CommandAck>();

        Assert.Equal(commandId, firstAck!.CommandId);
        Assert.Equal(commandId, secondAck!.CommandId);
        Assert.True(firstAck.Accepted);
        Assert.True(secondAck.Accepted);

        // 07 §5: ACK, "alindi/kuyruga girdi" demektir; Applied ayri bir
        // bildirimdir. Tek mesajda ikisi karistirilmaz.
        Assert.False(firstAck.Applied);
        Assert.False(secondAck.Applied);
    }

    [Fact]
    public async Task ACommandFromAnotherUserIsRejected()
    {
        await using var server = await ApiServer.StartAsync();
        var alice = await Fixtures.RegisterAsync(server, ApiTestData.Alice);
        var bob = await Fixtures.RegisterAsync(server, ApiTestData.Bob);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);
        var created = await StartAsync(alice, teams, seed: 1111);

        var response = await bob.PostAsJsonAsync($"/api/matches/{created.MatchId}/commands", new
        {
            matchId = created.MatchId,
            commandId = Guid.NewGuid(),
            side = 0,
            kind = 0,
            targetBoundary = 0,
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
            $"Beklenen 400 veya 403, gelen {response.StatusCode}.");
    }

    [Fact]
    public async Task ALiveMatchRunsToCompletionWithAnEngineProducedScore()
    {
        // EN UCDAN UCA. Mac baslar, canli yurutulur ve biter. Pacer beklemez
        // (test sunucusunda NoDelay) ama motor YAVASLATILMAZ; yani sonuc
        // motorun normal sonucudur.
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);
        var created = await StartAsync(client, teams, seed: 1212);

        var terminal = await WaitForTerminalAsync(client, created.MatchId, TimeSpan.FromSeconds(60));

        Assert.True(terminal.IsTerminal, "Mac zaman icinde bitmedi.");

        // Skor MOTORUN MatchEnded olayindan gelir. Ilk yazimda
        // "ShotMade sayisi x 2" vekiline guvenildi; o vekil iki takimin
        // TOPLAMINI sayar ve 234 gibi bir sayi uretir. Yanlis bir olcum,
        // dogru bir testin en kotu halidir.
        var ended = terminal.Events[^1].Payload;

        Assert.Equal(MatchEventType.MatchEnded, terminal.Events[^1].Type);

        var home = ended.GetProperty("homeScore").GetInt32();
        var away = ended.GetProperty("awayScore").GetInt32();

        Assert.InRange(home, 60, 160);
        Assert.InRange(away, 60, 160);
        Assert.Equal(home == away, ended.GetProperty("isTie").GetBoolean());
    }

    [Fact]
    public async Task AfterCompletionTheMatchIsStillReadableFromPersistence()
    {
        // Oturum kapandiktan sonra kalici kayittan okunabilmeli: yeniden
        // baglanan istemci veriyi kaybetmemeli.
        await using var server = await ApiServer.StartAsync();
        var client = await Fixtures.RegisterAsync(server, ApiTestData.Alice);

        var teams = await Fixtures.TwoTeamsAsync(server, ApiTestData.Alice, 12);
        var created = await StartAsync(client, teams, seed: 1313);

        await WaitForTerminalAsync(client, created.MatchId, TimeSpan.FromSeconds(60));

        // Oturum depodan temizlendikten sonra okuma calismali.
        await Task.Delay(500);

        var response = await client.GetAsync($"/api/matches/{created.MatchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"isTerminal\":true", body);
    }

    // -----------------------------------------------------------------
    // Yardimcilar
    // -----------------------------------------------------------------

    private static async Task<HttpResponseMessage> StartRawAsync(
        HttpClient client, TwoTeams teams, ulong seed) =>
        await client.PostAsJsonAsync("/api/matches", new
        {
            homeTeamId = teams.Home.TeamId,
            awayTeamId = teams.Away.TeamId,
            seed,
            homeLineup = teams.Home.Lineup,
            awayLineup = teams.Away.Lineup,
        });

    private static async Task<StartMatchResponse> StartAsync(
        HttpClient client, TwoTeams teams, ulong seed)
    {
        var response = await StartRawAsync(client, teams, seed);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<StartMatchResponse>())!;
    }

    private static async Task<StateResponse> WaitForEventsAsync(
        HttpClient client, Guid matchId, int minimum)
    {
        for (var attempt = 0; attempt < 400; attempt++)
        {
            var state = await client.GetFromJsonAsync<StateResponse>($"/api/matches/{matchId}");

            if (state is not null && state.Events.Count >= minimum)
            {
                return state;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"{minimum} event birikmeyi basaramadi.");
    }

    private static async Task<StateResponse> WaitForTerminalAsync(
        HttpClient client, Guid matchId, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;

        while (DateTime.UtcNow < deadline)
        {
            var state = await client.GetFromJsonAsync<StateResponse>($"/api/matches/{matchId}");

            if (state?.IsTerminal == true)
            {
                return state;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Mac {budget.TotalSeconds:F0} saniyede bitmedi.");
    }

    /// <summary>
    /// Event dizisi <b>ardisik</b> mi? 07 §8: ortusme ve bosluk siniri
    /// ATOMIK tanimlanir. Tekrar eden veya atlanan sequence, istemcinin bir
    /// zaman dilimini sessizce kaybettigi andir.
    /// </summary>
    private static void AssertContiguous(IReadOnlyList<MatchEventDto> events, string context)
    {
        for (var i = 1; i < events.Count; i++)
        {
            var previous = events[i - 1].Sequence;
            var current = events[i].Sequence;

            Assert.True(
                current == previous + 1,
                context + ": sequence bosluğu. " + previous + " sonrasi "
                + current + " geldi.");
        }
    }

    private static int ExtractScore(MatchEventDto dto)
    {
        if (dto.Payload.ValueKind != JsonValueKind.Object
            || !dto.Payload.TryGetProperty("points", out var points))
        {
            return -1;
        }

        return points.GetInt32();
    }
}

/// <summary>Bir takim ve onun bes kisilik lineup'i (motor kurali, D31).</summary>
internal sealed record TeamWithLineup(Guid TeamId, IReadOnlyList<Guid> Lineup);

/// <summary>Bir macin iki tarafi: ev ve deplasman.</summary>
internal sealed record TwoTeams(TeamWithLineup Home, TeamWithLineup Away);

/// <summary>M7 API testleri icin kurulum yardimcilari.</summary>
internal static class Fixtures
{
    private static int _player;
    private static int _team;

    /// <summary>
    /// Gercek yoldan kullanici olusturur: <c>/api/auth/token</c> cagrilir.
    /// <c>CreatePlayer</c> bilinmeyen bir kullaniciya oyuncu vermez; test de
    /// o yoldan gecer.
    /// </summary>
    public static async Task<HttpClient> RegisterAsync(ApiServer server, Guid userId)
    {
        var name = userId == ApiTestData.Alice ? "alice" : "bob";

        var anon = server.CreateClient();
        var registered = await anon.PostAsJsonAsync("/api/auth/token", new { displayName = name });
        registered.EnsureSuccessStatusCode();
        anon.Dispose();

        var client = server.CreateClient();
        TestAuth.Apply(client, userId);

        return client;
    }

    public static async Task<TwoTeams> TwoTeamsAsync(ApiServer server, Guid userId, int rosterSize)
    {
        var client = await RegisterAsync(server, userId);

        var first = await OneTeamAsync(client, rosterSize);
        var second = await OneTeamAsync(client, rosterSize);

        return new TwoTeams(first, second);
    }

    private static async Task<TeamWithLineup> OneTeamAsync(HttpClient client, int rosterSize)
    {
        var name = $"Takim{Interlocked.Increment(ref _team)}";

        var teamResponse = await client.PostAsJsonAsync("/api/teams", new { name });
        teamResponse.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await teamResponse.Content.ReadAsStringAsync());
        var teamId = document.RootElement.GetProperty("id").GetGuid();

        var ids = new List<Guid>(rosterSize);

        for (var i = 0; i < rosterSize; i++)
        {
            var playerName = $"O{Interlocked.Increment(ref _player)}";

            var playerResponse = await client.PostAsJsonAsync("/api/players", new
            {
                displayName = playerName,
                position = i % 5,
                ratings = ApiTestData.Ratings(68 + (i % 14)),
            });

            playerResponse.EnsureSuccessStatusCode();

            using var playerDocument =
                JsonDocument.Parse(await playerResponse.Content.ReadAsStringAsync());

            var playerId = playerDocument.RootElement.GetProperty("id").GetGuid();

            var add = await client.PostAsJsonAsync(
                $"/api/teams/{teamId}/roster", new { playerId });
            add.EnsureSuccessStatusCode();

            ids.Add(playerId);
        }

        return new TeamWithLineup(teamId, ids.Take(5).ToList());
    }
}
