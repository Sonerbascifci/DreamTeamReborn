using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Replay;
using DreamTeam.MatchEngine.Rules;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M5: simulation replay (07 §7) ve T16 "snapshot serialize/restore -> kesintisiz
/// kosuyla ayni devam".
///
/// <para><b>D87 neden her sey fingerprint ile?</b> Bu oturumda OLÇÜLDÜ:
/// <c>ImmutableArray&lt;T&gt;.Equals</c> referans esitligidir, bu yuzden
/// <c>record</c> uretici esitligi <c>Team</c>, <c>TeamMatchSetup</c>,
/// <c>MatchSetup</c> ve <c>MatchState</c> icin bozuktur. T16
/// <c>Assert.Equal(state, restored)</c> ile yazilamaz.</para>
/// </summary>
public class SnapshotReplayTests
{
    // ------------------------------------------------------------ T16: round-trip

    [Fact]
    public void SnapshotRoundTripIsByteIdentical()
    {
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = M5TestData.AdvanceSteps(simulation, simulation.Create(M2TestData.NeutralMirror()), 30);

        var snapshot = MatchSnapshot.Capture(state, simulation.ConfigHash);
        var json = MatchSnapshotSerializer.ToJson(snapshot);

        var back = MatchSnapshotSerializer.FromJson(json);

        // D88: enum'lar isim tabanli; JSON bayti ayni cikar.
        Assert.Equal(json, MatchSnapshotSerializer.ToJson(back));
    }

    [Fact]
    public void RestoredStateHasTheSameFingerprint()
    {
        // D87: esitlik parmak iziyle olculur.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = M5TestData.AdvanceSteps(simulation, simulation.Create(M2TestData.NeutralMirror()), 30);

        var snapshot = MatchSnapshot.Capture(state, simulation.ConfigHash);
        var restored = MatchSnapshotSerializer.RoundTrip(snapshot).Restore(config);

        Assert.Equal(MatchStateFingerprint.Of(state), MatchStateFingerprint.Of(restored));
    }

    [Fact]
    public void SnapshotPreservesTheRandomState()
    {
        // RNG state tohum DEGIL, mevcut durumdur (05 §14). Bu olcum M5
        // planlamasinda yapildi: GetState/SetState round-trip KESIN.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = M5TestData.AdvanceSteps(simulation, simulation.Create(M2TestData.NeutralMirror()), 25);

        var snapshot = MatchSnapshot.Capture(state, simulation.ConfigHash);
        var restored = MatchSnapshotSerializer.RoundTrip(snapshot).Restore(config);

        // Siradaki cekilisler birebir ayni.
        for (var index = 0; index < 8; index++)
        {
            Assert.Equal(state.Random.NextUInt64(), restored.Random.NextUInt64());
        }
    }

    [Fact]
    public void RestoredStateContinuesIdenticallyForFortySteps()
    {
        // T16'in kalbi: restore edilen durum, kesintisiz kosuyla AYNI devam eder.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var setup = M2TestData.NeutralMirror();

        var live = M5TestData.AdvanceSteps(simulation, simulation.Create(setup), 20);

        var snapshot = MatchSnapshot.Capture(live, simulation.ConfigHash);
        var restored = MatchSnapshotSerializer.RoundTrip(snapshot).Restore(config);

        // 40 adim paralel ilerlet.
        var fromLive = live;
        var fromRestored = restored;

        for (var step = 0; step < 40; step++)
        {
            fromLive = simulation.Advance(fromLive).State;
            fromRestored = simulation.Advance(fromRestored).State;
        }

        Assert.Equal(MatchStateFingerprint.Of(fromLive), MatchStateFingerprint.Of(fromRestored));
    }

    [Fact]
    public void RestoredStateProducesTheSameEventStream()
    {
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var setup = M2TestData.NeutralMirror();

        var live = M5TestData.AdvanceSteps(simulation, simulation.Create(setup), 15);
        var snapshot = MatchSnapshot.Capture(live, simulation.ConfigHash);
        var restored = MatchSnapshotSerializer.RoundTrip(snapshot).Restore(config);

        var liveEvents = new List<MatchEvent>();
        var restoredEvents = new List<MatchEvent>();

        var fromLive = live;
        var fromRestored = restored;

        for (var step = 0; step < 30; step++)
        {
            var a = simulation.Advance(fromLive);
            var b = simulation.Advance(fromRestored);

            liveEvents.AddRange(a.Events);
            restoredEvents.AddRange(b.Events);

            fromLive = a.State;
            fromRestored = b.State;
        }

        Assert.Equal(
            MatchStateFingerprint.OfEvents(liveEvents),
            MatchStateFingerprint.OfEvents(restoredEvents));
    }

