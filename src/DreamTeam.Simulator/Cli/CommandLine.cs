using System.Globalization;
using DreamTeam.MatchEngine.Config;
using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator.Fixture;

namespace DreamTeam.Simulator.Cli;

/// <summary>M6: a parsed command line, or a refusal with a reason.</summary>
public sealed record CliRequest
{
    public required CliVerb Verb { get; init; }

    public required string FixtureName { get; init; }

    public required ulong Seed { get; init; }

    public required long MatchCount { get; init; }

    public required long SeedStart { get; init; }

    public required int Jobs { get; init; }

    public required bool SummaryOnly { get; init; }

    public required bool WriteEvents { get; init; }

    public required string? OutputDirectory { get; init; }

    public required string ConfigPath { get; init; }

    public required long? HoldoutFrom { get; init; }

    public OffensiveTactic? HomeOffense { get; init; }

    public DefensiveTactic? HomeDefense { get; init; }

    public Pace? HomePace { get; init; }

    public OffensiveTactic? AwayOffense { get; init; }

    public DefensiveTactic? AwayDefense { get; init; }

    public Pace? AwayPace { get; init; }
}

public enum CliVerb
{
    /// <summary>No arguments: the D33-compatible default. Same as <c>single neutral-mirror 20260927</c>.</summary>
    Default,

    Single,

    Batch,

    /// <summary>M6: writes every built-in fixture to disk as JSON.</summary>
    ExportFixtures,

    Help,
}

/// <summary>Why a command line was refused. Never thrown as a bare exception at the user.</summary>
public sealed record CliParseFailure(string Message)
{
    public override string ToString() => Message;
}

/// <summary>
/// M6: hand-written argument parser. <b>No package.</b> <c>System.CommandLine</c>
/// would break the "zero NuGet references" rule that has held since M1, and the
/// grammar here is thirteen flags.
///
/// <para><b>Two failure styles, deliberately.</b> A <b>malformed</b> argument
/// (unknown flag, non-numeric value, missing value) is a parse failure with a
/// message. A <b>semantically invalid</b> request (unknown fixture name, negative
/// match count) passes parsing and fails later at the point of use, where the
/// message can name the valid options. Silently defaulting a typo is the one
/// outcome this parser must never produce.</para>
/// </summary>
public static class CommandLine
{
    /// <summary>D33's fixed seed. The argument-less run must reproduce the M5 match.</summary>
    public const ulong DefaultSeed = 20260927;

    public const string DefaultFixture = "neutral-mirror";

    public const string DefaultConfigPath = "config/engine/baseline.v0.1.json";

    public const int ExitSuccess = 0;
    public const int ExitFailed = 1;
    public const int ExitUsage = 2;

