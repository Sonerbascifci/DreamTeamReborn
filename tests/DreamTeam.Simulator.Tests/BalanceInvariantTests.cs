using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Config;
using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator.Fixture;
using DreamTeam.Simulator.Reporting;

namespace DreamTeam.Simulator.Tests;

/// <summary>
/// M6: ic tutarlilik esikleri (D98c) ve batch ozeti.
///
/// <para><b>Bu testler "motor iyi mi" diye SORMAZ.</b> D98c geregi esikler
/// gercek sezon verisine degil motorun kendi ic tutarliligina bakar. Buradaki
/// testler de kendi denkliklerini dogrular; esik KALIRSA test de KALIR ve
/// bulgu olarak raporlanir. Bir esigin kirmasi basarisiz kabul edilmez.</para>
///
/// <para>ASCII yorum kullanir (depo kurali).</para>
/// </summary>
public class BalanceInvariantTests
{
    [Fact]
    public void AThousandMatchSmokeCorpusIsDeterministicAndComplete()
    {
        var catalog = new FixtureCatalog();
        var driver = new BatchDriver(catalog, M6TestData.Config());
        var plan = M6TestData.Plan(matches: 1_000);

        var first = driver.Run(plan);
        var second = driver.Run(plan);

        Assert.Equal(M6TestData.Describe(first.Summary), M6TestData.Describe(second.Summary));
        Assert.Equal(1_000, first.Summary.MatchCount);
        Assert.Equal(1_000, first.ProcessedMatches);
    }

    [Fact]
    public void TheMirrorCorpusIsSymmetricInTheLargeSample()
    {
        // 08 S5 esik 1: ev/deplasman kazanma orani 0.5'ten aydirilamaz.
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(matches: 4_000));

        var report = BalanceReportBuilder.Build("neutral-mirror", outcome.Summary, outcome.ConfigHash);

        var symmetry = report.Thresholds.Single(threshold => threshold.Number == 1);

