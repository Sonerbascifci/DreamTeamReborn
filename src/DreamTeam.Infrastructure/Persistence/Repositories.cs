using System.Data.Common;
using Dapper;
using DreamTeam.Application.Ports;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Commands;

namespace DreamTeam.Infrastructure.Persistence;

/// <summary>
/// M7: Dapper repository'leri. Elle yazilmis SQL, hazir parametreler yok.
///
/// <para><b>GUVENLIK: parametreler HEP bindir.</b> Sorgulardaki hicbir
/// deger birleştirilmez. Bir istemci komutu ya da mac adi bu repository'ye
/// girdiğinde de <c>WHERE</c> veya <c>SET</c> icinde <c>@param</c> olarak
/// gider. Dapper bunu varsayilan yapar ve biz sorgulari elle yazdigimiz icin
/// sorgu metnini gözle de okuyabiliyoruz.</para>
///
/// <para><b>HER ASYNC METOT BAGLANTIYI KENDISI ACAR VE KAPATIR.</b> Baglanti
/// havuzdan gelir; Npgsql havuzlu baglantilari kirletmez. Bu, use case'lerin
/// baglanti yonetimiyle ugrasmasindan iyidir: use case yalnizca port bilir.</para>
/// </summary>
internal static class Db
{
    public static CommandDefinition Cmd(string sql, object? parameters = null, CancellationToken cancellationToken = default) =>
        new(sql, parameters, cancellationToken: cancellationToken);
}

public sealed class PostgresUserRepository : IUserRepository
{
    private readonly DreamTeamDatabase _database;

    public PostgresUserRepository(DreamTeamDatabase database) => _database = database;

    public async Task<UserRecord?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(Db.Cmd(
            """
            SELECT id AS Id, display_name AS DisplayName, created_at AS CreatedAt
              FROM users
             WHERE id = @id;
            """,
            new { id },
            cancellationToken)).ConfigureAwait(false);

        return row?.ToRecord();
    }

    public async Task AddAsync(UserRecord user, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(Db.Cmd(
            """
            INSERT INTO users (id, display_name, created_at)
            VALUES (@id, @displayName, @createdAt);
            """,
            new { id = user.Id, displayName = user.DisplayName, createdAt = user.CreatedAt },
            cancellationToken)).ConfigureAwait(false);
    }

    private sealed record Row(Guid Id, string DisplayName, DateTimeOffset CreatedAt)
    {
        public UserRecord ToRecord() => new(Id, DisplayName, CreatedAt);
    }
}

public sealed class PostgresPlayerRepository : IPlayerRepository
{
    private readonly DreamTeamDatabase _database;

    public PostgresPlayerRepository(DreamTeamDatabase database) => _database = database;

    public async Task<PlayerRecord?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(Db.Cmd(
            """
            SELECT id AS Id, owner_user_id AS OwnerUserId, display_name AS DisplayName,
                   position AS Position, ratings AS Ratings
              FROM players
             WHERE id = @id;
            """,
            new { id },
            cancellationToken)).ConfigureAwait(false);

        return row?.ToRecord();
    }

    public async Task<IReadOnlyList<PlayerRecord>> ListForOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        // SIRA: id ile. Roster sirasinin anlami yok; siranin her seferinde
        // ayni olmasi var, yoksa digest ve snapshot'lar kararsizlasir.
        var rows = await connection.QueryAsync<Row>(Db.Cmd(
            """
            SELECT id AS Id, owner_user_id AS OwnerUserId, display_name AS DisplayName,
                   position AS Position, ratings AS Ratings
              FROM players
             WHERE owner_user_id = @ownerUserId
             ORDER BY id;
            """,
            new { ownerUserId },
            cancellationToken)).ConfigureAwait(false);

        return [.. rows.Select(r => r.ToRecord())];
    }

    public async Task AddAsync(PlayerRecord player, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(Db.Cmd(
            """
            INSERT INTO players (id, owner_user_id, display_name, position, ratings, created_at)
            VALUES (@id, @ownerUserId, @displayName, @position, CAST(@ratings AS jsonb), now());
            """,
            new
            {
                id = player.Id,
                ownerUserId = player.OwnerUserId,
                displayName = player.DisplayName,
                position = (int)player.Position,
                ratings = RatingsCodec.Encode(player.Ratings),
            },
            cancellationToken)).ConfigureAwait(false);
    }

    private sealed record Row(
        Guid Id, Guid OwnerUserId, string DisplayName, int Position, string Ratings)
    {
        public PlayerRecord ToRecord() => new(
            Id, OwnerUserId, DisplayName, (Position)Position, RatingsCodec.Decode(Ratings));
    }
}

