using DreamTeam.MatchEngine.Config;
using DreamTeam.Simulator.Cli;
using DreamTeam.Simulator.Fixture;

namespace DreamTeam.Simulator.Tests;

/// <summary>
/// M6: CLI sozlesmesi.
///
/// <para><b>En onemli test:</b> <c>TheArgumentlessInvocationMatchesTheSingleCommand</c>.
/// D33 M2'de "bu giris noktasi argümansizdir" diyordu. M6 o kisiti kaldirdi;
/// argümansiz cagirmanin AYNI maçı oynamasi geri regresyon olarak korunur. Bu
/// regresyon M6 sirasinda gerçekten kirildi (fixture rating şekli düzlestiginde
/// 112-118/5 periyot -> 102-110/4 periyot) ve rapor farkiyla bulundu.</para>
///
/// <para>ASCII yorum kullanir (depo kurali).</para>
/// </summary>
public class CliTests
{
    [Fact]
    public void NoArgumentsMeansTheD33DefaultMatch()
    {
        Assert.Null(CommandLine.TryParse([], out var request));
        Assert.Equal(CliVerb.Single, request.Verb);
        Assert.Equal("neutral-mirror", request.FixtureName);
        Assert.Equal(20260927UL, request.Seed);
        Assert.Equal(1, request.Jobs);
    }

    [Fact]
    public void TheContractFromTheRoadmapParses()
    {
        // 09'in verdigi sozlesme, kelimesi kelimesine.
        Assert.Null(CommandLine.TryParse(
            ["single", "--fixture", "neutral-mirror", "--seed", "12345", "--output", "reports/single"],
            out var single));

        Assert.Equal(CliVerb.Single, single.Verb);
        Assert.Equal(12345UL, single.Seed);
        Assert.Equal("reports/single", single.OutputDirectory);

        Assert.Null(CommandLine.TryParse(
            [
                "batch", "--fixture", "neutral-mirror", "--matches", "10000",
                "--seed-start", "1", "--summary-only", "--output", "reports/balance/neutral-10k",
            ],
            out var batch));

        Assert.Equal(CliVerb.Batch, batch.Verb);
        Assert.Equal(10_000, batch.MatchCount);
        Assert.Equal(1, batch.SeedStart);
        Assert.True(batch.SummaryOnly);
        Assert.Equal("reports/balance/neutral-10k", batch.OutputDirectory);
    }

    [Fact]
    public void TheHundredThousandCommandParses()
    {
        Assert.Null(CommandLine.TryParse(
            [
                "batch", "--fixture", "neutral-mirror", "--matches", "100000",
                "--seed-start", "1", "--summary-only", "--output", "reports/balance/neutral-100k",
            ],
            out var request));

        Assert.Equal(100_000, request.MatchCount);
    }