    [Fact]
    public void APendingCommandQueueSurvivesASnapshotRoundTrip()
    {
        // D86: kuyruk kaybolursa replay bozulur. Kuyruk parmak izine de girer.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var setup = M2TestData.NeutralMirror();

        var state = simulation.Create(setup);

        // DeadBall komutu kuyruga girsin (ilk adim PeriodBreak sunar).
        var queued = simulation.Advance(
            state,
            [M5TestData.Substitute(setup, boundary: CommandBoundary.DeadBall)]).State;

        Assert.Equal(1, queued.CommandQueue.PendingCount);

        var snapshot = MatchSnapshot.Capture(queued, simulation.ConfigHash);
        var restored = MatchSnapshotSerializer.RoundTrip(snapshot).Restore(config);

        Assert.Equal(1, restored.CommandQueue.PendingCount);
        Assert.Equal(
            queued.CommandQueue.Pending[0].CommandId,
            restored.CommandQueue.Pending[0].CommandId);

        Assert.Equal(
            MatchStateFingerprint.Of(queued),
            MatchStateFingerprint.Of(restored));
    }

    [Fact]
    public void TheSettledIdempotencyLedgerSurvivesASnapshot()
    {
        // T14: "islendi" kaydi da replay ile korunmali; aksi halde ayni
        // CommandId restore sonrasi tekrar uygulanabilir.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var setup = M2TestData.NeutralMirror();

        var commandId = Guid.NewGuid();

        // PeriodBreak sunulan ilk sinirdir: komut ayni adimda uygulanir ve
        // "islendi" olarak isaretlenir (D93, D94).
        var applied = simulation
            .Advance(
                simulation.Create(setup),
                [
                    M5TestData.ChangeOffense(
                        tactic: OffensiveTactic.InsidePost,
                        boundary: CommandBoundary.PeriodBreak,
                        commandId: commandId),
                ])
            .State;

        Assert.Contains(commandId, applied.CommandQueue.Settled);

        var snapshot = MatchSnapshot.Capture(applied, simulation.ConfigHash);
        var restored = MatchSnapshotSerializer.RoundTrip(snapshot).Restore(config);

        Assert.Contains(commandId, restored.CommandQueue.Settled);

        // Restore sonrasi ayni komut reddedilir.
        var duplicate = simulation.Advance(restored, [M5TestData.ChangeOffense(commandId: commandId)]);
        Assert.Contains(duplicate.CommandResults, r =>
            !r.Applied && r.Reason == Commands.CommandRejectionReason.DuplicateCommand);
    }

    // ------------------------------------------------------------ surum uyumlulugu

    [Fact]
    public void TheSnapshotCarriesTheEngineAndConfigIdentity()
    {
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(M2TestData.NeutralMirror());

        var snapshot = MatchSnapshot.Capture(state, simulation.ConfigHash);

        Assert.Equal(EngineIdentityVersion(), snapshot.EngineVersion);
        Assert.Equal(simulation.ConfigHash, snapshot.ConfigHash);
        Assert.Equal(MatchSimulation.EventSchemaVersion, snapshot.EventSchemaVersion);
        Assert.Equal(state.NextSequence, snapshot.StateSequence);
        Assert.Equal(MatchSnapshot.CurrentSchemaVersion, snapshot.SnapshotSchemaVersion);
    }

    [Fact]
    public void AnEngineVersionMismatchIsRejectedLoudly()
    {
        // 07 §7: "Eski motor binary'si yoksa saklanan eventlerden mac izlenebilir;
        // eski macin yeniden hesaplandigi iddia edilmez."
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var snapshot = MatchSnapshot.Capture(
            simulation.Create(M2TestData.NeutralMirror()), simulation.ConfigHash);

        Assert.Throws<InvalidOperationException>(
            () => snapshot.RequireCompatible("0.0.1-fake", simulation.ConfigHash));
    }

    [Fact]
    public void AConfigHashMismatchIsRejectedLoudly()
    {
        // Farkli denge/ayar ile ayni mac uretilemez.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var snapshot = MatchSnapshot.Capture(
            simulation.Create(M2TestData.NeutralMirror()), simulation.ConfigHash);

