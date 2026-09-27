using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Projection;

/// <summary>Projector çıktısı: iki takım toplamı ve tüm oyuncu satırları.</summary>
public sealed record ProjectionResult(
    TeamBoxScore Home,
    TeamBoxScore Away,
    ImmutableArray<PlayerBoxScore> Players);

/// <summary>
/// Event akışından istatistik üreten tek yazıcı. 07 §3: "Box score projector tek
/// canonical settlement üzerinden yazar."
///
/// Projector motorun anlık skor durumunu <b>okumaz</b>; yalnız event'leri tüketir.
/// Böylece iki bağımsız yolun (motorun kendi sayacı ve event türevi istatistik)
/// eşleşmesi test edilebilir olur.
///
/// Tüketilen eventler:
/// <list type="bullet">
///   <item><c>ShotMade</c> → FGM, 2PM/3PM, puan, asist</item>
///   <item><c>ShotMissed</c> → FGA, 2PA/3PA</item>
///   <item><c>Turnover</c> → TOV</item>
///   <item><c>Rebound</c> → OREB/DREB; takım ribaundu ayrı sayılır</item>
/// </list>
/// <c>ShotAttempt</c> hiçbir sayaç yazmaz. FGA sayılabilirliği payload'daki
/// <c>CountsAsFieldGoalAttempt</c> bayrağıyla belirlenir; M2'de bu bayrak daima
/// true'dur ve false olan şut M3'te (kaçan shooting foul) mümkün olacaktır.
/// </summary>
public sealed class BoxScoreProjector
{
    private readonly MatchSetup _setup;
    private readonly Dictionary<Guid, Player> _definitions = [];
    private readonly Dictionary<Guid, Tally> _players = new();
    private readonly Dictionary<TeamSide, Tally> _teams = new();

    public BoxScoreProjector(MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _setup = setup;

        foreach (var player in setup.Home.Team.Roster)
        {
            _definitions[player.Id] = player;
        }

        foreach (var player in setup.Away.Team.Roster)
        {
            _definitions[player.Id] = player;
        }
    }

    public ProjectionResult Project(IReadOnlyList<MatchEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var matchEvent in events)
        {
            switch (matchEvent.Type)
            {
                case MatchEventType.ShotMade:
                    CountMade(matchEvent);
                    break;

                case MatchEventType.ShotMissed:
                    CountMissed(matchEvent);
                    break;

                case MatchEventType.Turnover:
                    CountTurnover(matchEvent);
                    break;

                case MatchEventType.Rebound:
                    CountRebound(matchEvent);
                    break;

                case MatchEventType.Foul:
                    CountFoul(matchEvent);
                    break;

                case MatchEventType.FreeThrowAttempt:
                    CountFreeThrowAttempt(matchEvent);
                    break;

                case MatchEventType.FreeThrowMade:
                    CountFreeThrowMade(matchEvent);
                    break;

                case MatchEventType.Block:
                    CountBlock(matchEvent);
                    break;
            }
        }

