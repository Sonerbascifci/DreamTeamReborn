using DreamTeam.MatchEngine.Config;
using DreamTeam.Simulator.Batch;
using DreamTeam.MatchEngine.Core;
using DreamTeam.Simulator.Cli;
using DreamTeam.Simulator.Fixture;

namespace DreamTeam.Simulator.Tests;

/// <summary>
/// M6: config belgesi ve fixture dosyalari.
///
/// <para><b>Drift testi.</b> `EveryCatalogFixtureRoundTripsThroughItsFile` yazilan
/// dosyayi okur ve kodun urettigi setup ile karsilastirir. Planin 15. bolumundeki
/// 10. risk "config/engine/ ve fixtures/ JSON'u drift eder" idi; bu test o riski
/// bir varsayimdan olculebilir bir gercege cevirir.</para>
///
/// <para>ASCII yorum kullanir (depo kurali).</para>
/// </summary>
public class FixtureSourceTests
{
    [Fact]
    public void TheCheckedInBalanceDocumentLoads()
    {
        var document = BalanceConfigStore.Load(M6TestData.BalanceConfigPath);

        // Belge motorun gomulu baseline'iyle AYNI olmamali artik: M6 kalibrasyonu
        // onu degistirdi. Esitligi test etmek, kalibrasyonu sessizce geri
        // almayi acar.
        var factory = BalanceConfigStore.BaselineDocument.ToEngineConfig().ComputeConfigHash();
        var calibrated = document.ToEngineConfig().ComputeConfigHash();

        Assert.NotEqual(factory, calibrated);
    }

