using System.Security.Cryptography;
using System.Text;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Diagnostics;
using DreamTeam.MatchEngine.Replay;

namespace DreamTeam.MatchEngine.Tests;

/// <summary>
/// M6 (D100), 08 T15: "Diagnostics acik/kapali -> ayni domain outcome."
///
/// <para><b>Neden bir anahtar degil?</b> T15 "acik/kapali" diyor. Bir config
/// anahtari eklemek o anahtari <c>ConfigHash</c>'e sokar ve "ayni config" anlamini
/// bozar. Bunun yerine T15 su anlamda denetleniyor: <b>bu yuzeyin eklenmesi
/// hicbir domain sonucunu degistirmedi.</b></para>
///
/// <para><b>Kanit nasil uretildi?</b> <see cref="M6Golden"/> degerleri commit
/// <c>3ca3b1e</c>'deki koddan (M6 yuzeyi YOKKEN) ayri bir git worktree'de
/// gecici bir prob ile alindi. Simdiki test, o degerleri degistirmedigi icin
/// kanitlar. M6'nin kendi kodunu M5'in kaniti yapmak, kanit olmazdi.</para>
///
/// <para>Bir yuzeyin "etkisiz" oldugunu soylemek 05 S3'te yasak; bu yuzden
/// sayaclarin BOS OLMADIGI da ayrica dogrulanir.</para>
/// </summary>
public class DiagnosticsEquivalenceTests
{
    [Theory]
    [InlineData(1UL, 1134)]
    [InlineData(2UL, 1110)]
    [InlineData(12_345UL, 1089)]
    [InlineData(20260927UL, 1126)]
    public void TheEventStreamForKnownSeedsIsUnchangedByAddingDiagnostics(ulong seed, int eventCount)
    {
        // M5'te uretilen, M6'nin DEGISTIRMEDIGI degerler. Not: ConfigHash
        // parmak izine GIRMEZ; D98a onu degistirdi ama bu kosuda komut
        // gonderilmedigi icin 20 saniyelik timeout butcesi devreye girmez ve
        // domain sonucu degismez. Iki gercek burada birlikte dogrulanir.
        var events = new MatchSimulation(M2TestData.Config())
            .Simulate(M2TestData.NeutralMirror(seed)).Events;

        Assert.Equal(eventCount, events.Length);
        Assert.Equal(M6Golden.EventFingerprintOf(seed), FingerprintOf(events));
    }

    [Theory]
    [InlineData(100, "D5DD591B792B00DF", 210, 61, 40, 15, 28, 185_500, 534_500, 1)]
    [InlineData(250, "6023D3646D1525AE", 515, 160, 99, 37, 55, 47_000, 1_393_000, 2)]
    public void TheRngStateAfterAFixedNumberOfStepsIsUnchanged(
        int steps,
        string expectedRngHex,
        long expectedSequence,
        int expectedActions,
        int expectedPossessions,
        int expectedHomeScore,
        int expectedAwayScore,
        long expectedGameClockMs,
        long expectedElapsedMs,
        int expectedPeriod)
    {
        // DOGRUDAN kanit. Diagnostics adim basina bir kayit yaziyor; bu bir RNG
        // cekilisi olsaydi ya da cekilis SIRASINI degistirseydi durum farkli
        // olurdu. Ayni sey event akisinda zaten gorunur, ama burada dogrudan
        // olculur.
        var simulation = new MatchSimulation(M2TestData.Config());
        var state = simulation.Create(M2TestData.NeutralMirror(4242));

        for (var step = 0; step < steps && !state.IsTerminal; step++)
        {
            state = simulation.Advance(state).State;
        }

        Span<byte> rng = stackalloc byte[8];
        state.Random.GetState(rng);

        Assert.Equal(expectedRngHex, Convert.ToHexString(rng));
        Assert.Equal(expectedSequence, state.NextSequence);
        Assert.Equal(expectedActions, state.TotalActionCount);
        Assert.Equal(expectedPossessions, state.PossessionCount);
        Assert.Equal(expectedHomeScore, state.HomeScore);
        Assert.Equal(expectedAwayScore, state.AwayScore);
        Assert.Equal(expectedPeriod, state.Clock.Period);
        Assert.Equal(expectedGameClockMs, state.Clock.GameClockMs);
        Assert.Equal(expectedElapsedMs, state.Clock.ElapsedGameTimeMs);
    }

    [Fact]
    public void TheCountersArePopulatedForACompletedMatch()
    {
        // 05 S3: etkisiz mekanizmayi gizleme yasagi. Bos bir yuzey "ayni
        // sonuc" testini her zaman gecirirdi; bu test onu bos yaziyor.
        var result = new MatchSimulation(M2TestData.Config())
            .Simulate(M2TestData.NeutralMirror(777));

        var counters = result.Diagnostics;

        Assert.NotEqual(DiagnosticCounters.Empty, counters);
        Assert.True(counters.ActionsRun > 0, "Aksiyon sayaci bos.");
        Assert.True(counters.ShotsAttempted > 0, "Sut sayaci bos.");
        Assert.True(counters.ShotsMade > 0, "Isabet sayaci bos.");
        Assert.True(counters.ShotsMissed > 0, "Kacan sayaci bos.");
        Assert.True(counters.RimContacts > 0, "Cember teması sayaci bos.");
        Assert.True(counters.PossessionsStarted > 0, "Possession sayaci bos.");
        Assert.True(counters.PeriodsStarted >= 4, "En az 4 periyot olmali.");
        Assert.True(counters.DeadBallWindows > 0, "Dead-ball penceresi sayilmamis.");
    }

