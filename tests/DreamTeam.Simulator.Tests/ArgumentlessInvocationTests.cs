using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator;
using DreamTeam.Simulator.Cli;
using DreamTeam.Simulator.Fixture;

namespace DreamTeam.Simulator.Tests;

/// <summary>
/// M6: D33 geri regresyonu.
///
/// <para><b>D33 (M2) diyordu:</b> "bu giris noktasi argümansizdir; tek sabit
/// kurgusal fixture'i oynar. CLI bayraklari, batch kipi, JSON/CSV export ve
/// fixture dosyalari M6'ya aittir." M6 o kisiti kaldirdi. Kaldirirken argümansiz
/// cagirmanin <b>AYNI maçı</b> oynamasini korumak zorundaydi.</para>
///
/// <para><b>Bu regresyon gercekten kirildi.</b> Yeni fixture katalogu rating
/// profilini düz (flat) hale getirdiginde varsayilan mac 112-118 / 5 periyottan
/// 102-110 / 4 periyota dustu. Yakalandi ve duzeltildi.</para>
///
/// <para><b>Ama sonra degerler yine degisti</b> — bu sefer KALIBRASYON yuzunden,
/// yanlislik degil. Ayrica "argümansiz cagirma" ile "single komutu" ayni maci
/// oynar ilkesi testte korunuyor; sayilarin kendisi degisti.</para>
///
/// <para>ASCII yorum kullanir (depo kurali).</para>
/// </summary>
public class ArgumentlessInvocationTests
{
    [Fact]
    public void TheArgumentlessInvocationMatchesTheSingleCommand()
    {
        var argumentless = Capture([]);

        var explicitCommand = Capture(
        [
            "single",
            "--fixture", CommandLine.DefaultFixture,
            "--seed", CommandLine.DefaultSeed.ToString(),
        ]);

        Assert.Equal(0, explicitCommand.ExitCode);
        Assert.Equal(argumentless.Output, explicitCommand.Output);
        Assert.Equal(argumentless.ExitCode, explicitCommand.ExitCode);
    }