    [Fact]
    public void AnUnknownVerbIsRefusedWithTheValidOnes()
    {
        var failure = CommandLine.TryParse(["run"], out _);

        Assert.NotNull(failure);
        Assert.Contains("single", failure.Message, StringComparison.Ordinal);
        Assert.Contains("batch", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownFlagIsRefusedRatherThanIgnored()
    {
        // Bir yazim hatasi sessizce varsayilana dustmemeli.
        var failure = CommandLine.TryParse(["batch", "--matchess", "10"], out _);

        Assert.NotNull(failure);
        Assert.Contains("--matchess", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFlagWithoutAValueIsRefused()
    {
        var failure = CommandLine.TryParse(["batch", "--matches"], out _);

        Assert.NotNull(failure);
        Assert.Contains("--matches", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANonNumericValueIsRefused()
    {
        Assert.NotNull(CommandLine.TryParse(["batch", "--matches", "cok"], out _));
        Assert.NotNull(CommandLine.TryParse(["batch", "--seed", "-1"], out _));
        Assert.NotNull(CommandLine.TryParse(["batch", "--jobs", "0.5"], out _));
    }

    [Fact]
    public void ANegativeSeedIsRefusedBecauseSeedIsUnsigned()
    {
        var failure = CommandLine.TryParse(["single", "--seed", "-5"], out _);

        Assert.NotNull(failure);
        Assert.Contains("--seed", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineValuesAreAccepted()
    {
        Assert.Null(CommandLine.TryParse(["batch", "--matches=25", "--jobs=4"], out var request));

        Assert.Equal(25, request.MatchCount);
        Assert.Equal(4, request.Jobs);
    }

    [Fact]
    public void ParallelImpliesAtLeastTwoJobs()
    {
        Assert.Null(CommandLine.TryParse(["batch", "--parallel"], out var request));

        Assert.True(request.Jobs >= 2);
    }

    [Fact]
    public void EventsImpliesNotSummaryOnly()
    {
        Assert.Null(CommandLine.TryParse(["single", "--events", "--output", "x"], out var request));

        Assert.True(request.WriteEvents);
        Assert.False(request.SummaryOnly);
    }

    [Fact]
    public void TacticOverrideAcceptsAnOffenseOrADefenseName()
    {
        Assert.Null(CommandLine.TryParse(
            ["single", "--tactics", "InsidePost"], out var offense));

        // Yalniz hucrem adi verildi: savunma alani BOS KALIR. Iki TryParse
        // birbirine baglanmamistir, yoksa "Drop" gibi savunma adlari hic
        // taninmazdi (C# `||` kisa devre yapar).
        Assert.Equal(OffensiveTactic.InsidePost, offense.HomeOffense);
        Assert.Null(offense.HomeDefense);

        Assert.Null(CommandLine.TryParse(
            ["single", "--tactics", "Drop"], out var defense));

        Assert.Equal(DefensiveTactic.Drop, defense.HomeDefense);
        Assert.Null(defense.HomeOffense);
    }

    [Fact]
    public void AnUnknownTacticIsRefusedAndTheValidOnesAreListed()
    {
        var failure = CommandLine.TryParse(["single", "--tactics", "Zone"], out _);

        Assert.NotNull(failure);
        Assert.Contains("InsidePost", failure.Message, StringComparison.Ordinal);
        Assert.Contains("ZonePackPaint", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownPaceIsRefused()
    {
        Assert.NotNull(CommandLine.TryParse(["single", "--pace", "Hizli"], out _));
    }

    [Fact]
    public void HelpMentionsEveryVerbAndTheKeyFlags()
    {
        var text = CommandLine.HelpText();

        foreach (var token in new[]
                 {
                     "single", "batch", "export-fixtures", "help",
                     "--fixture", "--seed", "--matches", "--seed-start", "--jobs",
                     "--summary-only", "--events", "--output", "--config",
                     "--holdout-from", "--tactics", "--pace",
                 })
        {
            Assert.Contains(token, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheHelpVerbIsRecognisedInEveryForm()
    {
        foreach (var args in new[] { new[] { "help" }, ["-h"], ["--help"] })
        {
            Assert.Null(CommandLine.TryParse(args, out var request));
            Assert.Equal(CliVerb.Help, request.Verb);
        }
    }

    [Fact]
    public void TheExportFixturesVerbIsRecognised()
    {
        Assert.Null(CommandLine.TryParse(["export-fixtures"], out var request));
        Assert.Equal(CliVerb.ExportFixtures, request.Verb);
    }

    [Fact]
    public void EveryBuiltInFixtureNameIsAccepted()
    {
        var catalog = new FixtureCatalog();

        foreach (var name in catalog.Names)
        {
            var failure = CommandLine.TryParse(["batch", "--fixture", name], out var request);

            Assert.Null(failure);
            Assert.Equal(name, request.FixtureName);
        }
    }

    [Fact]
    public void TheHelpTextNamesEveryBuiltInFixture()
    {
        var text = CommandLine.HelpText();

        foreach (var name in new FixtureCatalog().Names)
        {
            Assert.Contains(name, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheAwayTacticFlagsParseIndependently()
    {
        // Savunma matrisi (08 S5) 4x4 kurabilmek icin iki taraf ayri
        // ayarlanabilmeli; aksi halde "hangi eslesme kazaniyor" sorusu
        // sorulamaz.
        Assert.Null(CommandLine.TryParse(
            ["batch", "--tactics", "InsidePost", "--away-tactics", "Drop"], out var request));

        Assert.Equal(OffensiveTactic.InsidePost, request.HomeOffense);
        Assert.Equal(DefensiveTactic.Drop, request.AwayDefense);
    }

    [Fact]
    public void TheAwayPaceFlagParses()
    {
        Assert.Null(CommandLine.TryParse(
            ["batch", "--pace", "Slow", "--away-pace", "Fast"], out var request));

        Assert.Equal(Pace.Slow, request.HomePace);
        Assert.Equal(Pace.Fast, request.AwayPace);
    }

    [Fact]
    public void TheAwayTacticFlagsAreDocumented()
    {
        var text = CommandLine.HelpText();

        Assert.Contains("--away-tactics", text, StringComparison.Ordinal);
        Assert.Contains("--away-pace", text, StringComparison.Ordinal);
    }
}
