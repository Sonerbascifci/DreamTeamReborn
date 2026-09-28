using System.Globalization;
using System.Text;

namespace DreamTeam.MatchEngine.Diagnostics;

/// <summary>
/// M6 (D100, T15): read-only rule counters carried by <c>MatchState</c>.
///
/// <para><b>THREE HARD RULES. This type exists only to be read, never to
/// influence.</b></para>
/// <list type="number">
///   <item><description><b>It never consumes RNG.</b> A counter increment draws
///   nothing. M4's call-order contract is therefore intact.</description></item>
///   <item><description><b>It never enters <c>ConfigHash</c>.</b> There is no
///   config surface for diagnostics at all, so <c>ComputeConfigHash</c> is
///   untouched. "Same config" keeps meaning "same domain behaviour".</description></item>
///   <item><description><b>No domain decision reads it.</b> Every increment
///   happens after the decision it counts, and nothing outside diagnostics
///   reads these fields. That is what makes T15 true by construction rather
///   than by hope.</description></item>
/// </list>
///
/// <para><b>Why a toggle was deliberately NOT added.</b> T15 is phrased
/// "diagnostics on/off gives the same domain outcome". A config-level on/off
/// switch would itself be a config field and would enter <c>ConfigHash</c>,
/// breaking rule 2 and the meaning of "same config". Instead T15 is verified as
/// <i>"introducing this surface changed no domain outcome"</i>: the M5 simulator
/// SHA-256 is byte-identical after M6, the M5 event fingerprint is unchanged,
/// and the RNG state after N steps is unchanged. A no-op surface is also not
/// acceptable, so the counters must be provably populated. Both halves are
/// tested; neither is assumed.</para>
///
/// <para><b>Scope: counters only.</b> This is not a second decision mechanism
/// and adds no rule. 05 &#167;3 forbids hiding an ineffective mechanism, so
/// every counter here is reported in the M6 balance report.</para>
/// </summary>
public sealed record DiagnosticCounters
{
    /// <summary>All-zero instance. The starting point of every match.</summary>
    public static DiagnosticCounters Empty { get; } = new();

    public int ActionsRun { get; init; }

    /// <summary>Actions that ended without a shot attempt (pass/drive to a stop).</summary>
    public int ActionsWithoutShot { get; init; }

    public int ShotsAttempted { get; init; }

    public int ShotsMade { get; init; }

    public int ShotsMissed { get; init; }

    public int ShotsBlocked { get; init; }

    /// <summary>
    /// Rim contact draws. Engine-internal: it feeds the shot-clock policy only
    /// and is NOT an event, so this is the one counter an event-derived
    /// projection could not produce.
    /// </summary>
    public int RimContacts { get; init; }

    public int TurnoversLostBall { get; init; }

    public int TurnoversOffensiveFoul { get; init; }

    public int TurnoversShotClockViolation { get; init; }

    public int FoulsOffensive { get; init; }

    public int FoulsNonShooting { get; init; }

    public int FoulsShooting { get; init; }

    public int FreeThrowsAttempted { get; init; }

    public int FreeThrowsMade { get; init; }

    public int PossessionsStarted { get; init; }

    public int PeriodsStarted { get; init; }

    public int OvertimePeriodsStarted { get; init; }

    /// <summary>
    /// Dead-ball windows actually offered to commands, whether or not any
    /// command arrived. 06 &#167;7 window accounting.
    /// </summary>
    public int DeadBallWindows { get; init; }

    public int CommandsAccepted { get; init; }

    public int CommandsApplied { get; init; }

    public int CommandsRejected { get; init; }

    public int FoulOuts { get; init; }

    /// <summary>
    /// Element-wise sum. This is the only way counters are combined, and it is
    /// additive with no branching on values: merging cannot change a decision
    /// even if a decision were to read it.
    /// </summary>
    public static DiagnosticCounters Merge(DiagnosticCounters left, DiagnosticCounters right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new DiagnosticCounters
        {
            ActionsRun = left.ActionsRun + right.ActionsRun,
            ActionsWithoutShot = left.ActionsWithoutShot + right.ActionsWithoutShot,
            ShotsAttempted = left.ShotsAttempted + right.ShotsAttempted,
            ShotsMade = left.ShotsMade + right.ShotsMade,
            ShotsMissed = left.ShotsMissed + right.ShotsMissed,
            ShotsBlocked = left.ShotsBlocked + right.ShotsBlocked,
            RimContacts = left.RimContacts + right.RimContacts,
            TurnoversLostBall = left.TurnoversLostBall + right.TurnoversLostBall,
            TurnoversOffensiveFoul = left.TurnoversOffensiveFoul + right.TurnoversOffensiveFoul,
            TurnoversShotClockViolation = left.TurnoversShotClockViolation + right.TurnoversShotClockViolation,
            FoulsOffensive = left.FoulsOffensive + right.FoulsOffensive,
            FoulsNonShooting = left.FoulsNonShooting + right.FoulsNonShooting,
            FoulsShooting = left.FoulsShooting + right.FoulsShooting,
            FreeThrowsAttempted = left.FreeThrowsAttempted + right.FreeThrowsAttempted,
            FreeThrowsMade = left.FreeThrowsMade + right.FreeThrowsMade,
            PossessionsStarted = left.PossessionsStarted + right.PossessionsStarted,
            PeriodsStarted = left.PeriodsStarted + right.PeriodsStarted,
            OvertimePeriodsStarted = left.OvertimePeriodsStarted + right.OvertimePeriodsStarted,
            DeadBallWindows = left.DeadBallWindows + right.DeadBallWindows,
            CommandsAccepted = left.CommandsAccepted + right.CommandsAccepted,
            CommandsApplied = left.CommandsApplied + right.CommandsApplied,
            CommandsRejected = left.CommandsRejected + right.CommandsRejected,
            FoulOuts = left.FoulOuts + right.FoulOuts,
        };
    }

