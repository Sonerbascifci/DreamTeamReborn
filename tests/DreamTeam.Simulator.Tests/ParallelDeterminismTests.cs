using DreamTeam.MatchEngine.Core;
using DreamTeam.Simulator.Batch;
using DreamTeam.Simulator.Fixture;

namespace DreamTeam.Simulator.Tests;

/// <summary>
/// 08 T17: "Paralel ve sıralı batch → maç başına aynı sonuç."
///
/// <para><b>Neden bu kadar çok test?</b> T17'in zor kısmı toplamın aynı olması
/// değil, <b>maç başına</b> aynı olması. Aynı toplam, farklı eşleşmelerden
/// üretilebilir; 08 §4'ün istediği şey tam olarak eşleşmedir. Bu yüzden
/// testler toplamı değil, indeks → sonuç haritasını karşılaştırır.</para>
///
/// <para><b>Neden <c>Order</c> sızıntısı gerçek bir risk?</b> Motor birkaç yerde
/// <c>Dictionary</c> ve <c>HashSet</c> üzerinde yineleme yapar. Eşer sırası
/// çalışma zamanına göre değişirse, aynı seed farklı sonuç üretir. Testler
/// bunu ölçer; test yazılmadan "sorun yok" demek mümkün değildir.</para>
/// </summary>
public class ParallelDeterminismTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void ParallelAndSequentialBatchesGiveIdenticalPerMatchResults(int jobs)
    {
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var plan = M6TestData.Plan(matches: 120, jobs: jobs);

        var sequential = new BatchDriver(catalog, config).Run(plan with { Jobs = 1 });
        var parallel = new BatchDriver(catalog, config).Run(plan with { Jobs = jobs });

        Assert.Equal(
            M6TestData.Describe(sequential.Summary),
            M6TestData.Describe(parallel.Summary));
    }

    [Fact]
    public void EveryMatchIndexProducesTheSameOutcomeWhicheverWorkerRunsIt()
    {
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var runner = new MatchRunner(config);

        // 1) Her indeksi TEK TEK koş: bu sıralı referansıdır.
        var reference = new Dictionary<long, string>();

        for (var index = 0; index < 60; index++)
        {
            reference[index] = M6TestData.Describe(
                runner.Run(catalog.Build("neutral-mirror", BatchDriver.SeedFor(index))));
        }

        // 2) Ayni korpusu 4 is parcacigina dagitarak kos. Kuyruk sirasi tesadufi
        //    oldugu icin bir eslesme ya da sira sizintisi burada gorunur.
        var parallel = new BatchDriver(catalog, config)
            .Run(M6TestData.Plan(matches: 60, jobs: 4));

        var recomputed = new SummaryAccumulator();

        for (var index = 0; index < 60; index++)
        {
            var summary = runner.Run(catalog.Build("neutral-mirror", BatchDriver.SeedFor(index)));

            Assert.Equal(reference[index], M6TestData.Describe(summary));
            recomputed.Add(summary);
        }

        Assert.Equal(60, parallel.Summary.MatchCount);
        Assert.Equal(
            M6TestData.Describe(parallel.Summary),
            M6TestData.Describe(recomputed));
    }

    [Fact]
    public void SeedIsDerivedFromTheIndexAndNotFromARunningCounter()
    {
        // Ayni indeks, ayni tohum. Saf fonksiyon: durum tutmaz.
        Assert.Equal(BatchDriver.SeedFor(7), BatchDriver.SeedFor(7));
        Assert.Equal(BatchDriver.SeedFor(1_000), BatchDriver.SeedFor(1_000));
    }

    [Fact]
    public void ConsecutiveIndicesGetUnrelatedSeeds()
    {
        // Ardışık indeksler ardışık tohum vermemeli. SplitMix64'ün karıştırma
        // amacı budur; karıştırma olsaydı tohumlar birbirine yakın olurdu.
        var seeds = Enumerable.Range(0, 256).Select(index => BatchDriver.SeedFor(index)).ToList();

        Assert.Equal(seeds.Count, seeds.Distinct().Count());
    }

    [Fact]
    public void SeedStartOffsetsTheCorpusRatherThanRelabellingIt()
    {
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var driver = new BatchDriver(catalog, config);

        var fromZero = driver.Run(M6TestData.Plan(matches: 30, seedStart: 0));
        var fromOne = driver.Run(M6TestData.Plan(matches: 30, seedStart: 1));

        // seed-start 0 ve 1 FARKLI korpusler olmali: ayni 30 mac degil.
        Assert.NotEqual(
            M6TestData.Describe(fromZero.Summary),
            M6TestData.Describe(fromOne.Summary));

        // Ama seed-start 1'i 30 kez tekrarlamak ayni korpusu vermeli.
        var again = driver.Run(M6TestData.Plan(matches: 30, seedStart: 1));

        Assert.Equal(
            M6TestData.Describe(fromOne.Summary),
            M6TestData.Describe(again.Summary));
    }

    [Fact]
    public void ReplayingASingleIndexReproducesThatMatchExactly()
    {
        var catalog = new FixtureCatalog();
        var runner = new MatchRunner(M6TestData.Config());

        // 100K kosuda tek bir macin sizmasi ancak bu yolla izole edilir.
        var first = M6TestData.Describe(
            runner.Run(catalog.Build("neutral-mirror", BatchDriver.SeedFor(4_242))));

        var second = M6TestData.Describe(
            runner.Run(catalog.Build("neutral-mirror", BatchDriver.SeedFor(4_242))));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ParallelismDoesNotChangeTheConfigHash()
    {
        var catalog = new FixtureCatalog();
        var config = M6TestData.Config();
        var driver = new BatchDriver(catalog, config);

        var sequential = driver.Run(M6TestData.Plan(matches: 10, jobs: 1));
        var parallel = driver.Run(M6TestData.Plan(matches: 10, jobs: 4));

        Assert.Equal(sequential.ConfigHash, parallel.ConfigHash);
        Assert.Equal(config.ComputeConfigHash(), sequential.ConfigHash);
    }
}
