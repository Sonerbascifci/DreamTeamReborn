// M7, D115: BU DOSYA SIMULATOR'DAN TASINDI. Simulator kalan tek okuyucudur ve
// sunucu da ayni belgeyi ayni kodla okumalidir (D103: "denge kaynagi JSON
// belgedir"). Iki ayri JSON okuyucu, "bu rapor hangi config ile uretildi?"
// sorusunu tekrar ortaya cikarirdi. Bagimlilik grafigi 03'e uygun kalsin diye
// Infrastructure -> Simulator eklenmedi; okuyucu motorun Config katmaninda
// duruyor. System.Text.Json BCL'dedir, bu yuzden proje yine sifir paket kalir
// (M5 D88).
using System.Text.Json;
using System.Text.Json.Serialization;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.MatchEngine.Config;

/// <summary>
/// M6, moved in M7 (D115): the versioned, on-disk balance document. This is the file calibration
/// edits; C# code is not the tuning surface.
///
/// <para><b>What is here and what is not.</b> Every numeric model of
/// <see cref="EngineConfig"/> is here. <c>ActionProfiles</c> is NOT, because each
/// profile carries a <c>Func&lt;PlayerRatings,int&gt;</c> skill reader that cannot
/// be serialized (D97's reason, in a different place). That is a real limit and
/// it is stated here rather than discovered later: the document supplies the
/// coefficients, the code supplies the skill channels.</para>
///
/// <para><b>Why a document at all.</b> 08 §119 requires the config hash before and
/// after every calibration step. If the tuning lived in C#, "before and after"
/// would mean a commit. With a document it is a file, and the diff is the
/// calibration record.</para>
/// </summary>
public sealed record BalanceConfigDocument
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Document schema, not engine schema. Bumped when a field changes shape.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Free-form version label, e.g. "v0.1". Recorded in the manifest.</summary>
    public required string Version { get; init; }

    /// <summary>
    /// Engine version this document was written for. Loading against a different
    /// engine is an error, not a warning: a coefficient set tuned for another
    /// build is not comparable.
    /// </summary>
    public required string EngineVersion { get; init; }

    public required string RulesVersion { get; init; }

    /// <summary>
    /// One-line note on what this version changed. Mandatory so that a report
    /// reader can tell two documents apart without diffing them.
    /// </summary>
    public required string Notes { get; init; }

    public required RulesProfile Rules { get; init; }

    public required ShotModel Shot { get; init; }

    public required ActionModel Actions { get; init; }

    public required FoulModel Fouls { get; init; }

    public required FreeThrowModel FreeThrows { get; init; }

    public required TacticsModel Tactics { get; init; }

    public required DefenseModel Defense { get; init; }

    public required FatigueModel Fatigue { get; init; }

    public required PaceModel Pace { get; init; }

    public required double SelectionSpread { get; init; }

    public required int MaxActionsPerMatch { get; init; }

    /// <summary>Builds the engine config this document describes.</summary>
    public EngineConfig ToEngineConfig() => new()
    {
        Rules = Rules,
        Shot = Shot,
        Actions = Actions,
        Fouls = Fouls,
        FreeThrows = FreeThrows,
        Tactics = Tactics,
        Defense = Defense,
        Fatigue = Fatigue,
        Pace = Pace,
        SelectionSpread = SelectionSpread,
        MaxActionsPerMatch = MaxActionsPerMatch,

        // Not in the document, by design: Func<> skill channels (see the type doc).
        ActionProfiles = ActionProfile.Baseline,
    };
}

/// <summary>
/// Reads and writes <see cref="BalanceConfigDocument"/>. Zero packages:
/// <c>System.Text.Json</c> is measured to work in a class library with no NuGet
/// reference at all (M5 D88).
/// </summary>
public static class BalanceConfigStore
{
    /// <summary>
    /// Enum-as-name. M5 D88 measured the default: enums serialize NUMERICALLY, so
    /// reordering an enum would silently change the meaning of a saved file.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    static BalanceConfigStore()
    {
        // Enum-as-name. M5 D88 measured the default: enums serialize NUMERICALLY, so
        // reordering an enum would silently change the meaning of a saved file.
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    public static BalanceConfigDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Balance belgesi bulunamadi: {path}. "
                + "Simulator degeri config/engine/baseline.v0.1.json bekler.", path);
        }

        var json = File.ReadAllText(path);
        var document = Deserialize(json, path);

        Require(document.SchemaVersion == BalanceConfigDocument.CurrentSchemaVersion,
            $"{path}: belge semasi {document.SchemaVersion}, beklenen "
            + $"{BalanceConfigDocument.CurrentSchemaVersion}.");

        Require(document.EngineVersion == EngineVersion.Current,
            $"{path}: belge motor surumu '{document.EngineVersion}' icin yazildi, "
            + $"calisan motor '{EngineVersion.Current}'. Karsilastirilamaz.");

        return document;
    }

    public static void Save(BalanceConfigDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, Serialize(document));
    }

    public static string Serialize(BalanceConfigDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static BalanceConfigDocument Deserialize(string json, string originForErrors)
    {
        try
        {
            return JsonSerializer.Deserialize<BalanceConfigDocument>(json, Options)
                ?? throw new InvalidOperationException($"{originForErrors}: belge bos.");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException(
                $"{originForErrors}: balance belgesi okunamadi ({error.Message}).", error);
        }
    }

    /// <summary>
    /// The built-in document: the engine's own baseline values, as a document.
    ///
    /// <para><b>Why this exists.</b> It lets the shipped default and the on-disk
    /// file be compared directly. If the file and this record ever disagree, the
    /// file is the one the runs used, and the report says so.</para>
    /// </summary>
    public static BalanceConfigDocument BaselineDocument { get; } = new()
    {
        SchemaVersion = BalanceConfigDocument.CurrentSchemaVersion,
        Version = "v0.1",
        EngineVersion = EngineVersion.Current,
        RulesVersion = RulesIdentity.Current,
        Notes = "M5 baseline'i. M6 kalibrasyonu bu dosyayi degistirir.",
        Rules = RulesProfile.SimpleNbaInspired,
        Shot = ShotModel.Baseline,
        Actions = ActionModel.Baseline,
        Fouls = FoulModel.Baseline,
        FreeThrows = FreeThrowModel.Baseline,
        Tactics = TacticsModel.Baseline,
        Defense = DefenseModel.Baseline,
        Fatigue = FatigueModel.Baseline,
        Pace = PaceModel.Baseline,
        SelectionSpread = EngineConfig.Baseline.SelectionSpread,
        MaxActionsPerMatch = EngineConfig.Baseline.MaxActionsPerMatch,
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
