using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DreamTeam.Application.Ids;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Application.UseCases;
using DreamTeam.Api.Auth;
using DreamTeam.Api.Endpoints;
using DreamTeam.Api.Realtime;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DreamTeam.Api.Tests;

/// <summary>
/// M7: API'yi bellek ici, gercek HTTP boru hatti ile ayaga kaldiran
/// sunucu.
///
/// <para><b>NEDEN KENDI SUNUCUMUZ VAR?</b> <c>WebApplicationFactory</c>
/// <c>Program.cs</c>'in top-level <c>Program</c> tipini kullanir ve
/// <c>appsettings.json</c>'i okur. Bizim <c>Program.cs</c> gercek bir
/// PostgreSQL baglanti dizesi ZORUNLU kiliyor; testte bu satiri atlamak
/// uretim yolunu degistirirdi. Burada ayni endpoint'leri, ayni middleware
/// sirasini ve ayni DI kayitlarini <b>kendi host'umuzda</b> kuruyoruz.</para>
///
/// <para><b>BU BIR SAHTE MI?</b> Kimlik dogrulama <b>gercek</b> JWTBearer
/// middleware'idir; gercek bir imzali jeton uretilip dogrulanir. HTTP katmani
/// gercektir. Tek sahte olan veritabanidir ve o zaten ayrica isaretlidir.</para>
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ApiServer(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    /// <summary>
    /// Kimlik bagli yeni bir istemci. <b>TestServer'ın handler'ini paylasir.</b>
    ///
    /// <para><b>BU YARDIMCI NEDEN VAR?</b> Ilk yazimda testler
    /// <c>new HttpClient { BaseAddress = server.Client.BaseAddress }</c> ile
    /// istemci kuruyordu. Bu istemci TestServer'a DEGIL, <c>http://localhost</c>
    /// adresindeki GERCEK bir sunucuya gidiyordu; oradaki 404 sayfasi
    /// "endpoint kayitli degil" gibi gorunuyordu ve saatlerce yanlis yere
    /// bakmaya yol acti. Endpoint'ler kayitliydi; <b>istemci yanlisti</b>.
    /// Artik her istemci TestServer'in kendi handler'indan gelir.</para>
    /// </summary>
    public HttpClient CreateClient()
    {
        var client = _app.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");

        return client;
    }

    public IServiceProvider Services => _app.Services;

    public static async Task<ApiServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        ConfigureServices(builder.Services);

        var app = builder.Build();

        // <b>AYNI composition root.</b> Endpoint'ler uretimde
        // Program.cs icinden map edilir; testte de ayni cagri kullanilir.
        // Test kendi endpoint listesini yazsaydi, uretimde eklenen bir yol
        // testlerde hic test edilmeden kalirdi.
        app.MapDreamTeamEndpoints(app.Services.GetRequiredService<TokenOptions>());
        app.MapHub<MatchHub>("/hubs/match");

        await app.StartAsync();

        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");

        return new ApiServer(app, client);
    }

    /// <summary>
    /// DI kayitlari. <c>Program.cs</c> ile ayni kayitlari tekrar ediyoruz;
    /// fark yazmis olsaydi testler yanlis yolu olcerdi. Tek istisna:
    /// veritabani yerine bellek.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        services.AddSingleton(ApiTestData.TokenIssuer());

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = ApiTestData.TokenIssuer().ValidationParameters;
                options.MapInboundClaims = false;
            });

        services.AddAuthorization();

        services.AddSignalR(o =>
        {
            o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IMatchConfigProvider>(_ => new StubConfigProvider(ApiTestData.Config()));
        services.AddSingleton<IMonotonicClock>(new TestClock());
        services.AddSingleton<IDelay>(new NoDelay());
        services.AddSingleton<ISetupDigest, SetupDigest>();
        services.AddSingleton<MatchSessionStore>();

        services.AddSingleton<IUserRepository, MemoryUserRepository>();
        services.AddSingleton<IPlayerRepository, MemoryPlayerRepository>();
        services.AddSingleton<ITeamRepository, MemoryTeamRepository>();
        services.AddSingleton<IMatchRepository, MemoryMatchRepository>();
        services.AddSingleton<IEventRepository, MemoryEventRepository>();
        services.AddSingleton<ICommandLogRepository, MemoryCommandLog>();

        services.AddSingleton(ApiTestData.Jwt());
        services.AddSingleton(new Api.Endpoints.TokenOptions { AllowSelfRegistration = true });
        services.AddSingleton(new LivePacerOptions());

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<CreatePlayer>();
        services.AddScoped<SetLineup>();
        services.AddScoped<StartMatch>();
        services.AddScoped<SendManagerCommand>();
        services.AddScoped<CompleteMatch>();
        services.AddSingleton<AbortOrphanedMatches>();

        services.AddSingleton<IMatchBroadcaster, SignalRMatchBroadcaster>();
        services.AddHostedService<LiveMatchRunner>();
    }

    /// <summary>Elle ilerletilen saat. Pacer testleri bunu kullanir.</summary>
    public sealed class TestClock : IMonotonicClock
    {
        private readonly long _start = Environment.TickCount64;

        public long ElapsedMilliseconds => Environment.TickCount64 - _start;
    }

    /// <summary>
    /// Beklemeyen gecikme. Testler duvar saati beklemez; pacer mantigi
    /// Application testlerinde sahte saatle ayrica olculuyor.
    /// </summary>
    public sealed class NoDelay : IDelay
    {
        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class StubConfigProvider : IMatchConfigProvider
    {
        private readonly EngineConfig _config;

        public StubConfigProvider(EngineConfig config) => _config = config;

        public EngineConfig GetConfig(out string configHash)
        {
            configHash = new DreamTeam.MatchEngine.Core.MatchSimulation(_config).ConfigHash;
            return _config;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>M7: testlere JWT uretme yardimci.</summary>
public static class TestAuth
{
    public static void Apply(HttpClient client, Guid userId, string name = "test")
    {
        var token = ApiTestData.TokenIssuer().Issue(userId, name);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
