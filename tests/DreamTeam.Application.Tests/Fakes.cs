using System.Collections.Concurrent;
using DreamTeam.Application.Ports;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.Application.Tests;

/// <summary>
/// M7 test sahteleri.
///
/// <para><b>Bunlar kalici bir sistem DEGILDIR.</b> Amaclari tek tek:
/// bir kuralin <i>neyi bozdugunu</i> gormek. Bu yuzden her sahte en az bir
/// yerde "kusurlu davranis" moduna girebilir ve o modu <i>bilerek</i>
/// kullanilan testler vardir. Aksi halde sahte, testin konu oldugu davranisi
/// degistirir ve test yeil gecer.</para>
///
/// <para><b>Bunlar motor DEGILDIR.</b> Motor her zaman gercek
/// <c>MatchSimulation</c>'dir; sahteler yalniz dis dunya (saat, bekleme,
/// depo) yerine geçer.</para>
/// </summary>
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, UserRecord> _users = new();

    public int WriteCount { get; private set; }

    /// <summary>Ayni id ile ikinci yazma denemesi <b>basarisiz</b> olur.</summary>
    public bool RejectDuplicates { get; set; } = true;

    public Task<UserRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);

    public Task AddAsync(UserRecord user, CancellationToken cancellationToken)
    {
        if (RejectDuplicates && !_users.TryAdd(user.Id, user))
        {
            throw new InvalidOperationException($"Duplicate user {user.Id}.");
        }

        WriteCount += 1;
        return Task.CompletedTask;
    }

    public void Seed(UserRecord user) => _users[user.Id] = user;
}

internal sealed class InMemoryPlayerRepository : IPlayerRepository
{
    private readonly ConcurrentDictionary<Guid, PlayerRecord> _players = new();

    public int WriteCount { get; private set; }

    public Task<PlayerRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_players.TryGetValue(id, out var player) ? player : null);

    public Task<IReadOnlyList<PlayerRecord>> ListForOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlayerRecord>>(
            [.. _players.Values.Where(p => p.OwnerUserId == ownerUserId)]);

    public Task AddAsync(PlayerRecord player, CancellationToken cancellationToken)
    {
        _players[player.Id] = player;
        WriteCount += 1;
        return Task.CompletedTask;
    }

    public int CountFor(Guid ownerUserId) => _players.Values.Count(p => p.OwnerUserId == ownerUserId);
}

internal sealed class InMemoryTeamRepository : ITeamRepository
{
    private readonly ConcurrentDictionary<Guid, TeamRecord> _teams = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _rosters = new();

    public int WriteCount { get; private set; }

    public Task<TeamRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_teams.TryGetValue(id, out var team) ? team : null);

    public Task<IReadOnlyList<TeamRecord>> ListForOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TeamRecord>>(
            [.. _teams.Values.Where(t => t.OwnerUserId == ownerUserId)]);

    public Task AddAsync(TeamRecord team, CancellationToken cancellationToken)
    {
        _teams[team.Id] = team;
        _rosters.TryAdd(team.Id, []);
        WriteCount += 1;
        return Task.CompletedTask;
    }

    public Task AddRosterEntryAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken)
    {
        var roster = _rosters.GetOrAdd(teamId, _ => []);

        lock (roster)
        {
            roster.Add(playerId);
        }

        WriteCount += 1;
        return Task.CompletedTask;
    }

    public Task RemoveRosterEntryAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken)
    {
        if (_rosters.TryGetValue(teamId, out var roster))
        {
            lock (roster)
            {
                roster.Remove(playerId);
            }
        }

        WriteCount += 1;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> RosterPlayerIdsAsync(Guid teamId, CancellationToken cancellationToken)
    {
        if (!_rosters.TryGetValue(teamId, out var roster))
        {
            return Task.FromResult<IReadOnlyList<Guid>>([]);
        }

        lock (roster)
        {
            return Task.FromResult<IReadOnlyList<Guid>>([.. roster]);
        }
    }

    public void Seed(TeamRecord team, params Guid[] playerIds)
    {
        _teams[team.Id] = team;
        _rosters[team.Id] = [.. playerIds];
    }
}

