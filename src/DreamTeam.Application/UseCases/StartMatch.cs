using System.Collections.Immutable;
using DreamTeam.Application.Ids;
using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.Application.Setup;
using DreamTeam.Domain.Players;
using DreamTeam.Domain.Teams;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.Application.UseCases;

/// <summary>M7: baslatma icin dondurulmus girdi. 04: "Setup snapshot/version".</summary>
public sealed record FrozenMatchSetup(
    MatchSetup Setup,
    string SetupDigest,
    string ConfigHash,
    string EngineVersion,
    string RulesVersion);

public sealed record StartMatchResult(Guid MatchId, FrozenMatchSetup Frozen);

/// <summary>
/// M7: AI mac baslatir (D112: PvP yok).
///
/// <para><b>Setup DONDURULUR.</b> 04 §"Veri butunlugu": "Macta kullanilan
/// snapshot, history'nin canli sorgusundan bagimsizdir." Mac basladiktan sonra
/// kadro degisse bile o mac degismez.</para>
/// </summary>
public sealed class StartMatch
{
    private readonly ITeamRepository _teams;
    private readonly IPlayerRepository _players;
    private readonly IMatchRepository _matches;
    private readonly IMatchConfigProvider _config;
    private readonly ISetupDigest _digest;
    private readonly MatchSessionStore _sessions;
    private readonly ICurrentUser _current;
    private readonly IMonotonicClock _clock;

    public StartMatch(
        ITeamRepository teams,
        IPlayerRepository players,
        IMatchRepository matches,
        IMatchConfigProvider config,
        ISetupDigest digest,
        MatchSessionStore sessions,
        ICurrentUser current,
        IMonotonicClock clock)
    {
        _teams = teams;
        _players = players;
        _matches = matches;
        _config = config;
        _digest = digest;
        _sessions = sessions;
        _current = current;
        _clock = clock;
    }

    public async Task<UseCaseResult<StartMatchResult>> ExecuteAsync(
        Guid homeTeamId,
        Guid awayTeamId,
        ulong seed,
        IReadOnlyList<Guid> homeLineup,
        IReadOnlyList<Guid> awayLineup,
        CancellationToken cancellationToken)
    {
        MatchIdGuard.Require(homeTeamId);
        MatchIdGuard.Require(awayTeamId);

        if (homeTeamId == awayTeamId)
        {
            return UseCaseResult<StartMatchResult>.Fail("Iki takim ayni olamaz.");
        }

        var home = await LoadOwnedTeamAsync(homeTeamId, cancellationToken).ConfigureAwait(false);

        if (home is null)
        {
            return UseCaseResult<StartMatchResult>.Fail(
                $"Ev takimi bulunamadi veya size ait degil: {homeTeamId}.");
        }

        var away = await LoadOwnedTeamAsync(awayTeamId, cancellationToken).ConfigureAwait(false);

        if (away is null)
        {
            return UseCaseResult<StartMatchResult>.Fail(
                $"Depasman takimi bulunamadi veya size ait degil: {awayTeamId}.");
        }

        var homeTeamSetup = await BuildTeamSetupAsync(home, homeLineup, cancellationToken)
            .ConfigureAwait(false);

        if (homeTeamSetup is null)
        {
            return UseCaseResult<StartMatchResult>.Fail("Ev lineup'i gecersiz.");
        }

        var awayTeamSetup = await BuildTeamSetupAsync(away, awayLineup, cancellationToken)
            .ConfigureAwait(false);

        if (awayTeamSetup is null)
        {
            return UseCaseResult<StartMatchResult>.Fail("Depasman lineup'i gecersiz.");
        }

        var config = _config.GetConfig(out var configHash);
        var matchId = DerivedId.From("match", homeTeamId.ToString(), awayTeamId.ToString(), seed.ToString());

        var setup = new MatchSetup
        {
            MatchId = matchId,
            Home = homeTeamSetup,
            Away = awayTeamSetup,
            Seed = seed,
            Engine = new EngineIdentity
            {
                EngineVersion = EngineVersion.Current,
                RulesVersion = RulesIdentity.Current,
                BalanceConfigHash = configHash,
                RngAlgorithm = RngIdentity.Algorithm,
                RngVersion = RngIdentity.Version,
            },
        };

        var validation = MatchSetupValidator.Validate(setup);

        if (!validation.IsValid)
        {
            return UseCaseResult<StartMatchResult>.Fail(
                "Gecersiz mac girdisi: "
                + string.Join("; ", validation.Errors.Select(error => $"{error.Field} ({error.Code})")));
        }

        var frozen = new FrozenMatchSetup(setup, _digest.Of(setup), configHash, EngineVersion.Current, RulesIdentity.Current);

        await _matches.AddAsync(
            new MatchRecord(
                matchId,
                _current.UserId,
                MatchLifecycle.Running,
                seed,
                configHash,
                EngineVersion.Current,
                RulesIdentity.Current,
                frozen.SetupDigest,
                homeTeamId,
                awayTeamId,
                DateTimeOffset.FromUnixTimeMilliseconds(_clock.ElapsedMilliseconds),
                HomeScore: null,
                AwayScore: null,
                AbortReason: null),
            cancellationToken).ConfigureAwait(false);

        // Oturum YALNIZCA burada olusur ve GetOrAdd ile bir kez.
        _sessions.GetOrAdd(matchId, () => new MatchSession(matchId, _current.UserId, setup, config));

        return UseCaseResult<StartMatchResult>.Ok(new StartMatchResult(matchId, frozen));
    }

    private async Task<TeamRecord?> LoadOwnedTeamAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await _teams.FindAsync(teamId, cancellationToken).ConfigureAwait(false);

        if (team is null || team.OwnerUserId != _current.UserId)
        {
            return null;
        }

        return team;
    }

    private async Task<TeamMatchSetup?> BuildTeamSetupAsync(
        TeamRecord team,
        IReadOnlyList<Guid> lineup,
        CancellationToken cancellationToken)
    {
        if (lineup.Count != 5)
        {
            return null;
        }

        var rosterIds = await _teams.RosterPlayerIdsAsync(team.Id, cancellationToken).ConfigureAwait(false);
        var roster = new List<Player>();
        var wanted = lineup.ToHashSet();

        foreach (var id in rosterIds)
        {
            var record = await _players.FindAsync(id, cancellationToken).ConfigureAwait(false);

            if (record is null)
            {
                return null;
            }

            roster.Add(new Player
            {
                Id = record.Id,
                DisplayName = record.DisplayName,
                Position = record.Position,
                Ratings = record.Ratings,
            });
        }

        if (!wanted.IsSubsetOf(rosterIds.ToHashSet()))
        {
            return null;
        }

        return new TeamMatchSetup
        {
            Team = new Team { Id = team.Id, Name = team.Name, Roster = [.. roster] },
            Lineup = new Lineup { PlayerIds = [.. lineup] },
            Offensive = OffensiveTactic.Balanced,
            Defense = DefensiveTactic.ManToMan,
            Pace = Pace.Normal,
        };
    }
}

