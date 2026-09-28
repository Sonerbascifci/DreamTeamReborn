using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DreamTeam.Application.Ports;
using DreamTeam.Api.Endpoints;
using DreamTeam.Domain.Players;

namespace DreamTeam.Api.Tests;

/// <summary>
/// M7, 09 §M7 kabul maddesi: <b>"Yetkisiz bir kullanici başka birinin
/// maçına komut gönderemez, skor yazamaz, maç sahipliğini değiştiremez."
/// (T19)</b>
///
/// <para><b>BU TEST NE KANITLAR?</b> Gercek bir HTTP istegi, gercek bir
/// JWTBearer middleware'i ve gercek bir yetki denetimi. Sahte olan tek sey
/// veritabanidir. Test, bir yetkisiz isteminin yapabilecegi <i>her</i> yolu
/// dener ve hepsinin reddedildigini olcer.</para>
///
/// <para><b>ASIL TEHLIKE NEREDE?</b> Yetki kontrolunu <i>unutabiliriz</i>.
/// 07 §5 diyor ki: "AuthenticatedUserId bağlantı/session'dan çözülür;
/// client'in 'ben şu takımım' beyanına güvenilmez." Yani istemcinin govdesinde
/// <c>userId</c> alani OLMAMALI. Test bunu ayrica dogrular.</para>
/// </summary>
public class AuthorizationTests
{
    [Fact]
    public async Task ARequestWithoutATokenIsRejected()
    {
        await using var server = await ApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/players");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ARequestWithAGarbageTokenIsRejected()
    {
        await using var server = await ApiServer.StartAsync();

        server.Client.DefaultRequestHeaders.Add("Authorization", "Bearer bu.gecersiz.jeton");

        var response = await server.Client.GetAsync("/api/players");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenSignedWithADifferentKeyIsRejected()
    {
        // IMZALAMA SALDIRISI TESTI. Anahtar degisse imza gecerli olmaz.
        await using var server = await ApiServer.StartAsync();

        var attacker = new Infrastructure.Security.JwtTokenIssuer(new Infrastructure.Security.JwtOptions
        {
            Issuer = ApiTestData.IssuerName,
            Audience = ApiTestData.Audience,
            SigningKey = "baska-bir-anahtar-32-bayttan-uzun-olmali-123456",
        });

        server.Client.DefaultRequestHeaders.Add(
            "Authorization", $"Bearer {attacker.Issue(ApiTestData.Alice, "saldirgan")}");

        var response = await server.Client.GetAsync("/api/players");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AValidTokenGetsTheOwnersOwnPlayersOnly()
    {
        await using var server = await ApiServer.StartAsync();

        await SeedPlayerAsync(server, ApiTestData.Alice, "alice-oyuncu");
        await SeedPlayerAsync(server, ApiTestData.Bob, "bob-oyuncu");

        var alice = await NewClientAsync(server, ApiTestData.Alice);

        // <b>HAT SOZLESMESI HAM JSON UZERINDEN OLCULUR.</b> Once endpoint'i
        // okuyup "enum muhtemelen sayi doner" diye varsaydik ve iki kez
        // yanildik; endpoint'in enum gosterimine bagimli bir kayit baglamak
        // bizi surekli kirdi. Ham govde hem daha saglam hem de sozlesmeyi
        // acikca gosterir.
        var raw = await alice.GetStringAsync("/api/players");

        Assert.Contains("alice-oyuncu", raw);
        Assert.DoesNotContain("bob-oyuncu", raw);
    }

    [Fact]
    public async Task AUserCannotReadAnotherUsersTeam()
    {
        await using var server = await ApiServer.StartAsync();

        var teamId = await SeedTeamAsync(server, ApiTestData.Alice);

        var bob = await NewClientAsync(server, ApiTestData.Bob);
        var response = await bob.GetAsync($"/api/teams/{teamId}/roster");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AUserCannotAddAPlayerToAnotherUsersTeam()
    {
        await using var server = await ApiServer.StartAsync();

        var teamId = await SeedTeamAsync(server, ApiTestData.Alice);
        var bobsPlayer = await SeedPlayerAsync(server, ApiTestData.Bob, "bob-un-oyuncusu");

        var bob = await NewClientAsync(server, ApiTestData.Bob);
        var response = await bob.PostAsJsonAsync(
            $"/api/teams/{teamId}/roster", new { playerId = bobsPlayer });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AUserCannotAddAnotherUsersPlayerToTheirOwnTeam()
    {
        // D111: oyuncu kisi basina. Baskasinin oyuncusu benim kadroma
        // GIREMEZ. Bu bir yetki hatasi degil, alan hatasi.
        await using var server = await ApiServer.StartAsync();

        var bobsPlayer = await SeedPlayerAsync(server, ApiTestData.Bob, "bob-un-oyuncusu");

        var alice = await NewClientAsync(server, ApiTestData.Alice);
        var teamId = await CreateTeamAsync(alice);

        var response = await alice.PostAsJsonAsync(
            $"/api/teams/{teamId}/roster", new { playerId = bobsPlayer });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("D111", body);
    }

    [Fact]
    public async Task AUserCannotStartAMatchOwnedByAnotherUser()
    {
        await using var server = await ApiServer.StartAsync();

        var alice = await NewClientAsync(server, ApiTestData.Alice);
        var bob = await NewClientAsync(server, ApiTestData.Bob);

        var aliceTeam = await SeedTeamWithLineupAsync(server, ApiTestData.Alice, 20);
        var bobTeam = await SeedTeamWithLineupAsync(server, ApiTestData.Bob, 20);

        // Bob, Alice'in takimiyla mac kurmaya calisiyor.
        var response = await bob.PostAsJsonAsync("/api/matches", new
        {
            homeTeamId = aliceTeam.TeamId,
            awayTeamId = bobTeam.TeamId,
            seed = 1234,
            homeLineup = aliceTeam.Lineup,
            awayLineup = bobTeam.Lineup,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("size ait degil", body);
    }

    [Fact]
    public async Task AUserCannotReadAnotherUsersMatch()
    {
        await using var server = await ApiServer.StartAsync();

        var alice = await NewClientAsync(server, ApiTestData.Alice);
        var team = await SeedTeamWithLineupAsync(server, ApiTestData.Alice, 20);
        var other = await SeedTeamWithLineupAsync(server, ApiTestData.Alice, 20);

        var started = await alice.PostAsJsonAsync("/api/matches", new
        {
            homeTeamId = team.TeamId,
            awayTeamId = other.TeamId,
            seed = 99,
            homeLineup = team.Lineup,
            awayLineup = other.Lineup,
        });

        Assert.True(
            started.IsSuccessStatusCode,
            $"POST /api/matches -> {(int)started.StatusCode}: {await started.Content.ReadAsStringAsync()}");

        var created = await started.Content.ReadFromJsonAsync<StartMatchResponse>();
        var bob = await NewClientAsync(server, ApiTestData.Bob);

        var response = await bob.GetAsync($"/api/matches/{created!.MatchId}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden,
            $"Bob baska bir maci okumaya calisti: {(int)response.StatusCode} "
            + $"/ matchId={created!.MatchId} body={await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task AUserCannotSendACommandToAnotherUsersMatch()
    {
        await using var server = await ApiServer.StartAsync();

        var alice = await NewClientAsync(server, ApiTestData.Alice);
        var team = await SeedTeamWithLineupAsync(server, ApiTestData.Alice, 20);
        var other = await SeedTeamWithLineupAsync(server, ApiTestData.Alice, 20);

        var started = await alice.PostAsJsonAsync("/api/matches", new
        {
            homeTeamId = team.TeamId,
            awayTeamId = other.TeamId,
            seed = 7,
            homeLineup = team.Lineup,
            awayLineup = other.Lineup,
        });

        var created = await started.Content.ReadFromJsonAsync<StartMatchResponse>();
        var bob = await NewClientAsync(server, ApiTestData.Bob);

        var response = await bob.PostAsJsonAsync($"/api/matches/{created!.MatchId}/commands", new
        {
            matchId = created!.MatchId,
            commandId = Guid.NewGuid(),
            side = 0,
            kind = 0,
            targetBoundary = 0,
        });

        // Komut MOTORA GIRMEZ. Kabul edilmez.
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
            $"Beklenen 400 veya 403, gelen {response.StatusCode}.");
    }

    [Fact]
    public async Task TheClientCannotDeclareWhichUserItIs()
    {
        // 07 §5: "client'in 'ben su takimim' beyanina guvenilmez."
        // Istek govdesinde userId alani YOKTUR; sunucu JWT'den okur.
        await using var server = await ApiServer.StartAsync();

        var bob = await NewClientAsync(server, ApiTestData.Bob);

        // Govdede userId gondermeye calisiyoruz. Kullanilmayacak.
        var response = await bob.PostAsJsonAsync("/api/players", new
        {
            displayName = "Saldirgan",
            position = 0,
            ratings = ApiTestData.Ratings(),
            userId = ApiTestData.Alice,     // <-- kullanilmayacak
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Kayit BOB'a aittir, Alice'e degil. Ham JSON uzerinden olculur;
        // endpoint'in alan gosterimine baglanmamak icin.
        var bobsPlayers = await bob.GetStringAsync("/api/players");
        Assert.Contains("Saldirgan", bobsPlayers);

        var alice = await NewClientAsync(server, ApiTestData.Alice);
        var alicesPlayers = await alice.GetStringAsync("/api/players");
        Assert.DoesNotContain("Saldirgan", alicesPlayers);
    }

    [Fact]
    public async Task TheTokenEndpointIsAnonymousButEverythingElseIsNot()
    {
        await using var server = await ApiServer.StartAsync();

        // Jeton ucu anonim olmali, yoksa hic kimse giremez.
        var token = await server.Client.PostAsJsonAsync("/api/auth/token", new { displayName = "yeni" });
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);

        // Oyuncu ucu ANONIM OLMAMALI.
        var players = await server.Client.GetAsync("/api/players");
        Assert.Equal(HttpStatusCode.Unauthorized, players.StatusCode);
    }

    [Fact]
    public async Task ATokenForOneUserCannotBeWidenedByChangingThePayload()
    {
        // Jetonu base64 decode edip govdeyi degistirmek imzayi bozar.
        await using var server = await ApiServer.StartAsync();

        var token = ApiTestData.TokenIssuer().Issue(ApiTestData.Bob, "bob");
        var parts = token.Split('.');

        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["sub"] = ApiTestData.Alice.ToString("D"),
            ["iss"] = ApiTestData.IssuerName,
            ["aud"] = ApiTestData.Audience,
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        });

        var forged = $"{parts[0]}.{Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(payload))}.{parts[2]}";

        server.Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {forged}");

        var response = await server.Client.GetAsync("/api/players");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------
    // Yardimcilar
    // -----------------------------------------------------------------

    /// <summary>
    /// Kimlikli bir istemci. <b>Once kullanici kaydi olusturur</b>:
    /// <c>CreatePlayer</c> bilinmeyen bir kullaniciya oyuncu vermez ve bu
    /// dogru davranistir. Bu yuzden testler de gercek yoldan gecer:
    /// jeton al -&gt; kullanici olusur -&gt; jeton kullan.
    /// </summary>
    private static async Task<HttpClient> NewClientAsync(ApiServer server, Guid userId)
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


    private static async Task<Guid> SeedPlayerAsync(ApiServer server, Guid userId, string name)
    {
        var client = await NewClientAsync(server, userId);

        var response = await client.PostAsJsonAsync("/api/players", new
        {
            displayName = name,
            position = 0,
            ratings = ApiTestData.Ratings(),
        });

        Assert.True(response.IsSuccessStatusCode,
            $"/api/players -> {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        // Yalniz kimlige ihtiyacimiz var. Tum kaydi baglamak, endpoint'in
        // enum gosterimine (sayi mi ad mi) bagimli hale getiriyor; kimlik
        // ise sabit bir JSON alani.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> SeedTeamAsync(ApiServer server, Guid userId)
    {
        var client = await NewClientAsync(server, userId);
        return await CreateTeamAsync(client);
    }

    private static int _teamCounter;

    private static int _playerCounter;

    private static async Task<Guid> CreateTeamAsync(HttpClient client)
    {
        // Kimlik girdiden turetilir (D115); ayni ad ayni kimligi verir
        // ve ikinci olusturma reddedilir. Test verisi benzersiz olmali.
        var name = $"TestTakim{Interlocked.Increment(ref _teamCounter)}";

        var response = await client.PostAsJsonAsync("/api/teams", new { name });
        Assert.True(response.IsSuccessStatusCode,
            $"/api/teams -> {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid TeamId, IReadOnlyList<Guid> Lineup)> SeedTeamWithLineupAsync(
        ApiServer server, Guid userId, int rosterSize)
    {
        var client = await NewClientAsync(server, userId);
        var teamId = await CreateTeamAsync(client);

        var ids = new List<Guid>(rosterSize);

        for (var i = 0; i < rosterSize; i++)
        {
            // Kimlik girdiden turetilir (D115); ayni ad ayni oyuncuyu
            // verir ve ikinci olusturma reddedilir. Test verisi benzersiz
            // olmak ZORUNDA.
            ids.Add(await SeedPlayerAsync(
                server, userId, $"O{Interlocked.Increment(ref _playerCounter)}"));
        }

        foreach (var id in ids)
        {
            var add = await client.PostAsJsonAsync(
                $"/api/teams/{teamId}/roster", new { playerId = id });
            add.EnsureSuccessStatusCode();
        }

        return (teamId, ids.Take(5).ToList());
    }
}