    [Fact]
    public void TheDefaultMatchIsTheRecordedCalibratedMatch()
    {
        // M5'te seed 20260927 argümansiz kosuda 112-118 / 5 periyot uretirdi.
        // M6 kalibrasyonu belgedeki katsayilari degistirdi; degerler BILINCLI
        // olarak degisti. Burada sabitlenen sey M5'in degeri DEGIL, kalibre
        // belgenin urettigi degerdir. Kalibrasyonu geri almak isteyen biri
        // once buradaki sayilari GOZDEN GECIRMELI.
        var output = Capture([]).Output;

        Assert.Contains("Status       : Completed", output, StringComparison.Ordinal);
        Assert.Contains("Periods      : 4   Elapsed: 2880.0 s", output, StringComparison.Ordinal);
        Assert.Contains("137 - 149", output, StringComparison.Ordinal);
        Assert.Contains("127 - 125", output, StringComparison.Ordinal);
        Assert.Contains("Toplam event : 1264", output, StringComparison.Ordinal);
        Assert.Contains("ConfigHash   : 91cbb2e60d78fd9a", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheArgumentlessInvocationDoesNotDependOnTheFileSystem()
    {
        // Fixture'lar kodda uretilir; dosya yoksa da argümansiz kosu calisir.
        // Config belgesi yoksa gomulu baseline kullanilir ve BU ACIKCA yazilir.
        var output = Capture([]).Output;

        Assert.DoesNotContain("bulunamadi", output, StringComparison.Ordinal);
        Assert.Contains("ConfigHash", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RunningTheSameInvocationTwiceGivesByteIdenticalOutput()
    {
        // T01'in CLI seviyesindeki karsiligi.
        var first = Capture([]);
        var second = Capture([]);

        Assert.Equal(first.Output, second.Output);
    }

    [Fact]
    public void AnUnknownFixtureExitsWithTheUsageCodeAndListsTheValidNames()
    {
        var result = Capture(["single", "--fixture", "yok-boyle-bir"]);

        Assert.Equal(CommandLine.ExitUsage, result.ExitCode);
        Assert.Contains("neutral-mirror", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Status       : Completed", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AMalformedArgumentExitsWithTheUsageCodeAndShowsHelp()
    {
        var result = Capture(["batch", "--matches", "cok"]);

        Assert.Equal(CommandLine.ExitUsage, result.ExitCode);
        Assert.Contains("Kullanım", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AJsonFixturePathIsAccepted()
    {
        var path = Path.Combine(
            M6TestData.FixtureDirectory,
            "neutral-mirror.json");

        Assert.True(File.Exists(path), $"Fixture dosyasi eksik: {path}");

        var result = Capture(["single", "--fixture", path, "--seed", "5"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Completed", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheJsonFixturePathProducesTheSameMatchAsTheCatalogName()
    {
        var byName = Capture(["single", "--fixture", "neutral-mirror", "--seed", "5"]).Output;
        var byPath = Capture(
            ["single", "--fixture", Path.Combine(M6TestData.FixtureDirectory, "neutral-mirror.json"), "--seed", "5"])
            .Output;

        // MatchId catalogda sabit, dosyada isimden turetilir; bu tek satir farkli
        // olabilir. Skor ve istatistikler ayni olmali.
        foreach (var line in new[] { "SCORE", "Possessions", "FG  ", "3P  ", "FT  ", "Periods" })
        {
            Assert.Equal(
                LineContaining(byName, line),
                LineContaining(byPath, line));
        }
    }

    [Fact]
    public void ABatchRunWritesTheFourArtefactsAndReturnsSuccess()
    {
        var directory = Directory.CreateTempSubdirectory("m6-cli");

        try
        {
            var result = Capture(
            [
                "batch", "--fixture", "neutral-mirror", "--matches", "25",
                "--seed-start", "1", "--output", directory.FullName,
            ]);

            Assert.Equal(0, result.ExitCode);

            foreach (var artefact in new[] { "summary.json", "summary.csv", "manifest.json", "report.txt" })
            {
                Assert.True(
                    File.Exists(Path.Combine(directory.FullName, artefact)),
                    $"Cikti eksik: {artefact}");
            }

            var csv = File.ReadAllText(Path.Combine(directory.FullName, "summary.csv"));
            var json = File.ReadAllText(Path.Combine(directory.FullName, "summary.json"));

            Assert.Contains("section,metric,value", csv, StringComparison.Ordinal);
            Assert.Contains("\"MatchCount\": 25", json, StringComparison.Ordinal);
            Assert.Contains("DENGE RAPORU", result.Output, StringComparison.Ordinal);
            Assert.Contains("DETERMINISM MANIFESTI", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ExportFixturesWritesOneFilePerFixture()
    {
        var directory = Directory.CreateTempSubdirectory("m6-export");

        try
        {
            var result = Capture(["export-fixtures", "--output", directory.FullName]);

            Assert.Equal(0, result.ExitCode);

            foreach (var name in new FixtureCatalog().Names)
            {
                Assert.True(
                    File.Exists(Path.Combine(directory.FullName, $"{name}.json")),
                    $"Fixture yazilmadi: {name}");
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ABatchRunAppliesTheSameTacticOverrideAsASingleRun()
    {
        // REGRESYON. Taktik bayraklari once yalniz `single` komutunda
        // uygulaniyordu; `batch` dogrudan kaynaktan setup kuruyordu. Uc
        // farkli taktigin 1.500 maclik raporlari bayt bayt AYNI cikti ve
        // savunma matrisi hic olculemedi. Tek macin raporunda taktik degismis
        // gorundugu icin hata ancak iki komut karsilastirilunca yakalandi.

        // Toplu komut tohumu DOGRUDAN degil, INDEKSTEN turetir (08 S4):
        // `seed(i) = mix(seedStart + i)`. Tek macla karsilastirmak icin tek
        // mac komutuna da ayni turetilmis tohum verilir; boylece karsilastirilan
        // sey tohum degil, TAKTIK UYGULAMASIDIR.
        var derived = BatchDriver.SeedFor(7).ToString();
        var single = Capture(["single", "--fixture", "neutral-mirror", "--seed", derived, "--tactics", "InsidePost"]);
        var batch = Capture(
            ["batch", "--fixture", "neutral-mirror", "--matches", "1", "--seed-start", "7", "--tactics", "InsidePost"]);

        Assert.Contains("Ev InsidePost", single.Output, StringComparison.Ordinal);
        // Tek maclik toplu kosunun ev skoru tek macin ev skoruyla ayni olmali.
        // Iki bicim de farkli oldugu icin her biri kendi ayristiricisini kullanir.
        var singleHome = HomeScoreOf(single.Output, "SCORE");
        var batchHome = HomeScoreOf(batch.Output, "Ortalama skor");

        Assert.NotNull(singleHome);
        Assert.NotNull(batchHome);

        // Tek mac tam sayi ("143"), toplu rapor ondalik ("143.00") basar. Iki
        // bicim de ayni SAYIyi soylemeli; metin karsilastirmasi bicim farkini
        // hata sanirdi.
        Assert.Equal(
            double.Parse(singleHome, System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(batchHome, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DifferentTacticsProduceDifferentBatchResults()
    {
        // Bayrak uygulaniyorsa iki taktik farkli sonuc vermeli.
        var balanced = Capture(
            ["batch", "--fixture", "neutral-mirror", "--matches", "300", "--seed-start", "1", "--tactics", "Balanced"]).Output;
        var inside = Capture(
            ["batch", "--fixture", "neutral-mirror", "--matches", "300", "--seed-start", "1", "--tactics", "InsidePost"]).Output;
        var perimeter = Capture(
            ["batch", "--fixture", "neutral-mirror", "--matches", "300", "--seed-start", "1", "--tactics", "PerimeterMotion"]).Output;

        var balancedThree = LineContaining(balanced, "3PA payi");
        var insideThree = LineContaining(inside, "3PA payi");
        var perimeterThree = LineContaining(perimeter, "3PA payi");

        Assert.NotEqual(balancedThree, insideThree);
        Assert.NotEqual(insideThree, perimeterThree);
        Assert.NotEqual(balancedThree, perimeterThree);
    }

    /// <summary>
    /// Tek mac raporunda "Kuzey Yildizlari 127 - 153", toplu raporda
    /// "Ev 129.00 / Dep 128.00". Her ikisinden de EV skoru cikarilir; boylece
    /// iki bicim dogrudan karsilastirilabilir.
    /// </summary>
    private static string? HomeScoreOf(string output, string marker)
    {
        var line = output.Split('\n').FirstOrDefault(candidate => candidate.Contains(marker, StringComparison.Ordinal));

        if (line is null)
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(line, @"\d+(?:\.\d+)?");

        return match.Success ? match.Value : null;
    }

    [Fact]
    public void HelpIsPrintedWithoutTouchingTheEngine()
    {
        var result = Capture(["help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("export-fixtures", result.Output, StringComparison.Ordinal);
    }

    private static string LineContaining(string text, string needle) =>
        text.Split('\n').First(line => line.Contains(needle, StringComparison.Ordinal));

    private static (int ExitCode, string Output, string Error) Capture(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = new SimulatorRunner(output, error).Run(args);

        return (exitCode, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
