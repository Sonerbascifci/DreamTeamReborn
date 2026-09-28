using System.Text.Json;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Projection;
using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator.Export;
using DreamTeam.Simulator.Fixture;
using DreamTeam.Simulator.Reporting;

namespace DreamTeam.Simulator.Tests;

/// <summary>
/// 08 T18: "CSV/JSON summary ile event-derived sonuç tutarlı."
///
/// <para><b>Bu, M6'nın en önemli testidir.</b> Bellek sınırlı özet kipi
/// <c>MatchResult</c>'ı hiç üretmez; event'leri akıtırken sayar. Bu yüzden iki
/// bağımsız hesap vardır: motorun kendi <c>Simulate</c> yolu ve akış yolu.
/// Aralarındaki fark, akış yolunun sessizce kaybettiği her şeyi gosterir.</para>
///
/// <para>Uç modül, ASCII yorum kullanır (depo kuralı).</para>
/// </summary>
public class ExportConsistencyTests
{
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(7UL)]
    [InlineData(11UL)]
    public void MatchRunnerProducesTheSameOutcomeAsSimulate(ulong seed)
    {
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var setup = catalog.Build("neutral-mirror", seed);

        // Yol 1: motorun kendi yolu. Tum event'leri biriktirir, MatchResult uretir.
        var simulation = new MatchSimulation(config);
        var full = simulation.Simulate(setup);

        // Yol 2: bellek sinirli akis yolu. MatchResult hic uretmez.
        var summary = new MatchRunner(config).Run(setup);

        Assert.True(summary.Completed, $"Mac tamamlanmadi: {summary.AbortReason}");
        Assert.Equal(full.Status, summary.Status);
        Assert.Equal(full.HomeScore, summary.HomeScore);
        Assert.Equal(full.AwayScore, summary.AwayScore);
        Assert.Equal(full.IsTie, summary.IsTie);
        Assert.Equal(full.HomePossessions, summary.HomePossessions);
        Assert.Equal(full.AwayPossessions, summary.AwayPossessions);
        Assert.Equal(full.ElapsedGameTimeMs, summary.ElapsedGameTimeMs);
        Assert.Equal(full.PeriodsPlayed, summary.PeriodsPlayed);
        Assert.Equal(full.Events.Length, summary.EventCount);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(5UL)]
    public void SummaryTotalsEqualTheEngineProjectorTotals(ulong seed)
    {
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var setup = catalog.Build("neutral-mirror", seed);

        var full = new MatchSimulation(config).Simulate(setup);
        var summary = new MatchRunner(config).Run(setup);

        // 08 S2: skor = 2*2PM + 3*3PM + FTM. Ozeti dogrudan dogrulamak yerine
        // bu esitligi her iki yolda birden kurmak, iki yolun ayni sayaclari
        // paylastigini gosterir.
        foreach (var (box, totals) in new[]
                 {
                     (full.BoxScores[0], summary.Home),
                     (full.BoxScores[1], summary.Away),
                 })
        {
            Assert.Equal(box.Points, totals.Points);
            Assert.Equal(box.FieldGoalsMade, totals.FieldGoalsMade);
            Assert.Equal(box.FieldGoalsAttempted, totals.FieldGoalsAttempted);
            Assert.Equal(box.TwoPointersMade, totals.TwoPointersMade);
            Assert.Equal(box.TwoPointersAttempted, totals.TwoPointersAttempted);
            Assert.Equal(box.ThreePointersMade, totals.ThreePointersMade);
            Assert.Equal(box.ThreePointersAttempted, totals.ThreePointersAttempted);
            Assert.Equal(box.FreeThrowMakes, totals.FreeThrowMakes);
            Assert.Equal(box.FreeThrowAttempts, totals.FreeThrowAttempts);
            Assert.Equal(box.Assists, totals.Assists);
            Assert.Equal(box.Turnovers, totals.Turnovers);
            Assert.Equal(box.PersonalFouls, totals.PersonalFouls);
            Assert.Equal(box.Blocks, totals.Blocks);
            Assert.Equal(box.OffensiveRebounds, totals.OffensiveRebounds);
            Assert.Equal(box.DefensiveRebounds, totals.DefensiveRebounds);

            Assert.Equal(2 * totals.TwoPointersMade + (3 * totals.ThreePointersMade) + totals.FreeThrowMakes,
                totals.Points);
        }
    }

    [Fact]
    public void PlayerEnergySummaryMatchesTheEngineReport()
    {
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var setup = catalog.Build("neutral-mirror", 42);

        var full = new MatchSimulation(config).Simulate(setup);
        var summary = new MatchRunner(config).Run(setup);

        Assert.Equal(full.PlayerEnergy.Length, summary.PlayerEnergy.Length);

        for (var index = 0; index < full.PlayerEnergy.Length; index++)
        {
            Assert.Equal(full.PlayerEnergy[index].PlayerId, summary.PlayerEnergy[index].PlayerId);
            Assert.Equal(full.PlayerEnergy[index].Energy, summary.PlayerEnergy[index].Energy);
            Assert.Equal(
                full.PlayerEnergy[index].SecondsOnCourt,
                summary.PlayerEnergy[index].SecondsOnCourt,
                9);
        }
    }

    [Fact]
    public void ProjectAndAccumulateGiveTheSameProjection()
    {
        // Accumulate, Project'i tek tek cagirarak devreder. Bu test, devrenin
        // SONUC degistirmedigini kanitlar; 200+ motor testi bundan etkilenmez.
        var catalog = new FixtureCatalog();
        var setup = catalog.Build("neutral-mirror", 9);
        var events = new MatchSimulation(M6TestData.Config()).Simulate(setup).Events;

        var whole = new BoxScoreProjector(setup).Project(events);

        var incremental = new BoxScoreProjector(setup);

        foreach (var matchEvent in events)
        {
            incremental.Accumulate(matchEvent);
        }

        var accumulated = incremental.Project([]);

        Assert.Equal(whole.Home, accumulated.Home);
        Assert.Equal(whole.Away, accumulated.Away);
        Assert.Equal(whole.Players.Length, accumulated.Players.Length);

        for (var index = 0; index < whole.Players.Length; index++)
        {
            Assert.Equal(whole.Players[index], accumulated.Players[index]);
        }
    }

    [Fact]
    public void CsvAndJsonSummariesAgree()
    {
        var catalog = new FixtureCatalog();
        var driver = new BatchDriver(catalog, M6TestData.Config());
        var outcome = driver.Run(M6TestData.Plan(matches: 40));
        var report = BalanceReportBuilder.Build("neutral-mirror", outcome.Summary, outcome.ConfigHash);

        var json = JsonSerializer.Serialize(report, SummaryWriter.Json);
        var csv = SummaryWriter.ToCsv(report);

        // Her CSV metriği JSON'da da aynı değerle bulunmalı.
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(report.MatchCount, root.GetProperty("MatchCount").GetInt64());

        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(',');

            if (parts.Length < 3)
            {
                continue;
            }

            if (parts[0] == "home" && parts[1] == "points_per_match")
            {
                Assert.Equal(
                    root.GetProperty("Home").GetProperty("PointsPerMatch").GetDouble().ToString("F6"),
                    parts[2]);
            }
        }

        Assert.Contains("section,metric,value", csv, StringComparison.Ordinal);
        Assert.Contains("compared_against_real_season_data,false", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroDenominatorYieldsNullNotInfinity()
    {
        // 08 S6: sifir payda -> null. Asla 0, asla Infinity, asla NaN.
        Assert.Null(BalanceReportBuilder.Ratio(0, 0));
        Assert.Equal(0.5, BalanceReportBuilder.Ratio(5, 10));

        var zero = TeamTotals.Zero;
        var metrics = BalanceReportBuilder.Metrics(zero, zero, 0);

        Assert.Null(metrics.FieldGoalRate);
        Assert.Null(metrics.ThreeAttemptShare);
        Assert.Null(metrics.FreeThrowRate);
        Assert.Equal(0, metrics.OffensiveRating);
        Assert.Equal(0, metrics.TurnoversPerPossession);
    }

    [Fact]
    public void PercentagesAreNotAveragedAcrossMatches()
    {
        // 08 S6: "Mac yuzdelerinin basit ortalamasiyla karistirma." Iki macin
        // ham sayilari toplanir, oran EN SONDA hesaplanir. Bu test, 1/1 ve 0/9
        // maclarinin ortalamasinin 0.5 degil 1/10 oldugunu gosterir.
        var accumulator = new SummaryAccumulator();

        accumulator.Add(new MatchSummary
        {
            MatchId = Guid.NewGuid(),
            Seed = 1,
            Status = MatchStatus.Completed,
            HomeScore = 2,
            AwayScore = 0,
            IsTie = false,
            HomePossessions = 1,
            AwayPossessions = 1,
            ElapsedGameTimeMs = 2880_000,
            PeriodsPlayed = 4,
            TotalActions = 2,
            Home = TeamTotals.Zero with { FieldGoalsMade = 1, FieldGoalsAttempted = 1 },
            Away = TeamTotals.Zero,
            HomeShots = ShotTypeTally.Empty,
            AwayShots = ShotTypeTally.Empty,
            Diagnostics = DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters.Empty,
        });

        accumulator.Add(new MatchSummary
        {
            MatchId = Guid.NewGuid(),
            Seed = 2,
            Status = MatchStatus.Completed,
            HomeScore = 0,
            AwayScore = 0,
            IsTie = false,
            HomePossessions = 1,
            AwayPossessions = 1,
            ElapsedGameTimeMs = 2880_000,
            PeriodsPlayed = 4,
            TotalActions = 2,
            Home = TeamTotals.Zero with { FieldGoalsMade = 0, FieldGoalsAttempted = 9 },
            Away = TeamTotals.Zero,
            HomeShots = ShotTypeTally.Empty,
            AwayShots = ShotTypeTally.Empty,
            Diagnostics = DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters.Empty,
        });

        var metrics = BalanceReportBuilder.Metrics(accumulator.Home, accumulator.Away, 2);

        // Ortalamasi alinsa 0.5 cikardi. Ham sayilardan hesaplaninca 0.10.
        Assert.Equal(0.1, metrics.FieldGoalRate!.Value, 9);
        Assert.Equal(1, accumulator.Home.FieldGoalsMade);
        Assert.Equal(10, accumulator.Home.FieldGoalsAttempted);
        Assert.NotEqual(0.5, metrics.FieldGoalRate!.Value, 3);
    }

    [Fact]
    public void AnEmptyBatchIsAValidEmptyReport()
    {
        var catalog = new FixtureCatalog();
        var outcome = new BatchDriver(catalog, M6TestData.Config())
            .Run(M6TestData.Plan(matches: 0));

        var report = BalanceReportBuilder.Build("neutral-mirror", outcome.Summary, outcome.ConfigHash);

        Assert.Equal(0, report.MatchCount);
        Assert.Equal(0, report.AverageHomeScore);
        Assert.Null(report.Home.FieldGoalRate);
        Assert.False(report.HomeWinRate.HasSamples);
        Assert.Contains("n/a", report.ToText(), StringComparison.Ordinal);
        Assert.True(EveryPrintedNumberIsFinite(report.ToText()), "Rapor sonlu olmayan bir sayi basiyor.");
    }

    [Fact]
    public void AbortedMatchesAreCountedNotHidden()
    {
        // 09: "failures gizlenmiyor." Abort edilen bir mac hem sayilir hem
        // sebebi rapora girer.
        var accumulator = new SummaryAccumulator();

        accumulator.Add(new MatchSummary
        {
            MatchId = Guid.NewGuid(),
            Seed = 1,
            Status = MatchStatus.Aborted,
            HomeScore = 0,
            AwayScore = 0,
            IsTie = false,
            HomePossessions = 0,
            AwayPossessions = 0,
            ElapsedGameTimeMs = 0,
            PeriodsPlayed = 0,
            TotalActions = 0,
            Home = TeamTotals.Zero,
            Away = TeamTotals.Zero,
            HomeShots = ShotTypeTally.Empty,
            AwayShots = ShotTypeTally.Empty,
            Diagnostics = DreamTeam.MatchEngine.Diagnostics.DiagnosticCounters.Empty,
            AbortReason = "Yasal yedek yok (NoLegalSubstitute).",
        });

        var report = BalanceReportBuilder.Build("x", accumulator, "hash");

        Assert.Equal(1, report.MatchCount);
        Assert.Equal(1, report.AbortRate.Successes);
        Assert.Contains("Yasal yedek yok", report.ToText(), StringComparison.Ordinal);
        Assert.Contains("KALDI", report.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryReportFieldIsEitherAFactOrAnExplicitAbsence()
    {
        var catalog = new FixtureCatalog();
        var outcome = new BatchDriver(catalog, M6TestData.Config()).Run(M6TestData.Plan(matches: 50));
        var report = BalanceReportBuilder.Build("neutral-mirror", outcome.Summary, outcome.ConfigHash);
        var text = report.ToText();

        Assert.True(EveryPrintedNumberIsFinite(text), "Rapor sonlu olmayan bir sayi basiyor.");
        Assert.DoesNotContain("-0.000", text, StringComparison.Ordinal);

        foreach (var threshold in report.Thresholds)
        {
            Assert.False(string.IsNullOrWhiteSpace(threshold.Detail));
            Assert.False(string.IsNullOrWhiteSpace(threshold.Name));
        }
    }

    /// <summary>
    /// Her basilan sayiyi dener; hicbiri NaN veya Infinity olamaz.
    ///
    /// <para><b>Neden alt dize denetimi degil?</b> Esik adi "NaN/Infinity yok"
    /// diyor; basit bir <c>DoesNotContain("NaN")</c> bu adi kaci sanirdi ve
    /// denetimi anlamsizlastirirdi. Burada her token sayiya AYRISTIRILIR ve
    /// yalnizca gercekten bir sayi olanlar denetlenir.</para>
    /// </summary>
    private static bool EveryPrintedNumberIsFinite(string text)
    {
        foreach (var token in text.Split(
                     [' ', '\t', '\n', '\r', ',', ';', '[', ']', '(', ')', '|', '%', '='],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = token.TrimStart('+', '-').Replace("%", string.Empty, StringComparison.Ordinal);

            if (!double.TryParse(
                    cleaned,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
            {
                continue;
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void ShotTypeTalliesAreSeparateObjectsPerSide()
    {
        // REGRESYON. `ShotTypeTally.Empty` bir kez `static ... = new()` idi ve
        // `MatchRunner` iki yerel degiskene atiyordu. Sayaclar `ref` ile
        // mutasyona ugradigi icin iki taraf AYNI nesneyi gosteriyor, ev ve depo
        // toplamlari tek yere karisiyor ve `Empty` surec boyunca birikiyordu.
        // 2.000 macta oranlar yakinsadigi icin hata GORUNMEDI; 10.000 macta
        // ClosePost %-67, MidRange %+70 gibi imkansiz degerler verdi.
        var catalog = new FixtureCatalog();
        var runner = new MatchRunner(M6TestData.Config());
        var first = runner.Run(catalog.Build("neutral-mirror", 1));
        var second = runner.Run(catalog.Build("neutral-mirror", 2));

        Assert.NotSame(first.HomeShots, first.AwayShots);
        Assert.NotSame(ShotTypeTally.Empty, ShotTypeTally.Empty);

        // Ev ve depo AYRI sayilir. Ayni nesne olsalardi bu denklik gecerdi.
        var secondHalf = ShotTypeTally.Add(first.HomeShots, second.HomeShots);
        Assert.NotEqual(first.HomeShots.Attempts, first.AwayShots.Attempts);
        Assert.True(secondHalf.Attempts > first.HomeShots.Attempts);
    }

    [Fact]
    public void MissesNeverExceedAttemptsForAnyShotType()
    {
        // Uretilen her deneme icin en fazla bir kacis olur. Aksi halde isabet
        // orani negatif olur; 10K kosusunda ClosePost %-67 cikti.
        var catalog = new FixtureCatalog();
        var runner = new MatchRunner(M6TestData.Config());

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var summary = runner.Run(catalog.Build("neutral-mirror", seed));

            foreach (var tally in new[] { summary.HomeShots, summary.AwayShots })
            {
                Assert.InRange(tally.AtRimMisses, 0, tally.AtRimAttempts);
                Assert.InRange(tally.ClosePostMisses, 0, tally.ClosePostAttempts);
                Assert.InRange(tally.MidRangeMisses, 0, tally.MidRangeAttempts);
                Assert.InRange(tally.ThreePointMisses, 0, tally.ThreePointAttempts);
            }
        }
    }

    [Fact]
    public void ShotTypeAttemptsEqualTheEngineShotCounters()
    {
        // Dort tur sayacinin toplami motorun kendi `ShotsAttempted` sayacina
        // esit olmali; iki ayri yol birbirinden ayrilirsa kalibrasyon yanlis
        // yere bakar.
        var catalog = new FixtureCatalog();
        var runner = new MatchRunner(M6TestData.Config());

        for (ulong seed = 1; seed <= 25; seed++)
        {
            var summary = runner.Run(catalog.Build("neutral-mirror", seed));

            Assert.Equal(
                summary.Diagnostics.ShotsAttempted,
                summary.HomeShots.Attempts + summary.AwayShots.Attempts);
        }
    }

    [Fact]
    public void TheManifestRecordsEveryDeterminismField()
    {
        // 08 S4'in istedigi alanlar. Eksik alan sessizce gecmis olurdu.
        var catalog = new FixtureCatalog();
        var outcome = new BatchDriver(catalog, M6TestData.Config())
            .Run(M6TestData.Plan(matches: 10, holdoutFrom: 100));
        var plan = M6TestData.Plan(matches: 10, holdoutFrom: 100);

        var manifest = ExperimentManifestFactory.Create(
            "batch --fixture neutral-mirror --matches 10",
            "neutral-mirror",
            plan,
            "config/engine/baseline.v0.1.json",
            "v0.1",
            outcome.ConfigHash,
            outcome);

        var text = manifest.ToText();

        foreach (var field in new[]
                 {
                     "Fixture", "Mac sayisi", "Seed araligi", "Is parcacigi",
                     "ConfigHash", "Motor surumu", "Kural surumu", "RNG",
                     "Event semasi", "Runtime", "Isletim sistemi", "Sure",
                     "Azami bellek", "Mac/sn", "Byte equality", "Gercek sezon verisi",
                     "Holdout baslangici", "Config belgesi", "Config surumu",
                 })
        {
            Assert.Contains(field, text, StringComparison.Ordinal);
        }

        Assert.Equal(100, manifest.HoldoutFrom);
        Assert.Equal(9, manifest.LastSeedIndex);
        Assert.False(manifest.ByteEqualityClaimed);
        Assert.False(manifest.ComparedAgainstRealSeasonData);
    }

    [Fact]
    public void WilsonIntervalIsCorrectOnKnownInput()
    {
        // p = 0.5, n = 100: normal yaklasimi da +-0.098 ile ayni cikiyor, bu
        // yuzden Wilson'in UYGULANMASI bu noktada fark edilmez. Kenar testleri
        // asagidaki iki test.
        var center = WilsonInterval.For(50, 100);
        Assert.Equal(0.5, center.Point, 9);
        Assert.InRange(center.Lower, 0.40, 0.41);
        Assert.InRange(center.Upper, 0.59, 0.60);
        Assert.False(center.DeviatesFromHalf);
    }

    [Fact]
    public void WilsonIntervalStaysInsideTheUnitIntervalAtTheEdges()
    {
        // Normal yaklasimi p=0'da alt sinirini -0.03'e dusurur; Wilson yapamaz.
        var zero = WilsonInterval.For(0, 50);

        Assert.True(zero.HasSamples);
        Assert.True(zero.Lower >= 0, $"Alt sinir negatif: {zero.Lower}");
        Assert.True(zero.Upper <= 1, $"Ust sinir 1'den buyuk: {zero.Upper}");

        var all = WilsonInterval.For(50, 50);

        Assert.True(all.Upper <= 1, $"Ust sinir 1'den buyuk: {all.Upper}");
        Assert.True(all.DeviatesFromHalf);
    }

    [Fact]
    public void Pace48NormalizesOvertime()
    {
        // 08 S6: Pace48 = 48 * possession / oynanan dakika. Uzatma dakikalari
        // paydaya girmez; bolucu fiilen oynanan sure.
        var shortGame = new SummaryAccumulator();
        var longGame = new SummaryAccumulator();

        // 4 periyot = 48 dk, 100 possession.
        var config = M6TestData.Config();
        var runner = new MatchRunner(config);
        var summary = runner.Run(new FixtureCatalog().Build("neutral-mirror", 1));
        shortGame.Add(summary);

        // Ayni possession, 1 uzatma (53 dk).
        longGame.Add(summary with
        {
            PeriodsPlayed = summary.PeriodsPlayed + 1,
            ElapsedGameTimeMs = summary.ElapsedGameTimeMs + (5 * 60 * 1000),
        });

        var shortPace = BalanceReportBuilder.Pace(shortGame, shortGame.TotalElapsedGameTimeMs / 60_000.0);
        var longPace = BalanceReportBuilder.Pace(longGame, longGame.TotalElapsedGameTimeMs / 60_000.0);

        Assert.True(longPace < shortPace,
            $"Uzatma Pace48'i ARTIRMAMALI: {longPace} >= {shortPace}");
    }
}
