using System.Globalization;
using System.Text;

namespace DreamTeam.Simulator.Reporting;

/// <summary>
/// M6: the 08 §4 determinism manifest. Everything needed to re-run an experiment
/// and get the same numbers, and nothing that would prevent that.
///
/// <para><b>What is deliberately NOT recorded, and why.</b> 08 §4 says "JSON
/// canonicalization, kimlikler ve duvar saati alanları tanımlanmadan byte equality
/// bekleme" — do not expect byte equality from fields whose canonical form is
/// undefined. So this manifest records wall clock and host information as
/// <i>context</i> and never as an input, and the report says plainly that byte
/// equality is not claimed across runs.</para>
///
/// <para><b>Wall clock is recorded but not part of the seed.</b> Two runs of the
/// same corpus differ in duration; that must not be mistaken for a determinism
/// failure, and it must not be hidden either.</para>
/// </summary>
public sealed record ExperimentManifest
{
    public const int CurrentSchemaVersion = 1;

    public required int SchemaVersion { get; init; }

    public required string Command { get; init; }

    public required string CommandLine { get; init; }

    public required string FixtureName { get; init; }

    public required long MatchCount { get; init; }

    public required long SeedStart { get; init; }

    public required long LastSeedIndex { get; init; }

    public required int Jobs { get; init; }

    public required bool SummaryOnly { get; init; }

    /// <summary>Seeds reserved for validation, or null when no holdout was declared.</summary>
    public required long? HoldoutFrom { get; init; }

    public required string ConfigDocumentPath { get; init; }

    public required string ConfigDocumentVersion { get; init; }

    public required string ConfigHash { get; init; }

    public required string EngineVersion { get; init; }

    public required string RulesVersion { get; init; }

    public required string RngAlgorithm { get; init; }

    public required string RngVersion { get; init; }

    public required int EventSchemaVersion { get; init; }

    public required string Runtime { get; init; }

    public required string OperatingSystem { get; init; }

    public required int ProcessorCount { get; init; }

    public required double WallClockSeconds { get; init; }

    public required long PeakWorkingSetBytes { get; init; }

    public required double MatchesPerSecond { get; init; }

    /// <summary>
    /// Always false. 08 §4: byte equality is not claimed without a defined
    /// canonical form. Written down so nobody has to guess.
    /// </summary>
    public required bool ByteEqualityClaimed { get; init; }

    /// <summary>
    /// Whether the numbers in this run were produced with real data, or only
    /// against the engine's own internal consistency (D98c). Always
    /// <c>false</c> in M6: there is no season reference.
    /// </summary>
    public required bool ComparedAgainstRealSeasonData { get; init; }

    public string ToText()
    {
        var builder = new StringBuilder(2048);

        builder.Append("=== DETERMINISM MANIFESTI (08 S4) ===\n");
        builder.Append("Komut              : ").Append(Command).Append('\n');
        builder.Append("Komut satiri       : ").Append(CommandLine).Append('\n');
        builder.Append('\n');
        builder.Append("Fixture            : ").Append(FixtureName).Append('\n');
        builder.Append("Mac sayisi         : ").Append(MatchCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("Seed araligi       : [").Append(SeedStart.ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(LastSeedIndex.ToString(CultureInfo.InvariantCulture)).Append("]\n");
        builder.Append("Is parcacigi       : ").Append(Jobs.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("Summary-only       : ").Append(SummaryOnly ? "evet" : "hayir").Append('\n');
        builder.Append("Holdout baslangici : ")
            .Append(HoldoutFrom?.ToString(CultureInfo.InvariantCulture) ?? "(yok)").Append('\n');
        builder.Append('\n');
        builder.Append("Config belgesi     : ").Append(ConfigDocumentPath).Append('\n');
        builder.Append("Config surumu      : ").Append(ConfigDocumentVersion).Append('\n');
        builder.Append("ConfigHash         : ").Append(ConfigHash).Append('\n');
        builder.Append('\n');
        builder.Append("Motor surumu       : ").Append(EngineVersion).Append('\n');
        builder.Append("Kural surumu       : ").Append(RulesVersion).Append('\n');
        builder.Append("RNG                : ").Append(RngAlgorithm).Append(' ').Append(RngVersion).Append('\n');
        builder.Append("Event semasi       : ").Append(EventSchemaVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append('\n');
        builder.Append("Runtime            : ").Append(Runtime).Append('\n');
        builder.Append("Isletim sistemi    : ").Append(OperatingSystem).Append('\n');
        builder.Append("Islemci sayisi     : ").Append(ProcessorCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("Sure (sn)          : ").Append(WallClockSeconds.ToString("F2", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("Azami bellek (bayt): ").Append(PeakWorkingSetBytes.ToString("N0", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("Mac/sn             : ").Append(MatchesPerSecond.ToString("F1", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append('\n');
        builder.Append("Byte equality      : ").Append(ByteEqualityClaimed ? "VAR" : "VARILMAZ (08 S4)").Append('\n');
        builder.Append("Gercek sezon verisi: ").Append(ComparedAgainstRealSeasonData ? "evet" : "hayir (D98c)").Append('\n');

        return builder.ToString();
    }
}

public static class ExperimentManifestFactory
{
    public static ExperimentManifest Create(
        string commandLine,
        string fixtureName,
        Batch.BatchPlan plan,
        string configDocumentPath,
        string configDocumentVersion,
        string configHash,
        Batch.BatchOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(outcome);

        return new ExperimentManifest
        {
            SchemaVersion = ExperimentManifest.CurrentSchemaVersion,
            Command = "batch",
            CommandLine = commandLine,
            FixtureName = fixtureName,
            MatchCount = plan.MatchCount,
            SeedStart = plan.SeedStart,
            LastSeedIndex = plan.LastSeedIndex,
            Jobs = plan.Jobs,
            SummaryOnly = plan.SummaryOnly,
            HoldoutFrom = plan.HoldoutFrom,
            ConfigDocumentPath = configDocumentPath,
            ConfigDocumentVersion = configDocumentVersion,
            ConfigHash = configHash,
            EngineVersion = DreamTeam.MatchEngine.Core.EngineVersion.Current,
            RulesVersion = Fixture.FixtureCatalog.RulesVersion,
            RngAlgorithm = DreamTeam.MatchEngine.Randomness.RngIdentity.Algorithm,
            RngVersion = DreamTeam.MatchEngine.Randomness.RngIdentity.Version,
            EventSchemaVersion = DreamTeam.MatchEngine.Core.MatchSimulation.EventSchemaVersion,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ProcessorCount = System.Environment.ProcessorCount,
            WallClockSeconds = outcome.WallClock.TotalSeconds,
            PeakWorkingSetBytes = outcome.PeakWorkingSetBytes,
            MatchesPerSecond = outcome.MatchesPerSecond,
            ByteEqualityClaimed = false,
            ComparedAgainstRealSeasonData = false,
        };
    }
}
