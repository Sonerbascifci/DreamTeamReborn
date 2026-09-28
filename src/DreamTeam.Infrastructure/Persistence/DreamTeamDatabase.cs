using System.Data;
using System.Reflection;
using Dapper;
using Npgsql;

namespace DreamTeam.Infrastructure.Persistence;

/// <summary>
/// M7: PostgreSQL baglantisi ve migration.
///
/// <para><b>POOL'LU MI, AÇIK MI?</b> Acik (<see cref="NpgsqlDataSource"/>) ve
/// havuzlu. Gerekce somut: her istek icin yeni baglanti acmak, mac basina
/// yuzlerce baglanti demektir ve PostgreSQL'in <c>max_connections</c>'i
/// asilir. Havuz bunu cozer ve baglanti kurma suresi tekrar eden isteklerde
/// odenmez.</para>
///
/// <para><b>MIGRATION ARACI YOK.</b> Tek SQL dosyasi, elle calistirilir ve
/// <c>schema_migrations</c>'a yazilir. Nedeni somut: bu ortamda
/// <c>Testcontainers.PostgreSQL</c> paket onbelleginde YOK ve ag erisimi de
/// yok; ayrica 03 "EF migration" ve "gereksiz arac" diyor. M7 icin tek
/// dosya yeterli. M11'de degerlendirilir.</para>
///
/// <para><b>Idempotent.</b> SQL'in tamami <c>IF NOT EXISTS</c> kullanir ve
/// migration kaydi <c>ON CONFLICT DO NOTHING</c> ile yazilir. Ayni migration
/// iki kez calistirilabilir; bu bir hata degil, guvenlik.</para>
/// </summary>
public sealed class DreamTeamDatabase : IAsyncDisposable
{
    private const string MigrationResourceName =
        "DreamTeam.Infrastructure.migrations.001_initial_schema.sql";

    private const string MigrationVersion = "001_initial_schema";

    private readonly NpgsqlDataSource _dataSource;
    private readonly string _connectionString;

    private DreamTeamDatabase(NpgsqlDataSource dataSource, string connectionString)
    {
        _dataSource = dataSource;
        _connectionString = connectionString;
    }

    public static DreamTeamDatabase Connect(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        return new DreamTeamDatabase(builder.Build(), connectionString);
    }

    /// <summary>
    /// Ayni veritabanina ayni ayarlarla ikinci bir kopya. Testler her test
    /// icin ayri havuz kullanir; havuzun kendisi paylasilirsa bir testin
    /// baglantisi digerinin bekledigini bloklar.
    /// </summary>
    public DreamTeamDatabase Clone() => Connect(_connectionString);

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }

    /// <summary>
    /// Sema hazir mi? Uygulama acilirken cagrilir; hazir degilse sunucu
    /// <b>ACILMAZ</b>. Yarim bir sema ile acilan sunucu, ilk yazma
    /// denemesinde beklenmedik bir hatayi kullaniciya gosterirdi.
    /// </summary>
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var sql = ReadMigration();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO schema_migrations (version) VALUES (@version) "
            + "ON CONFLICT (version) DO NOTHING;",
            new { version = MigrationVersion },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> IsMigratedAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM information_schema.tables "
            + "WHERE table_schema = 'public' AND table_name = 'matches';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return exists > 0;
    }

    /// <summary>
    /// Testler icin: semayi DUSUR. Migration dosyasi bunu yapmaz; bir
    /// migration'in kendini geri almasi yazilmaz (geri alma elle ve
    /// gozden gecirilerek yapilir). Testler sadece kendi yarattiklari
    /// veriyi siler.
    /// </summary>
    public async Task DropSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string ReadMigration()
    {
        var assembly = typeof(DreamTeamDatabase).Assembly;

        using var stream = assembly.GetManifestResourceStream(MigrationResourceName)
            ?? throw new InvalidOperationException(
                $"Migration kaynagi bulunamadi: {MigrationResourceName}. "
                + "Proje csproj'undaki EmbeddedResource girdisini kontrol edin.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    /// <summary>
    /// Baglanti dizesi kontrolu. Sifre veritabanina GIRMEZ; yalnizca
    /// tanilama ve dosya yolu kontrolu icin. connection-string.example.txt
    /// ile karsilastirmak icin kullanilir.
    /// </summary>
    public string Redacted(string? secret = null)
    {
        var connectionString = _connectionString;
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Password = string.IsNullOrEmpty(secret) ? (string?)null : "***",
        };

        return builder.ConnectionString;
    }

    /// <summary>Testler ve tanilama: havuzdan alinan baglanti sayisi.</summary>
    public static IsolationLevel DefaultIsolationLevel => IsolationLevel.ReadCommitted;
}
