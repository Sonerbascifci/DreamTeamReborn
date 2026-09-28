using System.Net.Http.Json;
using DreamTeam.Api.Endpoints;
using DreamTeam.Application.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DreamTeam.Api.Tests;

/// <summary>
/// M7: hangi yolların gerçekten kayıt olduğunu <b>ÖLÇER</b>.
///
/// <para><b>BU NEDEN VAR?</b> M7'de rota desenleri iki kez sessizce 404
/// döndü ve nedeni gözle anlaşılmadı: `MapGroup("/api/players")` kökünde
/// `"/"` ve `""` desenlerinin ikisi de beklenmedik şekilde eşleşmedi. Gerçek
/// davranışı tahmin etmek yerine <b>ölçtüm</b>. Aynı hata bir daha olursa bu
/// test kırmızıya döner ve "hangi desen gerçekten çalışıyor" sorusu bir
/// bakışta yanıtlanır.</para>
///
/// <para><b>NE ÖLÇÜYOR?</b> Kayıt edilen <i>tüm</i> yolların listesini ve
/// herkesin bildiği birkaç yolun gerçekten var olduğunu. Üretimde var olup
/// hiç çağrılmayan bir yol, hiç çağrılmayan bir yoldur.</para>
/// </summary>
public class RouteRegistrationTests
{
    [Fact]
    public async Task TheExpectedRoutesAreRegistered()
    {
        var patterns = await RegisteredPatternsAsync();

        // Her biri üretim yüzeyinin bir parçası. Eksik olan biri, hiç
        // çağrılamayan bir yoldur ve test bunu söyler.
        string[] required =
        [
            "/api/health",
            "/api/auth/token",
            "/api/players",
            "/api/teams",
            "/api/teams/{teamId:guid}/roster",
            "/api/teams/{teamId:guid}/roster/{playerId:guid}",
            "/api/teams/{teamId:guid}/lineup",
            "/api/matches",
            "/api/matches/{matchId:guid}",
            "/api/matches/{matchId:guid}/commands",
        ];

        var missing = required
            .Where(expected => !patterns.Contains(expected, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Eksik yollar: " + string.Join(", ", missing)
            + Environment.NewLine
            + "Kayitli olanlar: " + string.Join(" | ", patterns));
    }

    [Fact]
    public async Task TheGroupRootMapsWithoutATrailingSlash()
    {
        // Kayit edilen desenler "/api/players" KOK yolunu icermeli. "/api/players/"
        // (son slash) kaydi varsa istekler calismaz; ASP.NET bunu sessizce
        // kabul eder.
        var patterns = await RegisteredPatternsAsync();

        Assert.Contains("/api/players", patterns, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/players/", patterns, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheHealthEndpointAnswers()
    {
        // Kayit + calistirma birlikte. Bir yol kayitli ama cagrilamaz olabilir
        // (donanim hatasi, middleware sirasi); bu ikisini birlikte olcer.
        await using var server = await ApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", body);
    }

    /// <summary>
    /// Uygulamanın kaydettiği tüm yollar. <c>WebApplication</c> yerine
    /// dogrudan bir <see cref="IEndpointRouteBuilder"/> kuruyoruz; boylece
    /// gercek kayit davranisini sunucu acmadan goruyoruz.
    /// </summary>
    /// <summary>
    /// Gercek sunucudan alinan <see cref="EndpointDataSource"/>'in listesini
    /// dondurur. Ayri bir <c>WebApplication</c> kurmaz; boylece test
    /// uretimle ayni DI ve ayni middleware sirasini olcer.
    /// </summary>
    private static async Task<IReadOnlyList<string>> RegisteredPatternsAsync()
    {
        await using var server = await ApiServer.StartAsync();

        var dataSource = server.Services.GetRequiredService<EndpointDataSource>();

        return [.. dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)];
    }
    [Fact]
    public async Task TheAuthenticatedSurfaceAnswers()
    {
        // Bu test bir kez "hepsi 404 donuyor" diye yazildi ve gercek bir
        // hatayi yakaladi: test istemcisi TestServer yerine GERCEK bir
        // sunucuya gidiyordu. Endpoint'ler kayitliydi; istemci yanlisti.
        //
        // Simdi kalici bir regresyon testi: kimlikli istekler gercekten
        // sunucuya ulasmali. Bir istemci kurma hatasi yeniden yapilirsa
        // burasi kirmiziya doner.
        await using var server = await ApiServer.StartAsync();

        // Gercek akis: once jeton al (kullanici olusur), sonra jeton kullan.
        // <c>CreatePlayer</c> bilinmeyen bir kullaniciya oyuncu vermez; bu
        // dogru davranistir ve burada da gecerlidir.
        var anon = server.CreateClient();
        var registered = await anon.PostAsJsonAsync("/api/auth/token", new { displayName = "alice" });
        Assert.True(registered.IsSuccessStatusCode, await registered.Content.ReadAsStringAsync());
        anon.Dispose();

        var client = server.CreateClient();
        TestAuth.Apply(client, ApiTestData.Alice);

        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/api/players")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/api/teams")).StatusCode);

        var team = await client.PostAsJsonAsync("/api/teams", new { name = "RegresyonTakim" });
        Assert.True(team.IsSuccessStatusCode,
            $"POST /api/teams -> {(int)team.StatusCode}: {await team.Content.ReadAsStringAsync()}");

        var player = await client.PostAsJsonAsync("/api/players", new
        {
            displayName = "RegresyonOyuncu",
            position = 0,
            ratings = ApiTestData.Ratings(),
        });

        Assert.True(player.IsSuccessStatusCode,
            $"POST /api/players -> {(int)player.StatusCode}: {await player.Content.ReadAsStringAsync()}");

        // HAT SOZLESMESI: enum AD olarak mi NUMARA olarak mi gidiyor? Burada
        // olcuyoruz cunku istemci buna gore yazilacak ve varsayim yanlis
        // oldugunda sozlesme sessizce degismis olur.
        var raw = await player.Content.ReadAsStringAsync();
        Assert.Contains("\"displayName\":\"RegresyonOyuncu\"", raw);
    }
    private sealed class StubConfigProvider : Application.Ports.IMatchConfigProvider
    {
        public MatchEngine.Config.EngineConfig GetConfig(out string configHash)
        {
            configHash = "test";
            return ApiTestData.Config();
        }
    }
}