public sealed class PostgresTeamRepository : ITeamRepository
{
    private readonly DreamTeamDatabase _database;

    public PostgresTeamRepository(DreamTeamDatabase database) => _database = database;

    public async Task<TeamRecord?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<TeamRow>(Db.Cmd(
            """
            SELECT id AS Id, owner_user_id AS OwnerUserId, name AS Name, created_at AS CreatedAt
              FROM teams
             WHERE id = @id;
            """,
            new { id },
            cancellationToken)).ConfigureAwait(false);

        return row?.ToRecord();
    }

    public async Task<IReadOnlyList<TeamRecord>> ListForOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<TeamRow>(Db.Cmd(
            """
            SELECT id AS Id, owner_user_id AS OwnerUserId, name AS Name, created_at AS CreatedAt
              FROM teams
             WHERE owner_user_id = @ownerUserId
             ORDER BY id;
            """,
            new { ownerUserId },
            cancellationToken)).ConfigureAwait(false);

        return [.. rows.Select(r => r.ToRecord())];
    }

    public async Task AddAsync(TeamRecord team, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(Db.Cmd(
            """
            INSERT INTO teams (id, owner_user_id, name, created_at)
            VALUES (@id, @ownerUserId, @name, @createdAt);
            """,
            new { id = team.Id, ownerUserId = team.OwnerUserId, name = team.Name, createdAt = team.CreatedAt },
            cancellationToken)).ConfigureAwait(false);
    }

    public async Task AddRosterEntryAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ON CONFLICT DO NOTHING: ayni oyuncu iki kez eklenemez
        // (PRIMARY KEY) ve eklemek iki kez denenirse hata degildir.
        //
        // <b>YETKI:</b> oyuncunun ayni kullaniciya ait oldugu burada
        // denetlenmez; PK yalniz ayni takimda tekr engeller. Sahiplik
        // use case katmanindadir (T19).
        await connection.ExecuteAsync(Db.Cmd(
            """
            INSERT INTO roster_entries (team_id, player_id, added_at)
            VALUES (@teamId, @playerId, now())
            ON CONFLICT (team_id, player_id) DO NOTHING;
            """,
            new { teamId, playerId },
            cancellationToken)).ConfigureAwait(false);
    }

    public async Task RemoveRosterEntryAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(Db.Cmd(
            "DELETE FROM roster_entries WHERE team_id = @teamId AND player_id = @playerId;",
            new { teamId, playerId },
            cancellationToken)).ConfigureAwait(false);
    }

    private sealed record TeamRow(Guid Id, Guid OwnerUserId, string Name, DateTimeOffset CreatedAt)
    {
        public TeamRecord ToRecord() => new(Id, OwnerUserId, Name, CreatedAt);
    }

    public async Task<IReadOnlyList<Guid>> RosterPlayerIdsAsync(Guid teamId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        // added_at ile sirali: "kadroya eklenme sirasi" anlamlidir ve
        // ayni kiyafet esitligi (added_at) olsa bile kararli kalmasini
        // isteriz; id ikincil kural olur.
        var rows = await connection.QueryAsync<Guid>(Db.Cmd(
            """
            SELECT player_id AS "PlayerId"
              FROM roster_entries
             WHERE team_id = @teamId
             ORDER BY added_at, player_id;
            """,
            new { teamId },
            cancellationToken)).ConfigureAwait(false);

        return [.. rows];
    }
}

public sealed class PostgresMatchRepository : IMatchRepository
{
    private readonly DreamTeamDatabase _database;

    public PostgresMatchRepository(DreamTeamDatabase database) => _database = database;

    public async Task<MatchRecord?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<MatchRow>(Db.Cmd(
            """
            SELECT id AS Id, owner_user_id AS OwnerUserId, lifecycle AS Lifecycle,
                   seed AS Seed, config_hash AS ConfigHash, engine_version AS EngineVersion,
                   rules_version AS RulesVersion, setup_digest AS SetupDigest,
                   home_team_id AS HomeTeamId, away_team_id AS AwayTeamId,
                   started_at AS StartedAt, home_score AS HomeScore,
                   away_score AS AwayScore, abort_reason AS AbortReason
              FROM matches
             WHERE id = @id;
            """,
            new { id },
            cancellationToken)).ConfigureAwait(false);