    /// <summary>
    /// All counters, one per line, fixed order. Same text for the same values,
    /// so a report or a test can compare it directly (08 &#167;4).
    /// </summary>
    public string ToReport()
    {
        var builder = new StringBuilder(512);

        Append(builder, nameof(ActionsRun), ActionsRun);
        Append(builder, nameof(ActionsWithoutShot), ActionsWithoutShot);
        Append(builder, nameof(ShotsAttempted), ShotsAttempted);
        Append(builder, nameof(ShotsMade), ShotsMade);
        Append(builder, nameof(ShotsMissed), ShotsMissed);
        Append(builder, nameof(ShotsBlocked), ShotsBlocked);
        Append(builder, nameof(RimContacts), RimContacts);
        Append(builder, nameof(TurnoversLostBall), TurnoversLostBall);
        Append(builder, nameof(TurnoversOffensiveFoul), TurnoversOffensiveFoul);
        Append(builder, nameof(TurnoversShotClockViolation), TurnoversShotClockViolation);
        Append(builder, nameof(FoulsOffensive), FoulsOffensive);
        Append(builder, nameof(FoulsNonShooting), FoulsNonShooting);
        Append(builder, nameof(FoulsShooting), FoulsShooting);
        Append(builder, nameof(FreeThrowsAttempted), FreeThrowsAttempted);
        Append(builder, nameof(FreeThrowsMade), FreeThrowsMade);
        Append(builder, nameof(PossessionsStarted), PossessionsStarted);
        Append(builder, nameof(PeriodsStarted), PeriodsStarted);
        Append(builder, nameof(OvertimePeriodsStarted), OvertimePeriodsStarted);
        Append(builder, nameof(DeadBallWindows), DeadBallWindows);
        Append(builder, nameof(CommandsAccepted), CommandsAccepted);
        Append(builder, nameof(CommandsApplied), CommandsApplied);
        Append(builder, nameof(CommandsRejected), CommandsRejected);
        Append(builder, nameof(FoulOuts), FoulOuts);

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string name, int value)
    {
        builder.Append(name).Append('=')
            .Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }
}

/// <summary>
/// Mutable step-scoped accumulator. A plain class with public settable counters
/// so one <c>Advance</c> step needs exactly ONE immutable
/// <see cref="DiagnosticCounters"/> allocation at the end, instead of one per
/// event. At 100K matches x ~1100 events that difference is the whole
/// difference between cheap and slow.
///
/// <para>It lives inside a single <c>Step</c> and is never reachable from a
/// decision. That is the same "cannot influence" property as the immutable
/// record; only the allocation cost differs.</para>
/// </summary>
public sealed class DiagnosticTally
{
    public int ActionsRun;
    public int ActionsWithoutShot;
    public int ShotsAttempted;
    public int ShotsMade;
    public int ShotsMissed;
    public int ShotsBlocked;
    public int RimContacts;
    public int TurnoversLostBall;
    public int TurnoversOffensiveFoul;
    public int TurnoversShotClockViolation;
    public int FoulsOffensive;
    public int FoulsNonShooting;
    public int FoulsShooting;
    public int FreeThrowsAttempted;
    public int FreeThrowsMade;
    public int PossessionsStarted;
    public int PeriodsStarted;
    public int OvertimePeriodsStarted;
    public int DeadBallWindows;
    public int CommandsAccepted;
    public int CommandsApplied;
    public int CommandsRejected;
    public int FoulOuts;

    /// <summary>Freezes the step's increments into an immutable record.</summary>
    public DiagnosticCounters ToCounters() => new()
    {
        ActionsRun = ActionsRun,
        ActionsWithoutShot = ActionsWithoutShot,
        ShotsAttempted = ShotsAttempted,
        ShotsMade = ShotsMade,
        ShotsMissed = ShotsMissed,
        ShotsBlocked = ShotsBlocked,
        RimContacts = RimContacts,
        TurnoversLostBall = TurnoversLostBall,
        TurnoversOffensiveFoul = TurnoversOffensiveFoul,
        TurnoversShotClockViolation = TurnoversShotClockViolation,
        FoulsOffensive = FoulsOffensive,
        FoulsNonShooting = FoulsNonShooting,
        FoulsShooting = FoulsShooting,
        FreeThrowsAttempted = FreeThrowsAttempted,
        FreeThrowsMade = FreeThrowsMade,
        PossessionsStarted = PossessionsStarted,
        PeriodsStarted = PeriodsStarted,
        OvertimePeriodsStarted = OvertimePeriodsStarted,
        DeadBallWindows = DeadBallWindows,
        CommandsAccepted = CommandsAccepted,
        CommandsApplied = CommandsApplied,
        CommandsRejected = CommandsRejected,
        FoulOuts = FoulOuts,
    };
}
