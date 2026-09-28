using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DreamTeam.Application.Ids;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Application.UseCases;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Api.Tests;

// =====================================================================
// M7 test verisi ve sahteler.
//
// <b>BU BIRIM TESTI; VERITABANI YOK.</b> Testcontainers bu ortamda paket
// onbelleginde degil ve ag erisimi de yok (docs/plans/M7 §3). Bu yuzden
// test kumesi ikiye bolunmus durumda:
//
//   1) BU DOSYA: kimlik dogrulama (T19), yetki, hub, reconnect, HTTP sozlesmesi.
//      Tamamen bellek ici. HIZLI ve her yerde calisir.
//
//   2) PostgresIntegrationTests: migration ve UNIQUE kisitlari.
//      GERCEK PostgreSQL gerektirir ve su an KOSULDU. Bu kosul saglanana
//      kadar o testler "gecmis" sayilmaz; hic kosulmazlar.
//
// Bu ayrim bilinclidir. "Tum M7 testleri gecti" demiyorum.
// =====================================================================

/// <summary>
/// Test kumesi icin bellek ici port uygulamalari. Ayni sozlesmeyi
/// doldurur; <b>davranisi Application testlerinkinin AYNIDIR</b> ki
/// farklilik bir kod hatasini saklamasin.
/// </summary>
internal sealed class MemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, UserRecord> _rows = new();

    public Task<UserRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.TryGetValue(id, out var user) ? user : null);

    public Task AddAsync(UserRecord user, CancellationToken cancellationToken)
    {
        _rows[user.Id] = user;
        return Task.CompletedTask;
    }
}

internal sealed class MemoryPlayerRepository : IPlayerRepository
{
    private readonly ConcurrentDictionary<Guid, PlayerRecord> _rows = new();

    public Task<PlayerRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.TryGetValue(id, out var player) ? player : null);

    public Task<IReadOnlyList<PlayerRecord>> ListForOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlayerRecord>>(
            [.. _rows.Values.Where(p => p.OwnerUserId == ownerUserId)]);

    public Task AddAsync(PlayerRecord player, CancellationToken cancellationToken)
    {
        _rows[player.Id] = player;
        return Task.CompletedTask;
    }
}

internal sealed class MemoryTeamRepository : ITeamRepository
{
    private readonly ConcurrentDictionary<Guid, TeamRecord> _teams = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _rosters = new();

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
        return Task.CompletedTask;
    }

    public Task AddRosterEntryAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken)
    {
        var roster = _rosters.GetOrAdd(teamId, _ => []);

        lock (roster)
        {
            roster.Add(playerId);
        }

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
}

internal sealed class MemoryMatchRepository : IMatchRepository
{
    private readonly ConcurrentDictionary<Guid, MatchRecord> _rows = new();

    public Task<MatchRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.TryGetValue(id, out var match) ? match : null);

    public Task AddAsync(MatchRecord match, CancellationToken cancellationToken)
    {
        _rows[match.Id] = match;
        return Task.CompletedTask;
    }

    public Task<bool> TryCompleteAsync(
        Guid matchId,
        MatchCompletion completion,
        CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(matchId, out var match) || match.Lifecycle != MatchLifecycle.Running)
        {
            return Task.FromResult(false);
        }

        _rows[matchId] = match with
        {
            Lifecycle = completion.Status.ToLifecycle(),
            HomeScore = completion.HomeScore,
            AwayScore = completion.AwayScore,
            AbortReason = completion.AbortReason,
        };

        return Task.FromResult(true);
    }

    public Task<int> AbortRunningAsync(string reason, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var pair in _rows)
        {
            if (pair.Value.Lifecycle == MatchLifecycle.Running)
            {
                _rows[pair.Key] = pair.Value with
                {
                    Lifecycle = MatchLifecycle.Aborted,
                    AbortReason = reason,
                };

                count += 1;
            }
        }

        return Task.FromResult(count);
    }
}

internal sealed class MemoryEventRepository : IEventRepository
{
    private readonly ConcurrentDictionary<Guid, List<MatchEngine.Events.MatchEvent>> _rows = new();