        return row?.ToRecord();
    }

    public async Task AddAsync(MatchRecord match, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(Db.Cmd(
            """
            INSERT INTO matches (
                id, owner_user_id, lifecycle, seed, config_hash, engine_version,
                rules_version, setup_digest, home_team_id, away_team_id, started_at)
            VALUES (
                @id, @ownerUserId, @lifecycle, @seed, @configHash, @engineVersion,
                @rulesVersion, @setupDigest, @homeTeamId, @awayTeamId, @startedAt);
            """,
            new
            {
                id = match.Id,
                ownerUserId = match.OwnerUserId,
                lifecycle = match.Lifecycle.ToString(),
                seed = match.Seed,
                configHash = match.ConfigHash,
                engineVersion = match.EngineVersion,
                rulesVersion = match.RulesVersion,
                setupDigest = match.SetupDigest,
                homeTeamId = match.HomeTeamId,
                awayTeamId = match.AwayTeamId,
                startedAt = match.StartedAt,
            },
            cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> TryCompleteAsync(
        Guid matchId,
        MatchCompletion completion,
        CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        // <b>BU SATIR IDEMPOTENCY'NIN TAMAMIDIR.</b>
        // `WHERE lifecycle = 'Running'` olmadan UPDATE yazsaydi iki komsu
        // istek ikisini de yazardi ve odul iki kez verilirdi.
        //
        // Etkilenen satir sayisi 0 ise bu mac ZATEN terminal durumda;
        // ikinci deneme bir sey YAPMADI ve false doner.
        var affected = await connection.ExecuteAsync(Db.Cmd(
            """
            UPDATE matches
               SET lifecycle        = @lifecycle,
                   completed_at     = @completedAt,
                   home_score       = @homeScore,
                   away_score       = @awayScore,
                   is_tie           = @isTie,
                   home_possessions = @homePossessions,
                   away_possessions = @awayPossessions,
                   elapsed_game_time_ms = @elapsedGameTimeMs,
                   periods_played   = @periodsPlayed,
                   abort_reason     = @abortReason
             WHERE id = @matchId
               AND lifecycle = 'Running';
            """,
            new
            {
                matchId,
                lifecycle = completion.Status.ToString(),
                completedAt = completion.CompletedAt,
                homeScore = completion.HomeScore,
                awayScore = completion.AwayScore,
                isTie = completion.IsTie,
                homePossessions = completion.HomePossessions,
                awayPossessions = completion.AwayPossessions,
                elapsedGameTimeMs = completion.ElapsedGameTimeMs,
                periodsPlayed = completion.PeriodsPlayed,
                abortReason = completion.AbortReason,
            },
            cancellationToken)).ConfigureAwait(false);

        return affected == 1;
    }

    public async Task<int> AbortRunningAsync(string reason, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        // D113: sunucu yeniden basladi. Kurtarma YOK; iptal var.
        // completed_at ZORUNLU cunku migration'daki CHECK bunu ister.
        return await connection.ExecuteAsync(Db.Cmd(
            """
            UPDATE matches
               SET lifecycle    = 'Aborted',
                   completed_at = now(),
                   abort_reason = @reason
             WHERE lifecycle = 'Running';
            """,
            new { reason },
            cancellationToken)).ConfigureAwait(false);
    }

    private sealed record MatchRow(
        Guid Id, Guid OwnerUserId, string Lifecycle, decimal Seed, string ConfigHash,
        string EngineVersion, string RulesVersion, string SetupDigest, Guid HomeTeamId,
        Guid AwayTeamId, DateTimeOffset StartedAt, int? HomeScore, int? AwayScore,
        string? AbortReason)
    {
        public MatchRecord ToRecord() => new(
            Id, OwnerUserId, Enum.Parse<MatchLifecycle>(Lifecycle), (ulong)Seed,
            ConfigHash.TrimEnd(), EngineVersion, RulesVersion, SetupDigest.TrimEnd(),
            HomeTeamId, AwayTeamId, StartedAt, HomeScore, AwayScore, AbortReason);
    }
}
