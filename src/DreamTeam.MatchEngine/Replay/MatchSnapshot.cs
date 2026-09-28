using System.Collections.Immutable;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Randomness;

namespace DreamTeam.MatchEngine.Replay;

/// <summary>
/// <b>Serializabilir durum.</b> <see cref="MatchState"/>'in tamami serilestirilemez:
/// <c>Config.ActionProfiles[].Skill</c> bir <c>Func&lt;PlayerRatings,int&gt;</c>'dir
/// ve <c>Random</c> bir arayuzdur. Bu kayit, serilestirilebilir parcalari
/// <b>acikca listeler</b> — hangi alanin neden disarida kaldigi gizli degildir.
///
/// <para><b><see cref="MatchState.Config"/> NEDEN YOK?</b> Config bir
/// <i>durum</i> degil, bir <i>parametredir</i>. Snapshot onu tasimaz; bunun
/// yerine <see cref="MatchSnapshot.ConfigHash"/> tasir ve restore ederken
/// <c>RequireCompatible</c> ile dogrulanir. Ayni sekilde
/// <c>MatchState.Random</c> de tasinmaz; onun yerine
/// <see cref="MatchSnapshot.RandomState"/> vardir.</para>
///
/// <para><b>Yeni alan eklersek buraya da eklenmelidir</b>; aksi halde sessizce
/// kaybolur. <c>SnapshotCoversEverySerializableStateField</c> testi bunu
/// zorlar.</para>
/// </summary>
public sealed record MatchSnapshotData
{
    public required MatchSetup Setup { get; init; }

    public required MatchClock Clock { get; init; }

    public required MatchPhase Phase { get; init; }

    public required TeamMatchState Home { get; init; }

    public required TeamMatchState Away { get; init; }

    public required int HomeScore { get; init; }

    public required int AwayScore { get; init; }

    public required PossessionState? Possession { get; init; }

    public required PendingShot? PendingShot { get; init; }

    public required PendingFreeThrowSeries? PendingFrees { get; init; }

    public required CommandQueue CommandQueue { get; init; }

    public required long NextSequence { get; init; }

    public required long NextActionId { get; init; }

    public required long NextShotId { get; init; }

    public required long NextTurnoverId { get; init; }

    public required long NextFoulId { get; init; }

    public required long NextFTSeriesId { get; init; }

    public required int TotalActionCount { get; init; }

    public required int PossessionCount { get; init; }

    /// <summary>
    /// M6 (D100): diagnostics sayaclari serilestirilir. Snapshot diagnostics'i
    /// tasimazsa ortada alinan bir snapshot sayaclari sifirlar ve M6 raporu
    /// eksik kalir. They do not change any outcome, so carrying them costs
    /// nothing and losing them would be a silent gap.
    /// </summary>
    public required Diagnostics.DiagnosticCounters Diagnostics { get; init; }

    public static MatchSnapshotData From(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new MatchSnapshotData
        {
            Setup = state.Setup,
            Clock = state.Clock,
            Phase = state.Phase,
            Home = state.Home,
            Away = state.Away,
            HomeScore = state.HomeScore,
            AwayScore = state.AwayScore,
            Possession = state.Possession,
            PendingShot = state.PendingShot,
            PendingFrees = state.PendingFrees,
            CommandQueue = state.CommandQueue,
            NextSequence = state.NextSequence,
            NextActionId = state.NextActionId,
            NextShotId = state.NextShotId,
            NextTurnoverId = state.NextTurnoverId,
            NextFoulId = state.NextFoulId,
            NextFTSeriesId = state.NextFTSeriesId,
            TotalActionCount = state.TotalActionCount,
            PossessionCount = state.PossessionCount,
            Diagnostics = state.Diagnostics,
        };
    }

    /// <summary>
    /// Durumu geri kurar. <paramref name="config"/> ve <paramref name="random"/>
    /// DIŞARIDAN verilir: config bir parametre, RNG durumu ise
    /// <see cref="MatchSnapshot.RandomState"/>'dir.
    /// </summary>
    public MatchState ToState(EngineConfig config, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(random);

        return new MatchState
        {
            Config = config,
            Setup = Setup,
            Clock = Clock,
            Phase = Phase,
            Home = Home,
            Away = Away,
            HomeScore = HomeScore,
            AwayScore = AwayScore,
            Possession = Possession,
            PendingShot = PendingShot,
            PendingFrees = PendingFrees,
            CommandQueue = CommandQueue,
            NextSequence = NextSequence,
            NextActionId = NextActionId,
            NextShotId = NextShotId,
            NextTurnoverId = NextTurnoverId,
            NextFoulId = NextFoulId,
            NextFTSeriesId = NextFTSeriesId,
            TotalActionCount = TotalActionCount,
            PossessionCount = PossessionCount,
            Random = random,
            Diagnostics = Diagnostics,
        };
    }
}

