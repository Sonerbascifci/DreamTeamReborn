using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator.Cli;
using DreamTeam.Simulator.Config;
using DreamTeam.Simulator.Export;
using DreamTeam.Simulator.Fixture;
using DreamTeam.Simulator.Reporting;

namespace DreamTeam.Simulator;

/// <summary>
/// M6: command execution. <see cref="Program"/> is a thin shell over this so that
/// the behaviour is testable without spawning a process.
/// </summary>
public sealed class SimulatorRunner
{
    public const string DefaultFixtureDirectory = "fixtures/teams";

    private readonly TextWriter _out;
    private readonly TextWriter _error;
    private readonly FixtureCatalog _fixtures = new();

    public SimulatorRunner(TextWriter output, TextWriter error)
    {
        _out = output;
        _error = error;
    }

    public int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (CommandLine.TryParse(args, out var request) is { } failure)
        {
            _error.WriteLine(failure.Message);
            _error.WriteLine();
            _error.WriteLine(CommandLine.HelpText());
            return CommandLine.ExitUsage;
        }

        if (request.Verb == CliVerb.Help)
        {
            _out.WriteLine(CommandLine.HelpText());
            return CommandLine.ExitSuccess;
        }

        if (request.Verb == CliVerb.ExportFixtures)
        {
            return ExportFixtures(request);
        }

