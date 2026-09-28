using Dapper;
using DreamTeam.Application.Ports;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.Infrastructure.Persistence;

public sealed class PostgresEventRepository : IEventRepository
{
    private readonly DreamTeamDatabase _database;

    public PostgresEventRepository(DreamTeamDatabase database) => _database = database;

    public async Task<int> AppendRangeAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return 0;
        }

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ON CONFLICT (match_id, sequence) DO NOTHING:
        // 04 "(match_id, sequence) benzersizdir." Yeniden baglanan istemci
        // ayni araligi tekrar isteyebilir; bu bir hata DEGIL. Yazilan
        // satir sayisi dondurulur ve ikinci yazma 0 verir.
        //
        // <b>TEK BAGLANTI, TEK ISLEM:</b> 1264 event'i tek tek ayni
        // baglantida yaziyoruz. Her event icin ayri baglanti acsaydi bir
        // mac 1264 gidis-donus olurdu.
        var written = 0;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var matchEvent in events)
        {
            written += await connection.ExecuteAsync(Db.Cmd(
                """
                INSERT INTO match_events (
                    match_id, sequence, schema_version, engine_version, config_hash,
                    type, period, game_clock_ms, elapsed_game_time_ms,
                    possession_id, action_id, team_id, player_id, secondary_player_id,
                    payload)
                VALUES (
                    @matchId, @sequence, @schemaVersion, @engineVersion, @configHash,
                    @type, @period, @gameClockMs, @elapsedGameTimeMs,
                    @possessionId, @actionId, @teamId, @playerId, @secondaryPlayerId,
                    CAST(@payload AS jsonb))
                ON CONFLICT (match_id, sequence) DO NOTHING;
                """,
                new
                {
                    matchId = matchEvent.MatchId,
                    sequence = matchEvent.Sequence,
                    schemaVersion = matchEvent.SchemaVersion,
                    engineVersion = matchEvent.EngineVersion,
                    configHash = matchEvent.ConfigHash,
                    type = (int)matchEvent.Type,
                    period = matchEvent.Period,
                    gameClockMs = matchEvent.GameClockMs,
                    elapsedGameTimeMs = matchEvent.ElapsedGameTimeMs,
                    possessionId = matchEvent.PossessionId,
                    actionId = matchEvent.ActionId,
                    teamId = matchEvent.TeamId is null ? (int?)null : (int)matchEvent.TeamId.Value,
                    playerId = matchEvent.PlayerId,
                    secondaryPlayerId = matchEvent.SecondaryPlayerId,
                    payload = MatchEventCodec.EncodePayload(matchEvent),
                },
                cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return written;
    }

    public async Task<IReadOnlyList<MatchEvent>> ReadAfterAsync(
        Guid matchId,
        long afterSequence,
        int maxCount,
        CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<Row>(Db.Cmd(
            """
            SELECT match_id AS MatchId, sequence AS Sequence, schema_version AS SchemaVersion,
                   engine_version AS EngineVersion, config_hash AS ConfigHash,
                   type AS Type, period AS Period, game_clock_ms AS GameClockMs,
                   elapsed_game_time_ms AS ElapsedGameTimeMs, possession_id AS PossessionId,
                   action_id AS ActionId, team_id AS TeamId, player_id AS PlayerId,
                   secondary_player_id AS SecondaryPlayerId, payload AS Payload
              FROM match_events
             WHERE match_id = @matchId
               AND sequence > @afterSequence
             ORDER BY sequence
             LIMIT @maxCount;
            """,
            new { matchId, afterSequence, maxCount },
            cancellationToken)).ConfigureAwait(false);

        return [.. rows.Select(r => r.ToEvent())];
    }

    private sealed record Row(
        Guid MatchId, long Sequence, int SchemaVersion, string EngineVersion,
        string ConfigHash, int Type, short Period, long GameClockMs, long ElapsedGameTimeMs,
        int? PossessionId, long? ActionId, byte? TeamId, Guid? PlayerId,
        Guid? SecondaryPlayerId, string Payload)
    {
        public MatchEvent ToEvent() => MatchEventCodec.ToEvent(
            MatchId, Sequence, SchemaVersion, EngineVersion, ConfigHash.TrimEnd(),
            Type, Period, GameClockMs, ElapsedGameTimeMs, PossessionId, ActionId,
            TeamId, PlayerId, SecondaryPlayerId, Payload);
    }
}

public sealed class PostgresCommandLogRepository : ICommandLogRepository
{
    private readonly DreamTeamDatabase _database;

    public PostgresCommandLogRepository(DreamTeamDatabase database) => _database = database;

    public async Task<bool> TryRecordAcceptedAsync(
        Guid matchId,
        Guid commandId,
        Guid ownerUserId,
        ManagerCommandKind kind,
        CommandBoundary boundary,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        // 07 §5: "Ayni CommandId yeniden gelirse ayni sonuc dondurulur."
        // PRIMARY KEY (match_id, command_id) bunu garanti eder; ON CONFLICT
        // DO NOTHING + RETURNING, ikinci gonderimde 0 satir doner.
        var inserted = await connection.ExecuteScalarAsync<int>(Db.Cmd(
            """
            INSERT INTO manager_commands (match_id, command_id, owner_user_id, kind, boundary, created_at)
            VALUES (@matchId, @commandId, @ownerUserId, @kind, @boundary, @createdAt)
            ON CONFLICT (match_id, command_id) DO NOTHING
            RETURNING 1;
            """,
            new
            {
                matchId, commandId, ownerUserId,
                kind = (int)kind, boundary = (int)boundary, createdAt,
            },
            cancellationToken)).ConfigureAwait(false);

        return inserted == 1;
    }

    public async Task RecordResultAsync(
        Guid matchId,
        Guid commandId,
        bool applied,
        CommandRejectionReason? rejectionReason,
        string? message,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(Db.Cmd(
            """
            UPDATE manager_commands
               SET applied = @applied,
                   rejection_reason = @rejectionReason,
                   message = @message,
                   applied_at = @appliedAt
             WHERE match_id = @matchId
               AND command_id = @commandId;
            """,
            new
            {
                matchId, commandId, applied,
                rejectionReason = rejectionReason is null ? (int?)null : (int)rejectionReason.Value,
                message, appliedAt,
            },
            cancellationToken)).ConfigureAwait(false);
    }
}