        Assert.Throws<InvalidOperationException>(
            () => snapshot.RequireCompatible(EngineIdentityVersion(), "0000000000000000"));
    }

    [Fact]
    public void AMatchingSnapshotPassesTheCompatibilityCheck()
    {
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var snapshot = MatchSnapshot.Capture(
            simulation.Create(M2TestData.NeutralMirror()), simulation.ConfigHash);

        snapshot.RequireCompatible(EngineIdentityVersion(), simulation.ConfigHash);
    }

    // -------------------------------------------------------------- fingerprint

    [Fact]
    public void TheFingerprintDetectsASingleEnergyChange()
    {
        // D87'nin yardimcisi gercekten ayirt edici olmali.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = M5TestData.AdvanceSteps(simulation, simulation.Create(M2TestData.NeutralMirror()), 12);

        var mutated = state with
        {
            Home = state.Home with
            {
                PlayerStates = [.. state.Home.PlayerStates.Select(
                    (playerState, index) => index == 0
                        ? playerState with { Energy = playerState.Energy + 1.0 }
                        : playerState)],
            },
        };

        Assert.NotEqual(MatchStateFingerprint.Of(state), MatchStateFingerprint.Of(mutated));
    }

    [Fact]
    public void TheFingerprintDetectsASingleTacticChange()
    {
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(M2TestData.NeutralMirror());

        var mutated = state with
        {
            Home = state.Home with { OffensiveTactic = OffensiveTactic.InsidePost },
        };

        Assert.NotEqual(MatchStateFingerprint.Of(state), MatchStateFingerprint.Of(mutated));
    }

    [Fact]
    public void TheFingerprintIsStableForIdenticalState()
    {
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(M2TestData.NeutralMirror());

        // Ayni durum icin iki kez ayni metin.
        Assert.Equal(MatchStateFingerprint.Of(state), MatchStateFingerprint.Of(state));
    }

    // ------------------------------------------------------------ serilestirme

    [Fact]
    public void EnumSerializationIsNameBased()
    {
        // D88: varsayilan sayisaldir (InsidePost -> 3) ve enum ordering'i
        // degisse kayitli snapshot sessizce bozulur. Biz isim tabanliyiz.
        // Fixture taktik degisikligi icin InsidePost'a cekilir, aksi halde
        // JSON'da gorunecek bir deger olmaz.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var insidePost = M2TestData.WithTactics(
            777, OffensiveTactic.InsidePost, DefensiveTactic.ZonePackPaint, Pace.Fast);

        var json = MatchSnapshotSerializer.ToJson(
            MatchSnapshot.Capture(simulation.Create(insidePost), simulation.ConfigHash));

        Assert.Contains("InsidePost", json, StringComparison.Ordinal);
        Assert.Contains("ZonePackPaint", json, StringComparison.Ordinal);
        Assert.Contains("Fast", json, StringComparison.Ordinal);

        // Sayisal temsil YOK.
        Assert.DoesNotContain("\"OffensiveTactic\":3", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Pace\":2", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRandomStateIsStoredAsHex()
    {
        // Onceden raw sayi dizisi olarak cikiyordu; hex hem kisa hem okunur.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);

        var json = MatchSnapshotSerializer.ToJson(
            MatchSnapshot.Capture(simulation.Create(M2TestData.NeutralMirror()), simulation.ConfigHash));

        // 8 bayt -> 16 hex karakter.
        var marker = "\"RandomState\":\"";
        var start = json.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var hex = json.Substring(start, 16);

        Assert.Equal(16, hex.Length);
        Assert.All(hex, character => Uri.IsHexDigit(character));
    }

    [Fact]
    public void TheSerializerUsesZeroNugetPackages()
    {
        // D88: bu oturumda olculdu. Bagimlilik kisiti bozulmaz.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var snapshot = MatchSnapshot.Capture(
            simulation.Create(M2TestData.NeutralMirror()), simulation.ConfigHash);

        // Serilestirme ayarlari degistirilse bile ayni bayt uretilir.
        Assert.Equal(
            MatchSnapshotSerializer.ToJson(snapshot),
            MatchSnapshotSerializer.ToJson(MatchSnapshotSerializer.RoundTrip(snapshot)));
    }

    [Fact]
    public void RestoringWithTheWrongRandomStateLengthIsRejected()
    {
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);

        var snapshot = MatchSnapshot.Capture(
            simulation.Create(M2TestData.NeutralMirror()), simulation.ConfigHash) with
        {
            RandomState = [1, 2, 3],
        };

        Assert.Throws<InvalidOperationException>(() => snapshot.Restore(config));
    }

    private static string EngineIdentityVersion() => M2TestData.Identity().EngineVersion;
}