    [Fact]
    public void AnActionEitherAttemptsAShotOrIsCountedAsNotDoingSo()
    {
        // Bu kimlik ilk tasarimda BOZUKTU ve bu test yakaladi: hucre saati
        // tukenince erken donen yol ile suta donusurken hucre faulu cozulen
        // yol sayaci artirmiyordu, oysa ikisi de sut denemiyordu. Duzeltme
        // yapilandirildi (bayrak), yani ileride eklenen bir cikis yolu onu
        // bozamaz.
        var counters = new MatchSimulation(M2TestData.Config())
            .Simulate(M2TestData.NeutralMirror(31)).Diagnostics;

        Assert.Equal(counters.ActionsRun, counters.ShotsAttempted + counters.ActionsWithoutShot);
    }

    [Fact]
    public void TheCountersHaveNoConfigSurfaceSoTheHashIsUnaffected()
    {
        // Diagnostics icin config alani YOKTUR. "Ayni config" anlami degismiyor.
        Assert.Equal(
            M2TestData.Config().ComputeConfigHash(),
            M2TestData.Config().ComputeConfigHash());
    }

    [Fact]
    public void CountersSurviveASnapshotRoundTrip()
    {
        // Yoksa ortada alinan bir snapshot sayaclari sessizce sifirlar ve M6
        // raporu eksik kalirdi.
        var config = M2TestData.Config();
        var simulation = new MatchSimulation(config);
        var state = simulation.Create(M2TestData.NeutralMirror(8));

        for (var step = 0; step < 200 && !state.IsTerminal; step++)
        {
            state = simulation.Advance(state).State;
        }

        Assert.NotEqual(DiagnosticCounters.Empty, state.Diagnostics);

        var snapshot = MatchSnapshot.Capture(state, simulation.ConfigHash);
        var restored = MatchSnapshotSerializer.RoundTrip(snapshot).Restore(config);

        Assert.Equal(state.Diagnostics.ToReport(), restored.Diagnostics.ToReport());
        Assert.Equal(MatchStateFingerprint.Of(state), MatchStateFingerprint.Of(restored));
    }

    [Fact]
    public void CountersAppearInTheStateFingerprint()
    {
        // Parmak izi sayaclarin DEGISTIGINI gormeli; yoksa bir sayaç hatası
        // replay testlerinde gorunmez olur.
        var simulation = new MatchSimulation(M2TestData.Config());
        var advanced = simulation.Advance(simulation.Create(M2TestData.NeutralMirror(1))).State;

        Assert.Contains(
            "diagnostics|ActionsRun=",
            MatchStateFingerprint.Of(advanced),
            StringComparison.Ordinal);
    }

    [Fact]
    public void MergingIsPurelyAdditive()
    {
        var left = new DiagnosticCounters { ActionsRun = 3, ShotsMade = 2 };
        var right = new DiagnosticCounters { ActionsRun = 4, ShotsMade = 1 };
        var merged = DiagnosticCounters.Merge(left, right);

        Assert.Equal(7, merged.ActionsRun);
        Assert.Equal(3, merged.ShotsMade);

        Assert.Equal(3, left.ActionsRun);
        Assert.Equal(4, right.ActionsRun);
    }

    [Fact]
    public void TheReportTextIsStableForTheSameValues()
    {
        var counters = new DiagnosticCounters { ActionsRun = 11, PossessionsStarted = 22 };

        Assert.Equal(counters.ToReport(), counters.ToReport());
        Assert.Contains("ActionsRun=11", counters.ToReport(), StringComparison.Ordinal);
        Assert.Contains("PossessionsStarted=22", counters.ToReport(), StringComparison.Ordinal);
    }

    [Fact]
    public void AStepScopedTallyFreezesIntoAnImmutableRecord()
    {
        var tally = new DiagnosticTally { ActionsRun = 5, ShotsMade = 2 };
        var frozen = tally.ToCounters();

        Assert.Equal(5, frozen.ActionsRun);
        Assert.Equal(2, frozen.ShotsMade);

        tally.ActionsRun += 1;

        Assert.Equal(5, frozen.ActionsRun);
        Assert.Equal(6, tally.ToCounters().ActionsRun);
    }

