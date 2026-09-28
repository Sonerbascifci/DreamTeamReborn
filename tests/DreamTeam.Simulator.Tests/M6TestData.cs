using System.Text;
using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator.Cli;
using DreamTeam.Simulator.Config;
using DreamTeam.Simulator.Fixture;
using DreamTeam.Simulator.Reporting;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Simulator.Tests;

/// <summary>
/// M6 test yardimcilari. Buradaki her sey GERCEK nesnedir: sahte motor, sahte
/// rastgelelik veya sabitlenmis cikti yoktur. Bir testin gectigi, uretilen
/// kodunun gectigi anlamina gelir.
/// </summary>
internal static class M6TestData
{
    /// <summary>Bu dosya dizinine gore repo kokunun 4 seviye ustunde.</summary>
    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DreamTeam.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Repo koku bulunamadi (DreamTeam.slnx).");
        }
    }

    public static string BalanceConfigPath => Path.Combine(RepositoryRoot, CommandLine.DefaultConfigPath);

    public static string FixtureDirectory => Path.Combine(RepositoryRoot, SimulatorRunner.DefaultFixtureDirectory);

    /// <summary>
    /// The config the runs actually use: the checked-in balance document.
    ///
    /// <para><b>Neden fabrika varsayilani degil?</b> M6 kalibrasyonu belgeyi
    /// degistirdi; motorun gomulu <c>EngineConfig.Baseline</c> degismedi ve
    /// degismemesi GEREKIYOR, cunku M2-M5'in golden testleri ona bagli. Bu
    /// yuzden iki ayri deger vardir ve hangisinin nerede kullanildigi acikca
    /// yazilmistir: simulator BELGEYI okur, motor birim testleri FABRIKA
    /// varsayilanini okur.</para>
    ///
    /// <para>Testler de belgeyi okumalidir; aksi halde testler kalibre
    /// edilmemis bir motoru olcer ve raporlar baska bir motoru olcer.</para>
    /// </summary>
    public static EngineConfig Config() => BalanceConfigStore.Load(BalanceConfigPath).ToEngineConfig();

    /// <summary>The factory default the engine's own unit tests keep using.</summary>
    public static EngineConfig FactoryConfig() => BalanceConfigStore.BaselineDocument.ToEngineConfig();

    public static FixtureCatalog Catalog() => new();

    public static BatchPlan Plan(
        string fixture = "neutral-mirror",
        long matches = 20,
        long seedStart = 0,
        int jobs = 1,
        long? holdoutFrom = null) => new()
        {
            FixtureName = fixture,
            MatchCount = matches,
            SeedStart = seedStart,
            Jobs = jobs,
            SummaryOnly = true,
            HoldoutFrom = holdoutFrom,
        };

    /// <summary>
    /// Bir toplu kosunun ozetini karsilastirilabilir metne cevirir. Testler
    /// "ozet degisti mi" sorusunu okunabilir bir farkla cevaplamak icin bunu
    /// kullanir; sadece "esit degil" demek, farkin ne oldugunu gizler.
    /// </summary>
    public static string Describe(SummaryAccumulator summary)
    {
        var builder = new StringBuilder(1024);

        builder.Append("mac=").Append(summary.MatchCount)
            .Append(" tamam=").Append(summary.CompletedCount)
            .Append(" abort=").Append(summary.AbortedCount)
            .Append(" ev=").Append(summary.HomeWins)
            .Append(" dep=").Append(summary.AwayWins)
            .Append(" beraberlik=").Append(summary.TieCount)
            .Append(" uzatma=").Append(summary.OvertimeMatchCount)
            .Append(" puan=").Append(summary.Home.Points).Append('/').Append(summary.Away.Points)
            .Append(" poss=").Append(summary.Home.Possessions).Append('/').Append(summary.Away.Possessions)
            .Append(" fga=").Append(summary.Home.FieldGoalsAttempted).Append('/').Append(summary.Away.FieldGoalsAttempted)
            .Append(" fgm=").Append(summary.Home.FieldGoalsMade).Append('/').Append(summary.Away.FieldGoalsMade)
            .Append(" ft=").Append(summary.Home.FreeThrowAttempts).Append('/').Append(summary.Away.FreeThrowAttempts)
            .Append(" tov=").Append(summary.Home.Turnovers).Append('/').Append(summary.Away.Turnovers)
            .Append(" pf=").Append(summary.Home.PersonalFouls).Append('/').Append(summary.Away.PersonalFouls)
            .Append(" oreb=").Append(summary.Home.OffensiveRebounds).Append('/').Append(summary.Away.OffensiveRebounds)
            .Append(" aksiyon=").Append(summary.TotalActions)
            .Append(" event=").Append(summary.TotalEvents)
            .Append(" sure=").Append(summary.TotalElapsedGameTimeMs)
            .Append('\n');

        builder.Append(summary.Diagnostics.ToReport());

        return builder.ToString();
    }

    /// <summary>Tek bir maçın özetini okunabilir metne çevirir.</summary>
    public static string Describe(MatchSummary summary) =>
        $"{summary.Seed}: {summary.HomeScore}-{summary.AwayScore} "
        + $"P{summary.PeriodsPlayed} poss{summary.HomePossessions}/{summary.AwayPossessions} "
        + $"ev{summary.Home.Points}/{summary.Home.FieldGoalsMade}-{summary.Home.FieldGoalsAttempted} "
        + $"dep{summary.Away.Points}/{summary.Away.FieldGoalsMade}-{summary.Away.FieldGoalsAttempted} "
        + $"ev{summary.Status} act{summary.TotalActions} ev{summary.EventCount}";
}
