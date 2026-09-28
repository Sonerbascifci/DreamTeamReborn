using System.Security.Claims;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Application.UseCases;
using DreamTeam.Api.Endpoints;
using DreamTeam.Api.Auth;
using DreamTeam.Api.Realtime;
using DreamTeam.Infrastructure;
using DreamTeam.Infrastructure.Persistence;
using DreamTeam.Infrastructure.Security;
using DreamTeam.MatchEngine.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// M7: composition root.
//
// BU DOSYA "NE KULLANILACAK" SORUSUNUN YERIDIR. Her servis kaydi burada
// gorunur; baska bir yerde kayit yoktur. 03 bunu ister: her sey icin ayri
// bir kayit noktasi, gizli otomatik bulma yok.
// =====================================================================

// --- Yapilandirma ----------------------------------------------------

// Denge belgesi. M6 D103: denge kaynagi JSON belgedir, motorun fabrika
// varsayilani DEGIL. Sunucu da ayni belgeyi okur.
var balanceConfigPath = builder.Configuration["Balance:ConfigPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "engine", "baseline.v0.1.json");

if (!File.Exists(balanceConfigPath))
{
    throw new InvalidOperationException(
        $"Denge belgesi bulunamadi: {balanceConfigPath}. "
        + "Sunucu KALIBRE EDILMEMIS bir motorla acilmaz; bu bir hata, bir uyari degil.");
}

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

// Sifre ortam degiskeninden: DREAMTEAM_Jwt__SigningKey gibi gelir.
// Dosyaya yazilmaz. Varsa acilista hata verir (JwtOptions.Validate).
jwtOptions.Validate();

var tokenOptions = builder.Configuration
    .GetSection(TokenOptions.SectionName)
    .Get<TokenOptions>() ?? new TokenOptions();

var pacerOptions = builder.Configuration
    .GetSection(LivePacerOptions.SectionName)
    .Get<LivePacerOptions>() ?? new LivePacerOptions();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Postgres zorunludur. Ornek: config/database/connection-string.example.txt");

// --- Servisler -------------------------------------------------------

// Tek veritabani havuzu. Tum repository'ler BUNU paylasir.
builder.Services.AddSingleton(_ => DreamTeamDatabase.Connect(connectionString));

// Denge config'i: bir kez okunur, motor hash'i ile birlikte saklanir.
builder.Services.AddSingleton<IMatchConfigProvider>(
    _ => new FileMatchConfigProvider(balanceConfigPath));

// Repository'ler. Somut tip kayit, arayuz DEGIL: somut zaten
// arayuzu uyguluyor ve DI eklenmesi bir sey kazandirmaz.
builder.Services.AddSingleton<IUserRepository, PostgresUserRepository>();
builder.Services.AddSingleton<IPlayerRepository, PostgresPlayerRepository>();
builder.Services.AddSingleton<ITeamRepository, PostgresTeamRepository>();
builder.Services.AddSingleton<IMatchRepository, PostgresMatchRepository>();
builder.Services.AddSingleton<IEventRepository, PostgresEventRepository>();
builder.Services.AddSingleton<ICommandLogRepository, PostgresCommandLogRepository>();

builder.Services.AddSingleton<IMonotonicClock, SystemClock>();
builder.Services.AddSingleton<IDelay, SystemDelay>();
builder.Services.AddSingleton<ISetupDigest, SetupDigest>();

// Oturum deposu. <b>Singleton.</b> 03: "Match runner ayni macin state'ini
// tek sahip altinda seri degistirir." Iki depo olsaydi iki sahip olurdu.
builder.Services.AddSingleton<MatchSessionStore>();

// Use case'ler. ICurrentUser HTTP baglamindan gelir; bu yuzden SCOPED.
// Iyi ki bu singleton DEGIL: aksi halde kullanici kimligi tum
// baglantilarda ayni kalirdi.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<CreatePlayer>();
builder.Services.AddScoped<SetLineup>();
builder.Services.AddScoped<StartMatch>();
builder.Services.AddScoped<SendManagerCommand>();
builder.Services.AddScoped<CompleteMatch>();
builder.Services.AddSingleton<AbortOrphanedMatches>();

builder.Services.AddSingleton(new JwtTokenIssuer(jwtOptions));
builder.Services.AddSingleton(tokenOptions);
builder.Services.AddSingleton(pacerOptions);

// --- Kimlik dogrulama ------------------------------------------------

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Tek dogrulayici: URETEN ile DOGRULAYEN ayni ayarlari paylasir.
        // Iki ayri dogrulama yolu, "neden testte gecti ama prodda gecmedi"
        // sinifini uretir.
        var parameters = new JwtTokenIssuer(jwtOptions).ValidationParameters;
        options.TokenValidationParameters = parameters;
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

// --- HTTP + SignalR --------------------------------------------------

builder.Services.AddSignalR(options =>
{
    // Zaman asimi: 8 dakikalik mac + yeniden baglanma payi. Istemci 30
    // saniye sessiz kalirsa baglanti duser ve yeniden baglanma yoluna gecer.
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IMatchBroadcaster, SignalRMatchBroadcaster>();
builder.Services.AddHostedService<LiveMatchRunner>();

builder.Services.AddProblemDetails();

var app = builder.Build();

// --- Sema kontrolu ---------------------------------------------------

// Yarim sema ile acilan sunucu ilk yazmada beklenmedik bir hata verir.
// Burada belirgin olur.
await using (var scope = app.Services.CreateAsyncScope())
{
    var database = app.Services.GetRequiredService<DreamTeamDatabase>();

    if (await database.IsMigratedAsync(CancellationToken.None))
    {
        app.Logger.LogInformation("Sema hazir.");
    }
    else
    {
        app.Logger.LogWarning(
            "Sema bulunamadi. M7'de migration otomatik UYGULANMAZ: "
            + "psql -f migrations/001_initial_schema.sql ile elle calistirin. "
            + "Bu bilincli bir tercihtir; sunucu yarim sema ile acilmaz.");

        if (app.Environment.IsDevelopment())
        {
            await database.MigrateAsync(CancellationToken.None);
            app.Logger.LogInformation("Gelistirme ortaminda sema uygulandi.");
        }
    }
}

// --- Endpoint'ler ----------------------------------------------------

app.UseAuthentication();
app.UseAuthorization();

app.MapHealth();
app.MapToken(tokenOptions);
app.MapPlayers();
app.MapTeams();
app.MapMatches(app.Services.GetRequiredService<IMonotonicClock>(), pacerOptions);
app.MapHub<MatchHub>("/hubs/match");

app.Logger.LogInformation(
    "DreamTeam API aciliyor. Motor {Engine}, kurallar {Rules}, denge {ConfigHash}, "
    + "canli hedef {Target} sn (carpan {Speedup}).",
    DreamTeam.MatchEngine.Core.EngineVersion.Current,
    DreamTeam.MatchEngine.Config.RulesIdentity.Current,
    app.Services.GetRequiredService<IMatchConfigProvider>().GetConfig(out var hash),
    pacerOptions.TargetWallClockMs / 1000,
    pacerOptions.Speedup);

await app.RunAsync();

/// <summary>
/// <b>Yalniz testler icin.</b> <c>WebApplicationFactory&lt;Program&gt;</c> bu
/// tipi kullandigi icin top-level ifadede <c>Program</c> adi verilemez;
/// bu partial tip onu gorunur kilar. Uretimde hicbiri cagrilmaz.
/// </summary>
public partial class Program;