/// <summary>
/// Simulation replay girdisi (07 §7).
///
/// <para><b>Event replay DEGILDIR.</b> Event replay saklanmis event'leri izler ve
/// RNG gerektirmez. Bu snapshot ise motoru <b>yeniden calistirilabilir</b> kilar:
/// ayni setup + ayni config + ayni RNG state'i + ayni komut kaydi = ayni mac
/// (03 §"Sürüm ve tekrar üretilebilirlik").</para>
///
/// <para><b>Neden her alan gerekli?</b> Birkac alan "zaten turetilir" gibi
/// gorunur; degildir:</para>
/// <list type="bullet">
///   <item><description><b>RNG state</b>: seed DEGIL, mevcut durumdur (05 §14).
///   Tohumdan yeniden uretilemez, cunku o ana kadar cekilis sayisi bilinmez.</description></item>
///   <item><description><b>Komut kuyrugu</b>: uygulanmamis komutlar kuyruktadir.
///   Kaybolursa restore edilen mac ayni degildir (D86).</description></item>
///   <item><description><b>Surum alanlari</b>: 07 §7 "Engine upgrade sonrasi eski
///   simulasyonun yeniden hesaplanmasi surum uyumlulugu gerektirir." Uyumsuz
///   surum <b>acik hata</b> verir, sessizce yanlis sonuc uretmez.</description></item>
/// </list>
///
/// <para><b>Serilestirme siralamasi kanoniktir</b> (D39 usulu): JSON alan sirasi
/// kaynak sirasiyla aynidir, boylece ayni durum ayni bayti uretir.</para>
/// </summary>
public sealed record MatchSnapshot
{
    public required int SnapshotSchemaVersion { get; init; }

    public required MatchSnapshotData Data { get; init; }

    /// <summary>RNG'nin mevcut durumu, <c>SeededRandom.StateSizeInBytes</c> bayt.</summary>
    public required ImmutableArray<byte> RandomState { get; init; }

    public required string EngineVersion { get; init; }

    public required string RulesVersion { get; init; }

    public required string ConfigHash { get; init; }

    public required int EventSchemaVersion { get; init; }

    /// <summary>Bu snapshot'in alindigi andaki <c>NextSequence</c>.</summary>
    public required long StateSequence { get; init; }

    public const int CurrentSchemaVersion = 1;

    /// <summary>Durumdan anlik goruntu alir.</summary>
    public static MatchSnapshot Capture(MatchState state, string configHash)
    {
        ArgumentNullException.ThrowIfNull(state);

        Span<byte> rngState = stackalloc byte[SeededRandom.StateSizeInBytes];
        state.Random.GetState(rngState);

        return new MatchSnapshot
        {
            SnapshotSchemaVersion = CurrentSchemaVersion,
            Data = MatchSnapshotData.From(state),
            RandomState = [.. rngState.ToArray()],
            EngineVersion = state.Setup.Engine.EngineVersion,
            RulesVersion = state.Setup.Engine.RulesVersion,
            ConfigHash = configHash,
            EventSchemaVersion = MatchSimulation.EventSchemaVersion,
            StateSequence = state.NextSequence,
        };
    }

    /// <summary>
    /// Snapshot'tan durumu geri yukler. <b>RNG state'i uygulanir</b> — bu olmadan
    /// restore edilen mac farkli olur. <paramref name="config"/> cagirandan gelir;
    /// once <see cref="RequireCompatible"/> ile dogrulanmalidir.
    /// </summary>
    public MatchState Restore(EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (RandomState.Length != SeededRandom.StateSizeInBytes)
        {
            throw new InvalidOperationException(
                $"RNG state tam olarak {SeededRandom.StateSizeInBytes} bayt olmali; "
                + $"alinan {RandomState.Length}.");
        }

        var random = new SeededRandom(0);
        random.SetState(RandomState.AsSpan());

        return Data.ToState(config, random);
    }

    /// <summary>
    /// Sürüm uyumluluğu denetimi (07 §7). Uyumsuzluk <b>sessizce yoksayılmaz</b>:
    /// eski motor binary'siyle yeniden hesaplama iddiası doğru olmaz.
    /// </summary>
    public void RequireCompatible(string expectedEngineVersion, string expectedConfigHash)
    {
        ArgumentNullException.ThrowIfNull(expectedEngineVersion);
        ArgumentNullException.ThrowIfNull(expectedConfigHash);

        if (!string.Equals(EngineVersion, expectedEngineVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Snapshot engine surumu {EngineVersion}, beklenen {expectedEngineVersion}. "
                + "Yeniden hesaplama yapilamaz; event replay kullanilmalidir (07 §7).");
        }

        if (!string.Equals(ConfigHash, expectedConfigHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Snapshot config hash'i {ConfigHash}, beklenen {expectedConfigHash}. "
                + "Farkli denge/ayar ile ayni mac uretilemez.");
        }
    }
}