    public Task<int> AppendRangeAsync(
        Guid matchId,
        IReadOnlyList<MatchEngine.Events.MatchEvent> events,
        CancellationToken cancellationToken)
    {
        var list = _rows.GetOrAdd(matchId, _ => []);

        lock (list)
        {
            var written = 0;

            foreach (var matchEvent in events)
            {
                // 04: (match_id, sequence) benzersiz.
                if (list.Any(e => e.Sequence == matchEvent.Sequence))
                {
                    continue;
                }

                list.Add(matchEvent);
                written += 1;
            }

            return Task.FromResult(written);
        }
    }

    public Task<IReadOnlyList<MatchEngine.Events.MatchEvent>> ReadAfterAsync(
        Guid matchId,
        long afterSequence,
        int maxCount,
        CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(matchId, out var list))
        {
            return Task.FromResult<IReadOnlyList<MatchEngine.Events.MatchEvent>>([]);
        }

        lock (list)
        {
            return Task.FromResult<IReadOnlyList<MatchEngine.Events.MatchEvent>>(
                [.. list.Where(e => e.Sequence > afterSequence).Take(maxCount)]);
        }
    }
}

internal sealed class MemoryCommandLog : ICommandLogRepository
{
    private readonly ConcurrentDictionary<(Guid, Guid), bool> _accepted = new();

    public Task<bool> TryRecordAcceptedAsync(
        Guid matchId,
        Guid commandId,
        Guid ownerUserId,
        ManagerCommandKind kind,
        CommandBoundary boundary,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken) =>
        Task.FromResult(_accepted.TryAdd((matchId, commandId), true));

    public Task RecordResultAsync(
        Guid matchId,
        Guid commandId,
        bool applied,
        CommandRejectionReason? rejectionReason,
        string? message,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// API'nin dondugu oyuncu sekli. <b>Alan adlari kucuk harfli</b> (web
/// serializer); testler bu kaydi kullandigi icin ayri bir kayit degil,
/// sunucunun gercek sozlesmesidir.
/// </summary>
internal sealed record PlayerResponse(
    Guid Id, string DisplayName, Domain.Players.Position Position, Domain.Players.PlayerRatings Ratings);

/// <summary>M7 API test verisi.</summary>
internal static class ApiTestData
{
    public const string SigningKey = "m7-test-signing-key-en-az-32-bayt-olmali-1234";

    public const string IssuerName = "dreamteam-test";
    public const string Audience = "dreamteam-test-client";

    public static readonly Guid Alice = DerivedId.From("user", "alice");
    public static readonly Guid Bob = DerivedId.From("user", "bob");

    public static Infrastructure.Security.JwtOptions Jwt() => new()
    {
        Issuer = IssuerName,
        Audience = Audience,
        SigningKey = SigningKey,
        Lifetime = TimeSpan.FromHours(1),
    };

    public static Infrastructure.Security.JwtTokenIssuer TokenIssuer() =>
        new(Jwt());

    /// <summary>
    /// M6'nin KALIBRE belgesi. API testleri de ayni belgeyi kullanir;
    /// kalibresiz bir motoru test etmek anlamsiz olurdu (D103).
    /// </summary>
    public static EngineConfig Config()
    {
        var path = Path.Combine(RepositoryRoot, "config", "engine", "baseline.v0.1.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Balance belgesi bulunamadi: {path}. API testleri belgeye baglidir.", path);
        }

        return BalanceConfigStore.Load(path).ToEngineConfig();
    }

    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DreamTeam.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Repo koku bulunamadi (DreamTeam.slnx).");
        }
    }

    public static PlayerRatings Ratings(int value = 74) => new()
    {
        Speed = value, Strength = value, Vertical = value, Stamina = value,
        Inside = value, MidRange = value, ThreePoint = value, FreeThrow = value,
        BallHandling = value, Passing = value, OffBall = value, PostOffense = value,
        PerimeterDefense = value, InteriorDefense = value, Steal = value,
        Block = value, Rebounding = value, BasketballIQ = value,
    };
}
