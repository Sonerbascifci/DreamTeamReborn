using System.Collections.Immutable;
using DreamTeam.Domain.Players;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M5 testleri icin komut fixture'lari.
///
/// <para><b>Buradaki butce degerleri motor varsayilanlariyla AYNIDIR</b>
/// (<c>RulesProfile.SimpleNbaInspired</c>). Testler motoru degil
/// <i>mekanizmayi</i> olcer: butce yeter mi, butce tukendi mi, pencere kapali
/// mi. Denge etkisi M6'nindir.</para>
///
/// <para><b>Substitution penceresi (D82, kullanici netlestirdi):</b> 5+1 nokta —
/// isabetli basket, hucrem degisimi, serbest atis serisi sonu, hucrem saati
/// ihlali, duduk sonrasi faul, devre arasi. Yalniz DREB/steal sonrasi oyun
/// CANLIDIR ve pencere YOKTUR.</para>
/// </summary>
internal static class M5TestData
{
    // ------------------------------------------------------------ komut kuruculari

    /// <summary>Taktik degistirme komutu. Varsayilan sinir: ActionDecision.</summary>
    public static ScheduledManagerCommand ChangeOffense(
        TeamSide side = TeamSide.Home,
        OffensiveTactic tactic = OffensiveTactic.InsidePost,
        CommandBoundary? boundary = null,
        Guid? commandId = null) => new()
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Side = side,
            Kind = ManagerCommandKind.ChangeOffense,
            Payload = CommandPayload.ForOffense(tactic),
            AcceptedOrder = 0,
            TargetBoundary = boundary ?? CommandValidator.DefaultBoundaryFor(
                ManagerCommandKind.ChangeOffense),
        };

    public static ScheduledManagerCommand ChangeDefense(
        TeamSide side = TeamSide.Home,
        DefensiveTactic tactic = DefensiveTactic.ZonePackPaint,
        CommandBoundary? boundary = null,
        Guid? commandId = null) => new()
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Side = side,
            Kind = ManagerCommandKind.ChangeDefense,
            Payload = CommandPayload.ForDefense(tactic),
            AcceptedOrder = 0,
            TargetBoundary = boundary ?? CommandValidator.DefaultBoundaryFor(
                ManagerCommandKind.ChangeDefense),
        };

    public static ScheduledManagerCommand ChangePace(
        TeamSide side = TeamSide.Home,
        Pace pace = Pace.Fast,
        CommandBoundary? boundary = null,
        Guid? commandId = null) => new()
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Side = side,
            Kind = ManagerCommandKind.ChangePace,
            Payload = CommandPayload.ForPace(pace),
            AcceptedOrder = 0,
            TargetBoundary = boundary ?? CommandValidator.DefaultBoundaryFor(
                ManagerCommandKind.ChangePace),
        };

    /// <summary>
    /// Substitution komutu. Varsayilan giren = kadro sirasi 5 (ilk yedek),
    /// cikan = lineup'in 0. slotundaki oyuncu.
    /// </summary>
    public static ScheduledManagerCommand Substitute(
        MatchSetup setup,
        TeamSide side = TeamSide.Home,
        Guid? incoming = null,
        Guid? outgoing = null,
        CommandBoundary? boundary = null,
        Guid? commandId = null)
    {
        var team = side == TeamSide.Home ? setup.Home : setup.Away;
        var onCourt = team.Lineup.PlayerIds;
        var roster = RosterIds(team.Team);

        // Ilk yedek: lineup'ta olmayan ilk kadro oyuncusu.
        var bench = roster.FirstOrDefault(id => !onCourt.Contains(id));

        return new ScheduledManagerCommand
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Side = side,
            Kind = ManagerCommandKind.Substitute,
            Payload = CommandPayload.ForSubstitution(
                incoming ?? bench,
                outgoing ?? onCourt[0]),
            AcceptedOrder = 0,
            TargetBoundary = boundary ?? CommandValidator.DefaultBoundaryFor(
                ManagerCommandKind.Substitute),
        };
    }

    public static ScheduledManagerCommand Timeout(
        TeamSide side = TeamSide.Home,
        TimeoutKind kind = TimeoutKind.Full,
        CommandBoundary? boundary = null,
        Guid? commandId = null) => new()
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Side = side,
            Kind = ManagerCommandKind.RequestTimeout,
            Payload = CommandPayload.ForTimeout(kind),
            AcceptedOrder = 0,
            TargetBoundary = boundary ?? CommandValidator.DefaultBoundaryFor(
                ManagerCommandKind.RequestTimeout),
        };

    public static ImmutableArray<Guid> RosterIds(Domain.Teams.Team team) =>
        [.. team.Roster.Select(player => player.Id)];

    // ------------------------------------------------------------------- config

    /// <summary>Faul ve top kaybi kapalı; hucrem sayaci hizli bitsin.</summary>
    public static EngineConfig NoFouls(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Fouls = config.Fouls with
            {
                FoulProbabilityPerAction = 0.0,
                OffensiveFoulShare = 0.0,
            },
        };
    }

    /// <summary>
    /// Hucrem saatinin her possesyonu tek aksiyonda tuketmesini saglar.
    /// Boylece dead-ball penceresi sayisi kontrollu olur; timeout butcesi
    /// testlerinde kullanilir.
    /// </summary>
    public static EngineConfig VeryShortShotClock(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Rules = config.Rules with { ShotClockMs = 5_000 },
        };
    }

    /// <summary>Sut yok; sadece turnover ile possession degisimi.</summary>
    public static EngineConfig TurnoverOnly(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Actions = config.Actions with
            {
                ShotCompletionProbability = 0.0,
                TurnoverProbability = 1.0,
            },
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
        };
    }

    /// <summary>Hucum saati ihlali; her hucrem 3 aksiyonda biter.</summary>
    public static EngineConfig ShotClockOnly(EngineConfig? source = null)
    {
        var config = source ?? M2TestData.Config();

        return config with
        {
            Actions = config.Actions with
            {
                ShotCompletionProbability = 0.0,
                TurnoverProbability = 0.0,
            },
            Fouls = config.Fouls with { FoulProbabilityPerAction = 0.0 },
        };
    }

    // -------------------------------------------------------------------- yardimci

    /// <summary>Adim sayisi verilen kadar ilerletir; komut listesini de gecirir.</summary>
    public static MatchState AdvanceSteps(
        MatchSimulation simulation,
        MatchState state,
        int steps,
        IReadOnlyList<ScheduledManagerCommand>? commands = null)
    {
        var guard = 0;

        while (steps-- > 0 && !state.IsTerminal && guard++ < 500_000)
        {
            state = simulation.Advance(state, commands ?? CommandList.Empty).State;
        }

        return state;
    }

    public static ImmutableArray<CommandResult> ResultsOf(params StepResult[] results) =>
        [.. results.SelectMany(result => result.CommandResults)];

    public static CommandResult FirstApplied(IEnumerable<CommandResult> results) =>
        results.First(result => result.Applied);

    public static CommandResult FirstRejected(IEnumerable<CommandResult> results) =>
        results.First(result => !result.Applied);

    /// <summary>Event akisindaki mudahale turlari.</summary>
    public static ImmutableList<MatchEvent> InterventionEvents(MatchResult result) =>
        [.. result.Events.Where(IsIntervention)];

    private static bool IsIntervention(MatchEvent matchEvent) => matchEvent.Type
        is MatchEventType.TacticChanged
        or MatchEventType.DefenseChanged
        or MatchEventType.PaceChanged
        or MatchEventType.Substitution
        or MatchEventType.Timeout;

    public static ImmutableArray<CommandRejectedPayload> Rejections(MatchResult result) =>
        [.. result.Events
            .Where(e => e.Type == MatchEventType.CommandRejected)
            .Select(e => e.PayloadAs<CommandRejectedPayload>())];

    public static int SubstitutionsApplied(MatchResult result) => result.Events
        .Count(e => e.Type == MatchEventType.Substitution);

    public static int TimeoutsTaken(MatchResult result) => result.Events
        .Count(e => e.Type == MatchEventType.Timeout);

    public static int TacticChanges(MatchResult result) => result.Events
        .Count(e => e.Type == MatchEventType.TacticChanged);
}