    public static CliParseFailure? TryParse(string[] args, out CliRequest request)
    {
        ArgumentNullException.ThrowIfNull(args);
        request = null!;

        if (args.Length == 0)
        {
            request = Default();
            return null;
        }

        var verb = args[0].ToLowerInvariant();

        if (verb is "help" or "-h" or "--help")
        {
            request = new CliRequest
            {
                Verb = CliVerb.Help,
                FixtureName = DefaultFixture,
                Seed = DefaultSeed,
                MatchCount = 1,
                SeedStart = 0,
                Jobs = 1,
                SummaryOnly = true,
                WriteEvents = false,
                OutputDirectory = null,
                ConfigPath = DefaultConfigPath,
                HoldoutFrom = null,
            };

            return null;
        }

        var exportFixtures = verb == "export-fixtures";

        if (!exportFixtures && verb is not ("single" or "batch"))
        {
            return new CliParseFailure(
                $"Bilinmeyen komut: '{args[0]}'. Kullanilabilir: single, batch, export-fixtures, help.");
        }

        var fixture = DefaultFixture;
        var seed = DefaultSeed;
        var matches = 1L;
        var seedStart = 0L;
        var jobs = 1;
        var summaryOnly = true;
        var writeEvents = false;
        string? output = null;
        var configPath = DefaultConfigPath;
        long? holdoutFrom = null;
        OffensiveTactic? homeOffense = null;
        DefensiveTactic? homeDefense = null;
        Pace? homePace = null;
        OffensiveTactic? awayOffense = null;
        DefensiveTactic? awayDefense = null;
        Pace? awayPace = null;

        for (var index = 1; index < args.Length; index++)
        {
            var flag = args[index];
            string? inline = null;

            var equals = flag.IndexOf('=');

            if (equals > 0)
            {
                inline = flag[(equals + 1)..];
                flag = flag[..equals];
            }

            // DEGERSIZ bayraklar ONCE ele alinir. Aksi halde "--parallel"
            // deger bekler ve bir sonraki bayragi yutardı.
            switch (flag)
            {
                case "--parallel":
                    jobs = Math.Max(2, jobs);
                    continue;

                case "--summary-only":
                    summaryOnly = true;
                    continue;

                case "--events":
                    writeEvents = true;
                    summaryOnly = false;
                    continue;
            }

            if (TryValue(args, ref index, flag, inline, out var value) is { } failure)
            {
                return failure;
            }

            switch (flag)
            {
                case "--fixture":
                    fixture = value;
                    break;

                case "--seed":
                    if (!TryULong(value, out var parsedSeed))
                    {
                        return new CliParseFailure($"--seed sayı olmalı, verilen: '{value}'.");
                    }

                    seed = parsedSeed;
                    break;

                case "--matches":
                    if (!TryLong(value, out var parsedMatches))
                    {
                        return new CliParseFailure($"--matches tam sayı olmalı, verilen: '{value}'.");
                    }

                    matches = parsedMatches;
                    break;

                case "--seed-start":
                    if (!TryLong(value, out var parsedStart))
                    {
                        return new CliParseFailure($"--seed-start tam sayı olmalı, verilen: '{value}'.");
                    }

                    seedStart = parsedStart;
                    break;

                case "--jobs":
                    if (!TryInt(value, out var parsedJobs))
                    {
                        return new CliParseFailure($"--jobs tam sayı olmalı, verilen: '{value}'.");
                    }

                    jobs = parsedJobs;
                    break;

                case "--output":
                    output = value;
                    break;

                case "--config":
                    configPath = value;
                    break;

                case "--holdout-from":
                    if (!TryLong(value, out var parsedHoldout))
                    {
                        return new CliParseFailure($"--holdout-from tam sayı olmalı, verilen: '{value}'.");
                    }

                    holdoutFrom = parsedHoldout;
                    break;

                case "--tactics":
                    // IKISI DE AYRI DENENIR. C#'te `a || b` kisa devre yapar;
                    // ilk TryParse basarisiz olursa ikincisi HIC calismaz ve
                    // savunma adlari (Drop, Switch, ...) reddedilirdi.
                    var offenseOk = Enum.TryParse<OffensiveTactic>(value, ignoreCase: true, out var offense);
                    var defenseOk = Enum.TryParse<DefensiveTactic>(value, ignoreCase: true, out var defense);

                    if (!offenseOk && !defenseOk)
                    {
                        return new CliParseFailure(
                            $"--tactics 'Hücum/Savunma' olmalı. Hücum: {OffenseOptions()}. Savunma: {DefenseOptions()}.");
                    }

                    homeOffense = offenseOk ? offense : null;
                    homeDefense = defenseOk ? defense : null;
                    break;

                case "--pace":
                    if (!Enum.TryParse<Pace>(value, ignoreCase: true, out var pace))
                    {
                        return new CliParseFailure($"--pace geçersiz: '{value}'. Seçenek: {PaceOptions()}.");
                    }

                    homePace = pace;
                    break;

                case "--away-tactics":
                    // Savunma matrisi (08 §5) 4 hücum x 4 savunma = 16 çift
                    // ister. Tek taraflı --tactics ile bu kurulamaz; iki taraf
                    // aynı oynarsa "hangi eşleşme kazanıyor" sorusu sorulamaz.
                    if (!Enum.TryParse<OffensiveTactic>(value, ignoreCase: true, out var awayOffenseOnly))
                    {
                        if (!Enum.TryParse<DefensiveTactic>(value, ignoreCase: true, out var awayDefenseOnly))
                        {
                            return new CliParseFailure(
                                $"--away-tactics 'Hücum/Savunma' olmalı. Hücum: {OffenseOptions()}. Savunma: {DefenseOptions()}.");
                        }

                        awayDefense = awayDefenseOnly;
                    }
                    else
                    {
                        awayOffense = awayOffenseOnly;
                    }

                    break;

                case "--away-pace":
                    if (!Enum.TryParse<Pace>(value, ignoreCase: true, out var awayPaceOnly))
                    {
                        return new CliParseFailure($"--away-pace geçersiz: '{value}'. Seçenek: {PaceOptions()}.");
                    }

                    awayPace = awayPaceOnly;
                    break;

                default:
                    return new CliParseFailure($"Bilinmeyen bayrak: '{flag}'. --help'a bakın.");
            }
        }

        // --tactics/--pace TEK tafafi tanimlar ve karşı taraf ayni degere
        // esitlenir. --away-tactics/--away-pace VERILDIYSE o deger korunur;
        // boylece savunma matrisi 4x4 kurulabilir.
        awayOffense ??= homeOffense;
        awayDefense ??= homeDefense;
        awayPace ??= homePace;

        request = new CliRequest
        {
            Verb = exportFixtures ? CliVerb.ExportFixtures : verb == "single" ? CliVerb.Single : CliVerb.Batch,
            FixtureName = fixture,
            Seed = seed,
            MatchCount = matches,
            SeedStart = seedStart,
            Jobs = jobs,
            SummaryOnly = summaryOnly,
            WriteEvents = writeEvents,
            OutputDirectory = output,
            ConfigPath = configPath,
            HoldoutFrom = holdoutFrom,
            HomeOffense = homeOffense,
            HomeDefense = homeDefense,
            HomePace = homePace,
            AwayOffense = awayOffense,
            AwayDefense = awayDefense,
            AwayPace = awayPace,
        };

        return null;
    }