        return new ProjectionResult(
            BuildTeamBoxScore(TeamSide.Home),
            BuildTeamBoxScore(TeamSide.Away),
            [.. _players.Values
                .OrderBy(tally => tally.PlayerId)
                .Select(BuildPlayerBoxScore)]);
    }

    private void CountMade(MatchEvent matchEvent)
    {
        var made = matchEvent.PayloadAs<ShotMadePayload>();
        var side = Side(matchEvent.TeamId, "ShotMade");
        var team = Team(side);

        team.Points += made.Points;

        if (!made.CountsAsFieldGoalAttempt)
        {
            // M2'de oluşmaz. Yine de puan skora girer, deneme sayacına girmez.
            return;
        }

        var shooter = Shooter(matchEvent.PlayerId, side);
        var isThree = made.ShotType == ShotType.ThreePoint;

        shooter.Made += 1;
        shooter.Attempts += 1;
        shooter.Points += made.Points;
        team.Made += 1;
        team.Attempts += 1;

        // İsabetli şut bir denemedir: 3PA yalnız kaçanlarda sayılırsa
        // "3PM <= 3PA" invariant'ı bozulur (örn. 4-0). İki sayacı birlikte yürüt.
        if (isThree)
        {
            shooter.ThreeMade += 1;
            shooter.ThreeAttempts += 1;
            team.ThreeMade += 1;
            team.ThreeAttempts += 1;
        }
        else
        {
            shooter.TwoMade += 1;
            shooter.TwoAttempts += 1;
            team.TwoMade += 1;
            team.TwoAttempts += 1;
        }

        if (matchEvent.SecondaryPlayerId is { } assisterId && assisterId != matchEvent.PlayerId)
        {
            PlayerTally(assisterId, side).Assists += 1;
            team.Assists += 1;
        }
    }

    private void CountMissed(MatchEvent matchEvent)
    {
        var missed = matchEvent.PayloadAs<ShotMissedPayload>();
        var side = Side(matchEvent.TeamId, "ShotMissed");
        var team = Team(side);

        if (!missed.CountsAsFieldGoalAttempt)
        {
            return;
        }

        var shooter = Shooter(matchEvent.PlayerId, side);
        var isThree = missed.ShotType == ShotType.ThreePoint;

        shooter.Attempts += 1;
        team.Attempts += 1;

        if (isThree)
        {
            shooter.ThreeAttempts += 1;
            team.ThreeAttempts += 1;
        }
        else
        {
            shooter.TwoAttempts += 1;
            team.TwoAttempts += 1;
        }
    }

    private void CountTurnover(MatchEvent matchEvent)
    {
        var side = Side(matchEvent.TeamId, "Turnover");

        Shooter(matchEvent.PlayerId, side).Turnovers += 1;
        Team(side).Turnovers += 1;
    }

    private void CountRebound(MatchEvent matchEvent)
    {
        var rebound = matchEvent.PayloadAs<ReboundPayload>();
        var side = Side(matchEvent.TeamId, "Rebound");
        var team = Team(side);

        if (rebound.IsTeamRebound)
        {
            team.TeamRebounds += 1;
            return;
        }

        var rebounder = Shooter(matchEvent.PlayerId, side);

        if (rebound.Offensive)
        {
            rebounder.OffensiveRebounds += 1;
            team.OffensiveRebounds += 1;
        }
        else
        {
            rebounder.DefensiveRebounds += 1;
            team.DefensiveRebounds += 1;
        }
    }

    private void CountFoul(MatchEvent matchEvent)
    {
        var foul = matchEvent.PayloadAs<FoulPayload>();
        var side = Side(matchEvent.TeamId, "Foul");

        if (foul.Type == FoulType.Offensive)
        {
            // 06 §88: hucum faulu savunma bonusunu tetiklemez.
            return;
        }

        Shooter(matchEvent.PlayerId, side).PersonalFouls += 1;
        Team(side).PersonalFouls += 1;
    }

    private void CountFreeThrowAttempt(MatchEvent matchEvent)
    {
        var attempt = matchEvent.PayloadAs<FreeThrowAttemptPayload>();
        var side = Side(matchEvent.TeamId, "FreeThrowAttempt");
        var shooter = Shooter(matchEvent.PlayerId, side);

        shooter.FreeThrowAttempts += 1;
        Team(side).FreeThrowAttempts += 1;
    }

    private void CountFreeThrowMade(MatchEvent matchEvent)
    {
        var side = Side(matchEvent.TeamId, "FreeThrowMade");
        var shooter = Shooter(matchEvent.PlayerId, side);
        var team = Team(side);

        shooter.FreeThrowMakes += 1;
        team.FreeThrowMakes += 1;

        // Serbest atis puani ayrica sayilir: 08 §2 skor = 2*2PM + 3*3PM + FTM.
        shooter.Points += 1;
        team.Points += 1;
    }

    private void CountBlock(MatchEvent matchEvent)
    {
        // 07 §3: blok ikinci FGA yazmaz ve yeni miss uretmez; yalnizca blok
        // sayacini artirir. Miss zaten ShotMissed ile yazildi.
        var side = Side(matchEvent.TeamId, "Block");

        Shooter(matchEvent.PlayerId, side).Blocks += 1;
        Team(side).Blocks += 1;
    }

    private TeamBoxScore BuildTeamBoxScore(TeamSide side)
    {
        var tally = Team(side);

        return new TeamBoxScore
        {
            Team = side,
            TeamName = side == TeamSide.Home ? _setup.Home.Team.Name : _setup.Away.Team.Name,
            FieldGoalsMade = tally.Made,
            FieldGoalsAttempted = tally.Attempts,
            TwoPointersMade = tally.TwoMade,
            TwoPointersAttempted = tally.TwoAttempts,
            ThreePointersMade = tally.ThreeMade,
            ThreePointersAttempted = tally.ThreeAttempts,
            Assists = tally.Assists,
            Turnovers = tally.Turnovers,
            PersonalFouls = tally.PersonalFouls,
            FreeThrowAttempts = tally.FreeThrowAttempts,
            FreeThrowMakes = tally.FreeThrowMakes,
            Blocks = tally.Blocks,
            OffensiveRebounds = tally.OffensiveRebounds,
            DefensiveRebounds = tally.DefensiveRebounds,
            TeamRebounds = tally.TeamRebounds,
            Points = tally.Points,
        };
    }

    private PlayerBoxScore BuildPlayerBoxScore(Tally tally)
    {
        var definition = _definitions[tally.PlayerId];

        return new PlayerBoxScore
        {
            PlayerId = tally.PlayerId,
            DisplayName = definition.DisplayName,
            Position = definition.Position,
            Team = tally.Team,
            FieldGoalsMade = tally.Made,
            FieldGoalsAttempted = tally.Attempts,
            TwoPointersMade = tally.TwoMade,
            TwoPointersAttempted = tally.TwoAttempts,
            ThreePointersMade = tally.ThreeMade,
            ThreePointersAttempted = tally.ThreeAttempts,
            Assists = tally.Assists,
            Turnovers = tally.Turnovers,
            PersonalFouls = tally.PersonalFouls,
            FreeThrowAttempts = tally.FreeThrowAttempts,
            FreeThrowMakes = tally.FreeThrowMakes,
            Blocks = tally.Blocks,
            OffensiveRebounds = tally.OffensiveRebounds,
            DefensiveRebounds = tally.DefensiveRebounds,
            TeamRebounds = 0,
            Points = tally.Points,
        };
    }

    private Tally Team(TeamSide side)
    {
        if (_teams.TryGetValue(side, out var tally))
        {
            return tally;
        }

        tally = new Tally(Guid.Empty, side);
        _teams[side] = tally;
        return tally;
    }

    private Tally Shooter(Guid? playerId, TeamSide side) => PlayerTally(Id(playerId, "atfedilen oyuncu"), side);

    private Tally PlayerTally(Guid playerId, TeamSide side)
    {
        if (_players.TryGetValue(playerId, out var tally))
        {
            return tally;
        }

        if (!_definitions.ContainsKey(playerId))
        {
            throw new InvalidOperationException(
                $"Event, setup kadrosunda olmayan bir oyuncuyu atfediyor: {playerId}");
        }

        tally = new Tally(playerId, side);
        _players[playerId] = tally;
        return tally;
    }

    private static TeamSide Side(TeamSide? side, string eventName) =>
        side ?? throw new InvalidOperationException($"{eventName} takım bilgisi taşımalı.");

    private static Guid Id(Guid? id, string description) =>
        id ?? throw new InvalidOperationException($"{description} bilgisi taşımalı.");

    private sealed class Tally(Guid playerId, TeamSide team)
    {
        public Guid PlayerId { get; } = playerId;

        public TeamSide Team { get; } = team;

        public int Made { get; set; }

        public int Attempts { get; set; }

        public int TwoMade { get; set; }

        public int TwoAttempts { get; set; }

        public int ThreeMade { get; set; }

        public int ThreeAttempts { get; set; }

        public int Assists { get; set; }

        public int Turnovers { get; set; }

        public int PersonalFouls { get; set; }

        public int FreeThrowAttempts { get; set; }

        public int FreeThrowMakes { get; set; }

        public int Blocks { get; set; }

        public int OffensiveRebounds { get; set; }

        public int DefensiveRebounds { get; set; }

        public int TeamRebounds { get; set; }

        public int Points { get; set; }
    }
}