    [Fact]
    public void DiagnosticsDoNotEmitEvents()
    {
        // 07 S1: "Diagnostic rol veya gizli state payload'a girmez." Sayac bir
        // event DEGILDIR; komut gonderilmedigi kosuda turler M5 ile ayni kalmali.
        var result = new MatchSimulation(M2TestData.Config())
            .Simulate(M2TestData.NeutralMirror(64));

        var types = result.Events.Select(e => e.Type).Distinct().OrderBy(t => (int)t).ToList();

        Assert.Equal(17, types.Count);
        Assert.DoesNotContain(Events.MatchEventType.CommandApplied, types);
        Assert.DoesNotContain(Events.MatchEventType.CommandRejected, types);
        Assert.DoesNotContain(Events.MatchEventType.Substitution, types);
        Assert.DoesNotContain(Events.MatchEventType.Timeout, types);
    }

    [Fact]
    public void CountersReconcileWithTheEventStream()
    {
        // Event'ten turetilebilen her sayac, event'ten turetilen degerle
        // uyusmali. Uyusmezse ya sayaç ya event akisi yanlistir.
        var result = new MatchSimulation(M2TestData.Config())
            .Simulate(M2TestData.NeutralMirror(1));

        var counters = result.Diagnostics;

        long Count(Events.MatchEventType type) =>
            result.Events.Count(e => e.Type == type);

        Assert.Equal(Count(Events.MatchEventType.ShotMade), counters.ShotsMade);
        Assert.Equal(Count(Events.MatchEventType.ShotMissed), counters.ShotsMissed);
        Assert.Equal(Count(Events.MatchEventType.ShotAttempt), counters.ShotsAttempted);
        Assert.Equal(Count(Events.MatchEventType.Foul),
            counters.FoulsShooting + counters.FoulsNonShooting + counters.FoulsOffensive);
        Assert.Equal(Count(Events.MatchEventType.Turnover),
            counters.TurnoversLostBall + counters.TurnoversOffensiveFoul + counters.TurnoversShotClockViolation);
        Assert.Equal(Count(Events.MatchEventType.FreeThrowAttempt), counters.FreeThrowsAttempted);
        Assert.Equal(Count(Events.MatchEventType.FreeThrowMade), counters.FreeThrowsMade);
        Assert.Equal(Count(Events.MatchEventType.PossessionStarted), counters.PossessionsStarted);
        Assert.Equal(Count(Events.MatchEventType.PeriodStarted), counters.PeriodsStarted);
        Assert.Equal(Count(Events.MatchEventType.Block), counters.ShotsBlocked);
    }

    /// <summary>
    /// M5'in kendi parmak iziyle BIREBIR ayni alan sirasi. Alanlarin sirasi
    /// burada acikca sabittir; JSON canonicalization varsayilmaz (08 S4).
    /// </summary>
    private static string FingerprintOf(IReadOnlyList<Events.MatchEvent> events)
    {
        var builder = new StringBuilder(events.Count * 96);

        foreach (var matchEvent in events)
        {
            builder.Append(matchEvent.Sequence).Append('|')
                .Append(matchEvent.Type).Append('|')
                .Append(matchEvent.Period).Append('|')
                .Append(matchEvent.GameClockMs).Append('|')
                .Append(matchEvent.ElapsedGameTimeMs).Append('|')
                .Append(matchEvent.PossessionId).Append('|')
                .Append(matchEvent.ActionId).Append('|')
                .Append(matchEvent.TeamId).Append('|')
                .Append(matchEvent.PlayerId).Append('|')
                .Append(matchEvent.SecondaryPlayerId).Append('|')
                .Append(matchEvent.Payload).Append('\n');
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}

/// <summary>
/// M6 (T15): M5 kodunda uretilen golden degerler.
///
/// <para><b>Nereden geldi?</b> Commit <c>3ca3b1e</c> (M5 kodu, M6 yuzeyi yok) bir
/// git worktree'de kuruldu; icine gecici bir prob testi kondu ve degerler doku.
/// Prob silindi, worktree kaldirildi. Bunlar <b>M5'in</b> ciktisidir.</para>
///
/// <para><b>Neden ham metin degil SHA?</b> Metin 1000 satirdan uzun. SHA-256
/// ayni esitligi tek satirlik alanla verir.</para>
/// </summary>
internal static class M6Golden
{
    private static readonly IReadOnlyDictionary<ulong, string> EventFingerprints =
        new Dictionary<ulong, string>
        {
            [1UL] = "48F76DBB0FCFCA3BD44784388B8316ABEC60C2309CFD2ACABBF17B18FD51C4CD",
            [2UL] = "3AD9523EC2E45EB553F4F36B608FB1F6E03EBB0A86F5ACDFA3715345960CDDAF",
            [12_345UL] = "B80E108709B09FA07857D6ED6C90C225E2D5B8B797F649207DE7153B2512F55A",
            [20260927UL] = "7E253C3811D330290D8A53471DBC49F7CC3DD06797183AF3448374EC5EF842E1",
        };

    public static string EventFingerprintOf(ulong seed) =>
        EventFingerprints.TryGetValue(seed, out var value)
            ? value
            : throw new KeyNotFoundException(
                $"M6 golden degeri yok: seed {seed}. Bilinen: {string.Join(", ", EventFingerprints.Keys)}.");
}