        return Dispatch(request);
    }

    /// <summary>
    /// Writes every built-in fixture to disk. The files are the reviewable form of
    /// the same content the code holds; a test proves the two agree, so this command
    /// is what keeps them from drifting.
    /// </summary>
    private int ExportFixtures(CliRequest request)
    {
        var directory = request.OutputDirectory ?? DefaultFixtureDirectory;
        Directory.CreateDirectory(directory);

        foreach (var name in _fixtures.Names)
        {
            var path = Path.Combine(directory, $"{name}.json");
            JsonFixtureSource.Save(_fixtures.ToFile(name), path);
            _out.WriteLine($"  {name,-24} -> {path}");
        }

        _out.WriteLine($"{_fixtures.Names.Count} fixture yazildi: {Path.GetFullPath(directory)}");
        return CommandLine.ExitSuccess;
    }

    private int Dispatch(CliRequest request)
    {
        if (request.MatchCount < 0)
        {
            _error.WriteLine($"--matches negatif olamaz: {request.MatchCount}.");
            return CommandLine.ExitUsage;
        }

        if (request.Jobs < 1)
        {
            _error.WriteLine($"--jobs en az 1 olmalı: {request.Jobs}.");
            return CommandLine.ExitUsage;
        }

        if (!TryResolveSource(request.FixtureName, out var source, out var fixtureName))
        {
            return CommandLine.ExitUsage;
        }

        BalanceConfigDocument document;

        try
        {
            document = LoadConfig(request.ConfigPath);
        }
        catch (Exception error) when (error is InvalidOperationException or FileNotFoundException)
        {
            _error.WriteLine($"Config yuklenemedi: {error.Message}");
            return CommandLine.ExitUsage;
        }

        var config = document.ToEngineConfig();

        return request.Verb switch
        {
            CliVerb.Single or CliVerb.Default => RunSingle(request, config, source, fixtureName),
            CliVerb.Batch => RunBatch(request, config, document, source, fixtureName),
            _ => CommandLine.ExitUsage,
        };
    }

    /// <summary>
    /// A fixture name that names a file is loaded from disk; otherwise the built-in
    /// catalog answers. The message on failure lists what IS available, because a
    /// typo must never silently fall through to a default fixture.
    ///
    /// <para><b>Neden <c>request.FixtureName</c> degil?</b> Bir dosya yolu
    /// verildiginde kaynak, dosyanin IcINDEKI adi ile anahtarlanir
    /// ("neutral-mirror"), yolla degil. Kullanici yol verdiyse istegin FixtureName
    /// alani yoldur; onu kullanmak "dosyada su adi yok" hatasi verirdi.</para>
    /// </summary>
    private bool TryResolveSource(string fixture, out IFixtureSource source, out string resolvedName)
    {
        if (LooksLikePath(fixture))
        {
            try
            {
                var path = ResolvePath(fixture);
                var file = JsonFixtureSource.Load(path);
                JsonFixtureSource.Validate(file);
                source = new JsonFixtureSource([path]);
                resolvedName = file.Name;
                return true;
            }
            catch (Exception error) when (error is InvalidOperationException or FileNotFoundException)
            {
                _error.WriteLine($"Fixture dosyasi kullanilamadi: {error.Message}");
                source = null!;
                resolvedName = string.Empty;
                return false;
            }
        }

        if (_fixtures.Names.Contains(fixture, StringComparer.Ordinal))
        {
            source = _fixtures;
            resolvedName = fixture;
            return true;
        }

        _error.WriteLine($"Bilinmeyen fixture: '{fixture}'.");
        _error.WriteLine($"Bilenler: {string.Join(", ", _fixtures.Names)}.");
        source = null!;
        resolvedName = string.Empty;
        return false;
    }

    private static bool LooksLikePath(string fixture) =>
        fixture.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        || fixture.Contains('/', StringComparison.Ordinal)
        || fixture.Contains('\\', StringComparison.Ordinal);

    /// <summary>
    /// Resolves a repo-relative path against the current directory first, then
    /// against the repository root.
    ///
    /// <para><b>Neden gerekli?</b> Konsola <c>dotnet run</c> ile calistirildiginda
    /// CWD repo kokudur, ama testler <c>bin/Release/net10.0</c> altindan calisir.
    /// Ayrica bir test, CWD'yi degistirmeden tum yollari cozebilmelidir. Ilk
    /// adim goreli yollari oldugu gibi denemektir; ikinci adim yalnizca kok ara.</para>
    /// </summary>
    public static string ResolvePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Path.IsPathRooted(path) || File.Exists(path) || Directory.Exists(path))
        {
            return path;
        }

        var root = FindRepositoryRoot();

        return root is null ? path : Path.Combine(root, path);
    }

    /// <summary>Repo kokunu bulur; bulunamazsa null (sessizce devam edilir).</summary>
    public static string? FindRepositoryRoot()
    {
        var candidates = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };

        foreach (var start in candidates)
        {
            var directory = new DirectoryInfo(start);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "DreamTeam.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads the balance document, or falls back to the built-in baseline when the
    /// default path does not exist. The fallback is reported loudly, never silent:
    /// a run whose coefficients came from code instead of a file must say so.
    /// </summary>
    private BalanceConfigDocument LoadConfig(string requestedPath)
    {
        var path = ResolvePath(requestedPath);

        if (File.Exists(path))
        {
            return BalanceConfigStore.Load(path);
        }

        if (!string.Equals(requestedPath, CommandLine.DefaultConfigPath, StringComparison.Ordinal))
        {
            throw new FileNotFoundException(
                $"Belge verilen yolda yok: {path}.", path);
        }

        _out.WriteLine(
            $"UYARI: {path} bulunamadi; motorun gomulu baseline'i kullaniliyor. "
            + "Kalibrasyon icin belge yazilmalidir.");

        return BalanceConfigStore.BaselineDocument;
    }

    private int RunSingle(CliRequest request, EngineConfig config, IFixtureSource source, string fixtureName)
    {
        var setup = MakeFactory(request, source, fixtureName)(request.Seed);
        var simulation = new MatchSimulation(config);
        var result = simulation.Simulate(setup);

        SingleMatchReport.Write(_out, result, setup.Engine, simulation.ConfigHash, setup);

        if (request.WriteEvents && request.OutputDirectory is { } outputDirectory)
        {
            var path = Path.Combine(outputDirectory, "events.json");
            EventExporter.Write(path, result.MatchId, setup.Seed, simulation.ConfigHash, [.. result.Events]);
            _out.WriteLine($"Event akisi yazildi: {path}");
        }

        return result.Status == MatchStatus.Completed
            ? CommandLine.ExitSuccess
            : CommandLine.ExitFailed;
    }

    private int RunBatch(CliRequest request, EngineConfig config, BalanceConfigDocument document, IFixtureSource source, string fixtureName)
    {
        var plan = CommandLine.ToPlan(request);
        var driver = new BatchDriver(source, config);

        // AYNI fabrika tek komutta da toplu komutta da kullanilir. Once
        // surucu dogrudan source.Build cagiriyordu ve taktik bayraklari yalniz
        // `single` komutunda gecerliydi; `batch` bunlari sessizce yok sayiyordu.
        var factory = MakeFactory(request, source, fixtureName);
        var outcome = driver.Run(plan, factory);
        var report = BalanceReportBuilder.Build(fixtureName, outcome.Summary, outcome.ConfigHash);
        var manifest = ExperimentManifestFactory.Create(
            CommandLineText(request),
            fixtureName,
            plan,
            request.ConfigPath,
            document.Version,
            outcome.ConfigHash,
            outcome);

        if (request.OutputDirectory is { } directory)
        {
            SummaryWriter.WriteAll(directory, report, manifest);
            _out.WriteLine($"Rapor yazildi: {Path.GetFullPath(directory)}");
        }

        _out.WriteLine(report.ToText());
        _out.WriteLine();
        _out.WriteLine(manifest.ToText());

        // Cikis kodu: basarisiz mac varsa 1. Esik ihlali AYRI kod degil, cunku esik
        // bir kalite alarmidir ve maclarin hicbiri basarisiz olmadiginda 0 donmek
        // dogru olur; ayri kod CI'da yanlis alarm uretir.
        return outcome.Summary.AbortedCount > 0 ? CommandLine.ExitFailed : CommandLine.ExitSuccess;
    }

    /// <summary>
    /// Applies the fixture, then any tactic overrides. An override is applied to
    /// both sides unless the request names them separately (it does not yet), which
    /// keeps "which variable changed" unambiguous in the manifest.
    /// </summary>
    /// <summary>
    /// The one path from CLI arguments to a <see cref="MatchSetup"/>.
    ///
    /// <para><b>Neden tek fabrika?</b> Taktik ve tempo bayraklari once yalniz
    /// <c>single</c> komutunda uygulaniyordu; <c>batch</c> dogrudan kaynaktan
    /// setup kuruyordu. 1.500 maçlık üç farklı taktigin raporu bayt bayt aynı
    /// çıktı, yani savunma matrisi hiç ölçülemiyordu. Tek maçta taktik
    /// değişmiş göründüğü için hata ancak iki komut karşılaştırılınca görünüyordu.
    /// Şimdi ikisi de bu fabrikadan geçer.</para>
    ///
    /// <para>Bir komutta çalışan bir girdi diğerinde çalışmaz. Bir bayrak
    /// tanımadığımız ya da uygulamadığımız için hata vermek yerine sessizce
    /// varsayılana düşmesi, CLI'da kabul edilmeyen davranıştır.</para>
    /// </summary>
    private static BatchDriver.SetupFactory MakeFactory(
        CliRequest request,
        IFixtureSource source,
        string fixtureName)
    {
        var hasOverride = request.HomeOffense is not null
            || request.HomeDefense is not null
            || request.HomePace is not null
            || request.AwayOffense is not null
            || request.AwayDefense is not null
            || request.AwayPace is not null;

        if (!hasOverride)
        {
            return seed => source.Build(fixtureName, seed);
        }

        return seed =>
        {
            var setup = source.Build(fixtureName, seed);

            return FixtureCatalog.WithTactics(
                setup.Seed,
                request.HomeOffense ?? setup.Home.Offensive,
                request.HomeDefense ?? setup.Home.Defense,
                request.HomePace ?? setup.Home.Pace,
                request.AwayOffense ?? setup.Away.Offensive,
                request.AwayDefense ?? setup.Away.Defense,
                request.AwayPace ?? setup.Away.Pace);
        };
    }

    private static string CommandLineText(CliRequest request) =>
        $"batch --fixture {request.FixtureName} --matches {request.MatchCount} "
        + $"--seed-start {request.SeedStart} --jobs {request.Jobs} "
        + $"--config {request.ConfigPath}";
}