internal sealed class InMemoryMatchRepository : IMatchRepository
{
    private readonly ConcurrentDictionary<Guid, MatchRecord> _matches = new();

    public int CompletionWrites { get; private set; }

    public int AddCalls { get; private set; }

    /// <summary>
    /// <b>KUSURLU MOD.</b> <c>true</c> dondurup yazmaz. 03 "kosullu guncelle"
    /// dediginde sunucunun YANLIS yapan hali budur; idempotency testi bunu
    /// kullanarak duzgun halin farkini gorur.
    /// </summary>
    public bool IgnoreLifecycleGuard { get; set; }

    public Task<MatchRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_matches.TryGetValue(id, out var match) ? match : null);

    public Task AddAsync(MatchRecord match, CancellationToken cancellationToken)
    {
        AddCalls += 1;
        _matches[match.Id] = match;
        return Task.CompletedTask;
    }

    public Task<bool> TryCompleteAsync(
        Guid matchId,
        MatchCompletion completion,
        CancellationToken cancellationToken)
    {
        if (IgnoreLifecycleGuard)
        {
            CompletionWrites += 1;
            return Task.FromResult(true);
        }

        if (!_matches.TryGetValue(matchId, out var match) || match.Lifecycle != MatchLifecycle.Running)
        {
            // Zaten tamamlanmis: BIR SEY YAPMA.
            return Task.FromResult(false);
        }

        _matches[matchId] = match with
        {
            Lifecycle = completion.Status.ToLifecycle(),
            HomeScore = completion.HomeScore,
            AwayScore = completion.AwayScore,
            AbortReason = completion.AbortReason,
        };

        CompletionWrites += 1;
        return Task.FromResult(true);
    }

    public Task<int> AbortRunningAsync(string reason, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var pair in _matches)
        {
            if (pair.Value.Lifecycle == MatchLifecycle.Running)
            {
                _matches[pair.Key] = pair.Value with
                {
                    Lifecycle = MatchLifecycle.Aborted,
                    AbortReason = reason,
                };

                count += 1;
            }
        }

        return Task.FromResult(count);
    }

    public int RunningCount =>
        _matches.Values.Count(m => m.Lifecycle == MatchLifecycle.Running);

    public void Seed(MatchRecord match) => _matches[match.Id] = match;
}

internal sealed class InMemoryEventRepository : IEventRepository
{
    private readonly ConcurrentDictionary<Guid, List<MatchEvent>> _events = new();

    /// <summary>
    /// <b>KUSURLU MOD.</b> <c>true</c> dondurup yazmaz. Idempotency testi bunu
    /// kullanarak dogru davranisin farkini gorur.
    /// </summary>
    public bool IgnoreUniqueConstraint { get; set; }

    public int AppendCalls { get; private set; }

    public Task<int> AppendRangeAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken)
    {
        AppendCalls += 1;

        if (IgnoreUniqueConstraint)
        {
            return Task.FromResult(events.Count);
        }

        var list = _events.GetOrAdd(matchId, _ => []);

        lock (list)
        {
            var written = 0;

            foreach (var matchEvent in events)
            {
                // 04: (match_id, sequence) benzersiz.
                if (list.Any(existing => existing.Sequence == matchEvent.Sequence))
                {
                    continue;
                }

                list.Add(matchEvent);
                written += 1;
            }

            return Task.FromResult(written);
        }
    }

    public Task<IReadOnlyList<MatchEvent>> ReadAfterAsync(
        Guid matchId,
        long afterSequence,
        int maxCount,
        CancellationToken cancellationToken)
    {
        if (!_events.TryGetValue(matchId, out var list))
        {
            return Task.FromResult<IReadOnlyList<MatchEvent>>([]);
        }

        lock (list)
        {
            return Task.FromResult<IReadOnlyList<MatchEvent>>(
                [.. list.Where(e => e.Sequence > afterSequence).Take(maxCount)]);
        }
    }

    public int CountFor(Guid matchId) => _events.TryGetValue(matchId, out var list) ? list.Count : 0;
}