        Assert.True(
            symmetry.Passed,
            $"Mirror simetrisi saglanmadi (BULGU, hata degil): {symmetry.Detail}");
    }

    [Fact]
    public void NoMatchAbortsInTheNeutralCorpus()
    {
        // 08 S5 esik 2: uc deger guvenligi.
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(matches: 2_000));

        Assert.Equal(0, outcome.Summary.AbortedCount);
        Assert.Empty(outcome.Summary.AbortReasons);
    }

    [Fact]
    public void HomeAndAwayScoreSimilarlyInTheNeutralCorpus()
    {
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(matches: 4_000));

        var report = BalanceReportBuilder.Build("neutral-mirror", outcome.Summary, outcome.ConfigHash);

        Assert.True(
            Math.Abs(report.AverageHomeScore - report.AverageAwayScore) < 3,
            $"Ev/dep ortalamasi asiri ayrildi: {report.AverageHomeScore} vs {report.AverageAwayScore}");
    }

    [Fact]
    public void TheStrongerRosterWinsMoreOftenInALargeSample()
    {
        // 08 S5 esik 4: "Guclu kadro buyuk orneklemde avantajli."
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan("quality-gap", matches: 4_000));

        var homeWinRate = WilsonInterval.For(outcome.Summary.HomeWins, outcome.Summary.MatchCount);

        Assert.True(
            homeWinRate.DeviatesFromHalf,
            "Guclu ev kadrosu belirgin bicimde kazanmadi. " +
            $"p={homeWinRate.Point:F3} CI95=[{homeWinRate.Lower:F3},{homeWinRate.Upper:F3}]");
    }

    [Theory]
    [InlineData("pace-slow")]
    [InlineData("pace-normal")]
    [InlineData("pace-fast")]
    public void EachPaceProducesAFiniteMeasuredReport(string fixture)
    {
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(fixture, matches: 300));

        var report = BalanceReportBuilder.Build(fixture, outcome.Summary, outcome.ConfigHash);

        Assert.False(double.IsNaN(report.Pace48));
        Assert.False(double.IsInfinity(report.Pace48));
        Assert.True(report.Pace48 > 0, $"Pace48 sifir veya negatif: {report.Pace48}");
    }

    [Fact]
    public void FasterPaceMeansMorePossessionsInTheSameGameTime()
    {
        // 08 S5 esik 5: "Pace: possession, TOV ve fatigue iliskisi olculur."
        var catalog = new FixtureCatalog();
        var driver = new BatchDriver(catalog, M6TestData.Config());

        var slow = driver.Run(M6TestData.Plan("pace-slow", matches: 1_500));
        var normal = driver.Run(M6TestData.Plan("pace-normal", matches: 1_500));
        var fast = driver.Run(M6TestData.Plan("pace-fast", matches: 1_500));

        Assert.True(
            slow.Summary.TotalPossessions < normal.Summary.TotalPossessions,
            $"Slow < Normal degil: {slow.Summary.TotalPossessions} vs {normal.Summary.TotalPossessions}");

        Assert.True(
            normal.Summary.TotalPossessions < fast.Summary.TotalPossessions,
            $"Normal < Fast degil: {normal.Summary.TotalPossessions} vs {fast.Summary.TotalPossessions}");
    }

    [Fact]
    public void SwappingSidesDoesNotChangeTheMirrorCorpusTotals()
    {
        // 08 S5 esik 6: "Home/away yer degistirmeli eslestirme kullan."
        var catalog = new FixtureCatalog();
        var driver = new BatchDriver(catalog, M6TestData.Config());

        var plain = driver.Run(M6TestData.Plan("neutral-mirror", matches: 800));
        var swapped = driver.Run(M6TestData.Plan("neutral-mirror-swapped", matches: 800));

        // Ayni toplam possession ve ayni toplam FGA beklenir: fixture'lar
        // birebir ayni kadrolari karsili taraflarda tutar.
        Assert.Equal(plain.Summary.TotalPossessions, swapped.Summary.TotalPossessions);
        Assert.Equal(
            plain.Summary.Home.Points + plain.Summary.Away.Points,
            swapped.Summary.Home.Points + swapped.Summary.Away.Points);
    }

    [Fact]
    public void DiagnosticsArePopulatedAndNotANoOpSurface()
    {
        // 05 S3 "etkisiz mekanizmayi gizleme" yasagi. Sayaçlar gerçekten
        // doluyorsa yüzey işe yarıyor; sıfır kalıyorsa işe yaramıyor.
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(matches: 50));

        var diagnostics = outcome.Summary.Diagnostics;

        Assert.True(diagnostics.ActionsRun > 0);
        Assert.True(diagnostics.ShotsAttempted > 0);
        Assert.True(diagnostics.ShotsMade > 0);
        Assert.True(diagnostics.ShotsMissed > 0);
        Assert.True(diagnostics.PossessionsStarted > 0);
        Assert.True(diagnostics.PeriodsStarted >= 4 * 50, "4 periyot x 50 mac = 200 olmali.");
        Assert.True(diagnostics.FoulsShooting + diagnostics.FoulsNonShooting
            + diagnostics.FoulsOffensive > 0);
        Assert.True(diagnostics.FreeThrowsAttempted > 0);
        Assert.True(diagnostics.RimContacts > 0);
        Assert.True(diagnostics.DeadBallWindows > 0);
    }

    [Fact]
    public void DiagnosticCountersReconcileWithTheEventStream()
    {
        // Sayaçlar bağımsız bir gerçek: event akışından türetilebilir olanlar
        // türetilebilir olanlarla uyuşmalı. Uyuşmazsa ya sayaç ya event yanlıştır.
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var setup = catalog.Build("neutral-mirror", 1);

        var result = new MatchSimulation(config).Simulate(setup);
        var counters = result.Diagnostics;

        var shotsMade = result.Events.Count(e => e.Type == DreamTeam.MatchEngine.Events.MatchEventType.ShotMade);
        var shotsMissed = result.Events.Count(e => e.Type == DreamTeam.MatchEngine.Events.MatchEventType.ShotMissed);
        var turnovers = result.Events.Count(e => e.Type == DreamTeam.MatchEngine.Events.MatchEventType.Turnover);
        var fouls = result.Events.Count(e => e.Type == DreamTeam.MatchEngine.Events.MatchEventType.Foul);
        var freeThrows = result.Events.Count(e =>
            e.Type == DreamTeam.MatchEngine.Events.MatchEventType.FreeThrowAttempt);
        var freeThrowMakes = result.Events.Count(e =>
            e.Type == DreamTeam.MatchEngine.Events.MatchEventType.FreeThrowMade);
        var possessions = result.Events.Count(e =>
            e.Type == DreamTeam.MatchEngine.Events.MatchEventType.PossessionStarted);

        Assert.Equal(shotsMade, counters.ShotsMade);
        Assert.Equal(shotsMissed, counters.ShotsMissed);
        Assert.Equal(turnovers,
            counters.TurnoversLostBall + counters.TurnoversOffensiveFoul + counters.TurnoversShotClockViolation);
        Assert.Equal(fouls,
            counters.FoulsShooting + counters.FoulsNonShooting + counters.FoulsOffensive);
        Assert.Equal(freeThrows, counters.FreeThrowsAttempted);
        Assert.Equal(freeThrowMakes, counters.FreeThrowsMade);
        Assert.Equal(possessions, counters.PossessionsStarted);
    }

    [Fact]
    public void EveryMetricIsFiniteInALargeRun()
    {
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(matches: 2_000));

        var report = BalanceReportBuilder.Build("neutral-mirror", outcome.Summary, outcome.ConfigHash);

        foreach (var side in new[] { report.Home, report.Away })
        {
            Assert.InRange(side.OffensiveReboundRate, 0, 1);
            Assert.InRange(side.OffensiveRating, 50, 200);
            Assert.InRange(side.TurnoversPerPossession, 0, 1);
            Assert.InRange(side.FoulsPerPossession, 0, 1);
            Assert.InRange(side.AssistsPerPossession, 0, 1);
            Assert.InRange(side.FreeThrowsPerPossession, 0, 2);
        }

        Assert.InRange(report.Pace48, 40, 200);
        Assert.InRange(report.AveragePeriods, 4, 6);
        Assert.InRange(report.AverageHomeScore, 50, 200);
        Assert.InRange(report.AverageAwayScore, 50, 200);
    }

    [Fact]
    public void ExtremeRatingsDoNotCorruptTheStructure()
    {
        // 08 S5 "Extremes: NaN, sonsuz dongu, yapusal bozulma yok."
        var catalog = new FixtureCatalog();
        var setup = catalog.Build("neutral-mirror", 3) with
        {
            Home = catalog.Build("neutral-mirror", 3).Home with
            {
                Team = catalog.Build("neutral-mirror", 3).Home.Team with
                {
                    Roster = [.. catalog.Build("neutral-mirror", 3).Home.Team.Roster.Select(player =>
                        player with { Ratings = Extreme(player.Ratings) })],
                },
            },
        };

        var summary = new MatchRunner(M6TestData.Config()).Run(setup);

        Assert.True(summary.Completed, $"Mac bitmedi: {summary.AbortReason}");
        Assert.True(summary.HomeScore > 0);
        Assert.True(summary.AwayScore > 0);
    }

    [Fact]
    public void TheReportNamesTheAbsenceOfARealSeasonReference()
    {
        // D98c: rapor NBA sezonuyla karsilastirma YAPMAMALI ve bunu soylemeli.
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(matches: 30));

        var report = BalanceReportBuilder.Build("neutral-mirror", outcome.Summary, outcome.ConfigHash);
        var manifest = ExperimentManifestFactory.Create(
            "test", "neutral-mirror", M6TestData.Plan(matches: 30),
            "config", "v0.1", outcome.ConfigHash, outcome);

        Assert.Contains("NBA sezonuyla karşılaştırma YAPILMAZ", report.ToText(), StringComparison.Ordinal);
        Assert.Contains("hayir (D98c)", manifest.ToText(), StringComparison.Ordinal);
        Assert.False(manifest.ComparedAgainstRealSeasonData);
    }

    [Fact]
    public void TheReportNeverClaimsByteEquality()
    {
        var outcome = new BatchDriver(new FixtureCatalog(), M6TestData.Config())
            .Run(M6TestData.Plan(matches: 5));

        var manifest = ExperimentManifestFactory.Create(
            "test", "neutral-mirror", M6TestData.Plan(matches: 5),
            "config", "v0.1", outcome.ConfigHash, outcome);

        Assert.Contains("VARILMAZ", manifest.ToText(), StringComparison.Ordinal);
        Assert.False(manifest.ByteEqualityClaimed);
    }

    [Fact]
    public void HoldoutSeedsAreOutsideTheTuningRange()
    {
        var plan = M6TestData.Plan(matches: 100, seedStart: 0, holdoutFrom: 10_001);

        Assert.False(BatchDriver.IsHoldout(plan, 0));
        Assert.False(BatchDriver.IsHoldout(plan, 10_000));
        Assert.True(BatchDriver.IsHoldout(plan, 10_001));
        Assert.True(BatchDriver.IsHoldout(plan, 50_000));
    }

    private static DreamTeam.Domain.Players.PlayerRatings Extreme(
        DreamTeam.Domain.Players.PlayerRatings ratings) => ratings with
        {
            Speed = 1,
            Strength = 100,
            Vertical = 1,
            Stamina = 100,
            Inside = 1,
            MidRange = 100,
            ThreePoint = 1,
            FreeThrow = 100,
            BallHandling = 1,
            Passing = 100,
            OffBall = 1,
            PostOffense = 100,
            PerimeterDefense = 1,
            InteriorDefense = 100,
            Steal = 1,
            Block = 100,
            Rebounding = 1,
            BasketballIQ = 100,
        };
}