    [Fact]
    public void TheCalibrationIsRecordedInTheDocumentNotes()
    {
        // 08 §119: "Her değişimde önce/sonra config hash kaydedilir." Degisikligin
        // gerekcesi belgede yazili olmali; hash tek basina ne oldugunu anlatmaz.
        var document = BalanceConfigStore.Load(M6TestData.BalanceConfigPath);

        Assert.Contains("ShotModel", document.Notes, StringComparison.Ordinal);
        Assert.Contains("ActionModel", document.Notes, StringComparison.Ordinal);
        Assert.Contains("2000", document.Notes, StringComparison.Ordinal);
        Assert.Contains("D98c", document.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSimulatorAndTheTestsUseTheSameConfig()
    {
        // Iki farkli config ile olcum yapmak, raporun hangi motoru anlattigini
        // belirsizlastirir. Simulator belgeden okur; testler de belgeden okur.
        var runner = new BatchDriver(new FixtureCatalog(), M6TestData.Config());

        Assert.Equal(
            M6TestData.Config().ComputeConfigHash(),
            runner.ConfigHash);
    }

    [Fact]
    public void TheBalanceDocumentRoundTrips()
    {
        var document = BalanceConfigStore.Load(M6TestData.BalanceConfigPath);
        var json = BalanceConfigStore.Serialize(document);
        var again = BalanceConfigStore.Deserialize(json, "test");

        Assert.Equal(
            document.ToEngineConfig().ComputeConfigHash(),
            again.ToEngineConfig().ComputeConfigHash());
    }

    [Fact]
    public void TheBalanceDocumentDeclaresTheVersionItWasWrittenFor()
    {
        var document = BalanceConfigStore.Load(M6TestData.BalanceConfigPath);

        Assert.Equal(BalanceConfigDocument.CurrentSchemaVersion, document.SchemaVersion);
        Assert.Equal(EngineVersion.Current, document.EngineVersion);
        Assert.False(string.IsNullOrWhiteSpace(document.Notes));
    }

    [Fact]
    public void D98aIsAppliedInTheDocument()
    {
        // M5'te kaynaksiz bir yer tutucuydu; kullanici 5'i (NBA) secti.
        var document = BalanceConfigStore.Load(M6TestData.BalanceConfigPath);

        Assert.Equal(5, document.Rules.ShortTimeoutsPerTeam);
    }

    [Fact]
    public void AVersionMismatchIsRefusedRatherThanIgnored()
    {
        // 07 S7: uyumsuz surum sessizce yoksayilmaz.
        var document = BalanceConfigStore.BaselineDocument with { EngineVersion = "0.0.1-other" };
        var path = TempPath();

        BalanceConfigStore.Save(document, path);

        var error = Assert.Throws<InvalidOperationException>(
            () => BalanceConfigStore.Load(path));

        Assert.Contains("motor surumu", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASchemaMismatchIsRefused()
    {
        var document = BalanceConfigStore.BaselineDocument with
        {
            SchemaVersion = BalanceConfigDocument.CurrentSchemaVersion + 1,
        };

        var path = TempPath();
        BalanceConfigStore.Save(document, path);

        Assert.Throws<InvalidOperationException>(() => BalanceConfigStore.Load(path));
    }

    [Fact]
    public void AMissingDocumentIsRefusedWithAPathInTheMessage()
    {
        var error = Assert.Throws<FileNotFoundException>(
            () => BalanceConfigStore.Load("yok/bu/belge.json"));

        Assert.Contains("yok/bu/belge.json", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryCatalogFixtureRoundTripsThroughItsFile()
    {
        var catalog = new FixtureCatalog();
        var directory = Directory.CreateTempSubdirectory("m6-fixtures");

        try
        {
            var paths = new List<string>();

            foreach (var name in catalog.Names)
            {
                var path = Path.Combine(directory.FullName, $"{name}.json");
                JsonFixtureSource.Save(catalog.ToFile(name), path);
                paths.Add(path);
            }

            var source = new JsonFixtureSource(paths);

            foreach (var name in catalog.Names)
            {
                JsonFixtureSource.Validate(JsonFixtureSource.Load(
                    Path.Combine(directory.FullName, $"{name}.json")));

                var fromCode = catalog.Build(name, 4_242);
                var fromFile = source.Build(name, 4_242);

                Assert.Equal(fromCode.Home.Team.Id, fromFile.Home.Team.Id);
                Assert.Equal(fromCode.Home.Team.Name, fromFile.Home.Team.Name);
                Assert.Equal(fromCode.Home.Offensive, fromFile.Home.Offensive);
                Assert.Equal(fromCode.Home.Defense, fromFile.Home.Defense);
                Assert.Equal(fromCode.Home.Pace, fromFile.Home.Pace);
                Assert.Equal(fromCode.Away.Offensive, fromFile.Away.Offensive);
                Assert.Equal(fromCode.Away.Defense, fromFile.Away.Defense);
                Assert.Equal(fromCode.Away.Pace, fromFile.Away.Pace);
                Assert.Equal(fromCode.Seed, fromFile.Seed);
                Assert.Equal(
                    fromCode.Home.Team.Roster.Select(player => player.Id),
                    fromFile.Home.Team.Roster.Select(player => player.Id));

                Assert.Equal(
                    fromCode.Home.Team.Roster.Select(player => player.Ratings),
                    fromFile.Home.Team.Roster.Select(player => player.Ratings));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void TheCheckedInFixtureFilesMatchTheCatalog()
    {
        // Depodaki dosyalar katalogla ayni olmali. Degilse, ya dosya guncel
        // degil ya da katalog degismis demektir; ikisi de sessiz gecmemeli.
        //
        // D87: `FixtureTeamFile` ImmutableArray icerdigi icin `Assert.Equal`
        // KULLANILAMAZ - record esitligi referans esitligidir ve her zaman
        // False doner. Alan alan karsilastirilir. Bu, D87'nin M6'da yeni bir
        // yere bulastiginin kanitidir.
        var catalog = new FixtureCatalog();

        foreach (var name in catalog.Names)
        {
            var path = Path.Combine(M6TestData.FixtureDirectory, $"{name}.json");

            Assert.True(File.Exists(path), $"Fixture dosyasi eksik: {path}");

            var onDisk = JsonFixtureSource.Load(path);
            var fromCatalog = catalog.ToFile(name);

            AssertSameTeam(fromCatalog.Home, onDisk.Home, $"{name} ev");
            AssertSameTeam(fromCatalog.Away, onDisk.Away, $"{name} dep");
            Assert.Equal(fromCatalog.Family, onDisk.Family);
            Assert.Equal(fromCatalog.Name, onDisk.Name);
            Assert.Equal(fromCatalog.Description, onDisk.Description);
        }
    }

    /// <summary>
    /// D87-safe team comparison: every field, by value. A record-level
    /// <c>Assert.Equal</c> here would always fail, which is the whole point of
    /// D87 being measured rather than assumed.
    /// </summary>
    private static void AssertSameTeam(FixtureTeamFile expected, FixtureTeamFile actual, string label)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Offensive, actual.Offensive);
        Assert.Equal(expected.Defense, actual.Defense);
        Assert.Equal(expected.Pace, actual.Pace);
        // D87: ImmutableArray uzerinde xunit`in koleksiyon karsilastirmasi da
        // guvenilir DEGIL. ToArray() ile deger karsilastirmasina dusurulur.
        Assert.Equal(expected.LineupPlayerIds.ToArray(), actual.LineupPlayerIds.ToArray());
        Assert.Equal(expected.Roster.Length, actual.Roster.Length);

        for (var index = 0; index < expected.Roster.Length; index++)
        {
            var left = expected.Roster[index];
            var right = actual.Roster[index];

            Assert.Equal(left.Id, right.Id);
            Assert.Equal(left.DisplayName, right.DisplayName);
            Assert.Equal(left.Position, right.Position);
            Assert.Equal(left.Ratings, right.Ratings);
        }
    }

    [Fact]
    public void EveryFixtureIsValidForTheEngineValidator()
    {
        // Yanlis kadro, 10K mac harcanmadan once yakalanmali.
        var catalog = new FixtureCatalog();

        foreach (var name in catalog.Names)
        {
            var setup = catalog.Build(name, 1);
            var validation = MatchEngine.Core.MatchSetupValidator.Validate(setup);

            Assert.True(validation.IsValid, $"{name}: {string.Join("; ", validation.Errors)}");
        }
    }

    [Fact]
    public void AnUnknownFixtureNameFailsLoudly()
    {
        var catalog = new FixtureCatalog();

        var error = Assert.Throws<KeyNotFoundException>(() => catalog.Build("yok-boyle-bir", 1));

        Assert.Contains("yok-boyle-bir", error.Message, StringComparison.Ordinal);
        Assert.Contains("neutral-mirror", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFixtureWithALineupOutsideTheRosterIsRefused()
    {
        var file = new FixtureCatalog().ToFile("neutral-mirror");
        var broken = file with
        {
            Home = file.Home with
            {
                LineupPlayerIds = [Guid.NewGuid(), .. file.Home.LineupPlayerIds[1..]],
            },
        };

        var error = Assert.Throws<InvalidOperationException>(() => JsonFixtureSource.Validate(broken));

        Assert.Contains("kadroda yok", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFixtureWithTheWrongRosterSizeIsRefused()
    {
        var file = new FixtureCatalog().ToFile("neutral-mirror");
        var broken = file with { Home = file.Home with { Roster = file.Home.Roster.RemoveAt(0) } };

        var error = Assert.Throws<InvalidOperationException>(() => JsonFixtureSource.Validate(broken));

        Assert.Contains("kadro", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryBuiltInFixtureUsesEntirelyFictionalContent()
    {
        // Gercek kisi, kulup, lisansli veri yoktur.
        var catalog = new FixtureCatalog();

        foreach (var name in catalog.Names)
        {
            var setup = catalog.Build(name, 1);

            foreach (var team in new[] { setup.Home.Team, setup.Away.Team })
            {
                Assert.StartsWith("Oyuncu ", team.Roster[0].DisplayName, StringComparison.Ordinal);

                foreach (var player in team.Roster)
                {
                    Assert.StartsWith("Oyuncu ", player.DisplayName, StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public void TheMirrorFixtureIsSymmetricApartFromIdentity()
    {
        // 08 S5: "Taraf/baslangic bias'i gorunmemeli." Fixture'in kendisi
        // simetrik olmali; motorun cift Taraf cizimi 5+1 noktada tek atar.
        var catalog = new FixtureCatalog();
        var home = catalog.Build("neutral-mirror", 1);
        var swapped = catalog.Build("neutral-mirror-swapped", 1);

        Assert.Equal(home.Away.Team.Roster.Select(p => p.Ratings),
            swapped.Home.Team.Roster.Select(p => p.Ratings));
        Assert.Equal(home.Home.Team.Roster.Select(p => p.Ratings),
            swapped.Away.Team.Roster.Select(p => p.Ratings));
    }

    [Fact]
    public void TheQualityGapFixtureDiffersOnlyInStrength()
    {
        var catalog = new FixtureCatalog();
        var mirror = catalog.Build("neutral-mirror", 1);
        var gap = catalog.Build("quality-gap", 1);

        Assert.Equal(mirror.Away.Team.Roster.Select(p => p.Ratings),
            gap.Away.Team.Roster.Select(p => p.Ratings));

        foreach (var (strong, weak) in
                 gap.Home.Team.Roster.Zip(gap.Away.Team.Roster))
        {
            Assert.Equal(weak.Ratings.Speed + 8, strong.Ratings.Speed);
        }
    }

    [Fact]
    public void TheRosterFitFixtureMovesBudgetBetweenPerimeterAndInside()
    {
        var catalog = new FixtureCatalog();
        var fit = catalog.Build("roster-fit", 1);

        var home = fit.Home.Team.Roster[0].Ratings;
        var away = fit.Away.Team.Roster[0].Ratings;

        Assert.True(home.ThreePoint > away.ThreePoint, "Ev disaridan iyi olmali.");
        Assert.True(away.Inside > home.Inside, "Depasman ici iyi olmali.");
    }

    [Fact]
    public void ThePaceFixturesChangeOnlyPace()
    {
        var catalog = new FixtureCatalog();
        var slow = catalog.Build("pace-slow", 1);
        var normal = catalog.Build("pace-normal", 1);
        var fast = catalog.Build("pace-fast", 1);

        Assert.Equal(Pace.Slow, slow.Home.Pace);
        Assert.Equal(Pace.Normal, normal.Home.Pace);
        Assert.Equal(Pace.Fast, fast.Home.Pace);

        Assert.Equal(slow.Home.Team.Roster.Select(p => p.Ratings),
            fast.Home.Team.Roster.Select(p => p.Ratings));

        Assert.Equal(slow.Home.Offensive, fast.Home.Offensive);
    }

    private static string TempPath() =>
        Path.Combine(Directory.CreateTempSubdirectory("m6-config").FullName, "config.json");
}