    public static CliRequest Default() => new()
    {
        Verb = CliVerb.Single,
        FixtureName = DefaultFixture,
        Seed = DefaultSeed,
        MatchCount = 1,
        SeedStart = 0,
        Jobs = 1,
        SummaryOnly = true,
        WriteEvents = false,
        OutputDirectory = null,
        ConfigPath = DefaultConfigPath,
        HoldoutFrom = null,
    };

    public static BatchPlan ToPlan(CliRequest request) => new()
    {
        FixtureName = request.FixtureName,
        MatchCount = request.MatchCount,
        SeedStart = request.SeedStart,
        Jobs = request.Jobs,
        SummaryOnly = request.SummaryOnly,
        HoldoutFrom = request.HoldoutFrom,
    };

    private static CliParseFailure? TryValue(
        string[] args,
        ref int index,
        string flag,
        string? inline,
        out string value)
    {
        if (inline is not null)
        {
            value = inline;
            return null;
        }

        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = args[++index];
            return null;
        }

        value = string.Empty;
        return new CliParseFailure($"{flag} bir değer gerektirir.");
    }

    private static bool TryLong(string value, out long result) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryULong(string value, out ulong result) =>
        ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    public static string OffenseOptions() => string.Join("/", Enum.GetNames<OffensiveTactic>());

    public static string DefenseOptions() => string.Join("/", Enum.GetNames<DefensiveTactic>());

    public static string PaceOptions() => string.Join("/", Enum.GetNames<Pace>());

    public static string HelpText() =>
        $"""
        DreamTeam Simulator (M6)

        Kullanım:
          dotnet run --project src/DreamTeam.Simulator -- [single|batch] [bayraklar]

        Komut:
          (yok)             Argümansız: single --fixture {DefaultFixture} --seed {DefaultSeed} ile aynı.
          single            Tek maç. Varsayılan olarak özet yazılır; --events tam akışı yazar.
          batch             Toplu koşu. Bellek sınırlı özet kipi (MatchResult üretilmez).
          export-fixtures   Gömülü fixture'ları JSON olarak fixtures/teams/ altına yazar.
          help              Bu metin.

        Bayraklar:
          --fixture <ad|yol> Fixture adi VEYA .json dosya yolu. Adlar: neutral-mirror,
                               neutral-mirror-swapped, quality-gap, roster-fit,
                               pace-slow, pace-normal, pace-fast
          --seed <n>          Tek maç tohumu (single).
          --matches <n>       Maç sayısı (batch).
          --seed-start <n>    İLK MAÇ İNDEKSİ. Tohum DEĞİLDİR: maç i tohumu
                               mix(seedStart + i). Farklı seed-start = farklı korpüs.
          --jobs <n>          İş parçacığı. Varsayılan 1 (sıralı = referans).
          --parallel          --jobs 2'ye ayarlar.
          --summary-only      Yalnız özet (varsayılan).
          --events            Tam event akışını JSON'a yazar (yalnız single).
          --output <yol>      Çıktı dizini. Verilmezse ekrana yazılır.
          --config <yol>      Balance belgesi. Varsayılan: {DefaultConfigPath}
          --holdout-from <n>  Ayar/holdout seed sınırı; manifest'e yazılır.
          --tactics <O/D>     Ev tarafinin hucum/savunma taktigi. O: {OffenseOptions()} | D: {DefenseOptions()}
          --pace <p>          Ev tarafinin temposu: {PaceOptions()}
          --away-tactics <O/D> Depasman taktigi. Verilmezse ev ile ayni.
          --away-pace <p>     Depasman temposu. Verilmezse ev ile ayni.

        Örnek:
          ... -- single --fixture neutral-mirror --seed {DefaultSeed}
          ... -- batch --fixture neutral-mirror --matches 10000 --seed-start 1 --summary-only
          ... -- batch --fixture quality-gap --matches 100000 --seed-start 1 --jobs 4
        """;
}