internal sealed class InMemoryCommandLogRepository : ICommandLogRepository
{
    private readonly ConcurrentDictionary<(Guid MatchId, Guid CommandId), LogEntry> _entries = new();

    private record LogEntry(
        Guid OwnerUserId,
        ManagerCommandKind Kind,
        CommandBoundary Boundary,
        DateTimeOffset CreatedAt,
        bool Applied,
        CommandRejectionReason? Reason,
        string? Message,
        DateTimeOffset? AppliedAt);

    public int AcceptedCalls { get; private set; }

    public int ResultCalls { get; private set; }

    public Task<bool> TryRecordAcceptedAsync(
        Guid matchId,
        Guid commandId,
        Guid ownerUserId,
        ManagerCommandKind kind,
        CommandBoundary boundary,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        AcceptedCalls += 1;

        var added = _entries.TryAdd(
            (matchId, commandId),
            new LogEntry(ownerUserId, kind, boundary, createdAt, false, null, null, null));

        return Task.FromResult(added);
    }

    public Task RecordResultAsync(
        Guid matchId,
        Guid commandId,
        bool applied,
        CommandRejectionReason? rejectionReason,
        string? message,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken)
    {
        ResultCalls += 1;

        if (_entries.TryGetValue((matchId, commandId), out var entry))
        {
            _entries[(matchId, commandId)] = entry with
            {
                Applied = applied,
                Reason = rejectionReason,
                Message = message,
                AppliedAt = appliedAt,
            };
        }

        return Task.CompletedTask;
    }

    public bool Has(Guid matchId, Guid commandId) => _entries.ContainsKey((matchId, commandId));

    /// <summary>Bir maca ait kayitli komutlar. Idempotency testleri icin.</summary>
    public IReadOnlyList<Guid> CommandsFor(Guid matchId) =>
        [.. _entries.Keys.Where(k => k.MatchId == matchId).Select(k => k.CommandId)];

    /// <summary>Komutun guncel kaydi: uygulandi mi, red mi?</summary>
    public bool WasApplied(Guid matchId, Guid commandId) =>
        _entries.TryGetValue((matchId, commandId), out var entry) && entry.Applied;
}

internal sealed class StubConfigProvider : IMatchConfigProvider
{
    private readonly EngineConfig _config;

    public StubConfigProvider(EngineConfig config) => _config = config;

    public string Hash { get; set; } = "stub-config-hash";

    public EngineConfig GetConfig(out string configHash)
    {
        configHash = Hash;
        return _config;
    }
}

internal sealed class FixedCurrentUser : ICurrentUser
{
    public FixedCurrentUser(Guid userId) => UserId = userId;

    public Guid UserId { get; set; }
}

/// <summary>
/// <b>Yayin sayaci.</b> Sunucunun istemciye gonderdigi event sayisini olcer.
/// Application testleri SignalR bilmedigi icin bu portun saglayicisidir.
/// </summary>
internal sealed class RecordingBroadcaster : IMatchBroadcaster
{
    public int EventBatches { get; private set; }

    public int EventsPublished { get; private set; }

    public List<Guid> PublishedMatchIds { get; } = [];

    public Task PublishEventsAsync(
        Guid matchId,
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken)
    {
        EventBatches += 1;
        EventsPublished += events.Count;
        PublishedMatchIds.Add(matchId);
        return Task.CompletedTask;
    }

    public Task PublishCommandResultAsync(
        Guid matchId,
        Guid commandId,
        bool applied,
        string? reason,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
