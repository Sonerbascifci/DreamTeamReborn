using System.Collections.Concurrent;
using System.Diagnostics;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.Simulator.Fixture;

namespace DreamTeam.Simulator.Batch;

/// <summary>M6: how a batch is parameterised. One record, validated once.</summary>
public sealed record BatchPlan
{
    public required string FixtureName { get; init; }

    public required long MatchCount { get; init; }

    /// <summary>
    /// The first match index. <b>This is an index offset, not a seed.</b> Match
    /// <c>i</c> uses <c>SeedFor(seedStart + i)</c>, so two runs with different
    /// <c>SeedStart</c> values are different corpora rather than the same corpus
    /// relabelled.
    /// </summary>
    public required long SeedStart { get; init; }

    public required int Jobs { get; init; }

    public required bool SummaryOnly { get; init; }

    /// <summary>
    /// Match indices reserved for validation and excluded from tuning (08 §8 step 7).
    /// Reported so a reader can confirm the holdout was actually held out.
    /// </summary>
    public long? HoldoutFrom { get; init; }

    public long LastSeedIndex => SeedStart + MatchCount - 1;
}

/// <summary>M6: the outcome of a batch run, including the cost of running it.</summary>
public sealed record BatchOutcome
{
    public required BatchPlan Plan { get; init; }

    public required SummaryAccumulator Summary { get; init; }

    public required string ConfigHash { get; init; }

    public required TimeSpan WallClock { get; init; }

    public required long PeakWorkingSetBytes { get; init; }

    public required int ProcessedMatches { get; init; }

    /// <summary>Matches per second. Reported, never predicted (08 §119).</summary>
    public double MatchesPerSecond => WallClock.TotalSeconds <= 0
        ? 0
        : ProcessedMatches / WallClock.TotalSeconds;
}

/// <summary>
/// M6: seed derivation and the sequential/parallel driver.
///
/// <para><b>Seed derivation (08 §4, T17).</b> A match's seed comes from its
/// <i>index</i>, never from a running counter or a shared RNG:
/// <c>seed(i) = mix(SeedStart + i)</c> where <c>mix</c> is SplitMix64's output
/// function. That gives three properties the batch depends on:</para>
/// <list type="bullet">
///   <item><description>Match <c>i</c> has the same seed no matter which worker
///   runs it, or in what order. Parallel and sequential therefore agree
///   <b>per match</b>, not just in aggregate.</description></item>
///   <item><description>No shared mutable RNG exists, so there is nothing to race
///   on. 08 §4 requires exactly this.</description></item>
///   <item><description>Re-running a single index reproduces that match exactly,
///   which is what makes a 100K failure investigable.</description></item>
/// </list>
///
/// <para><b>Why jobs default to 1.</b> Sequential is the reference. A parallel run
/// is only meaningful as evidence if it matches the sequential one, and that is a
/// test (T17), not a default.</para>
/// </summary>
public sealed class BatchDriver
{
    private readonly IFixtureSource _fixtures;
    private readonly EngineConfig _config;

    public BatchDriver(IFixtureSource fixtures, EngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(fixtures);
        ArgumentNullException.ThrowIfNull(config);

        _fixtures = fixtures;
        _config = config;
    }

    public string ConfigHash => _config.ComputeConfigHash();

    /// <summary>
    /// The seed of match index <paramref name="index"/>. SplitMix64's output
    /// function on the index, so consecutive indices give unrelated seeds and the
    /// mapping is a pure function with no state.
    /// </summary>
    public static ulong SeedFor(long index)
    {
        unchecked
        {
            var z = (ulong)index + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>True when this index is inside the declared holdout range.</summary>
    public static bool IsHoldout(BatchPlan plan, long index) =>
        plan.HoldoutFrom is { } from && index >= from;

    /// <summary>
    /// Builds the setup for a match index.
    ///
    /// <para><b>Neden bir fabrika?</b> Ilk surum surucu dogrudan
    /// <c>source.Build(...)</c> cagiriyordu. Boylece <c>--tactics</c> /
    /// <c>--pace</c> bayraklari <c>single</c> komutunda calisiyor ama
    /// <c>batch</c> komutunda <b>sessizce yok sayiliyordu</b>: 1.500 maclik
    /// uc farkli taktigin raporlari bayt bayt ayni cikti. Tek maçta ise taktik
    /// degismis gorunuyordu, yani hata ancak iki komutu karsilastirarak
    /// yakalanabiliyordu. Simdi tek bir fabrika her iki komutun da kullandigi
    /// yoldur; bir komutta calisan bir girdi digerinde calismaz.</para>
    /// </summary>
    public delegate MatchSetup SetupFactory(ulong seed);

    public SetupFactory FactoryFor(string fixtureName) => seed => _fixtures.Build(fixtureName, seed);

    public BatchOutcome Run(BatchPlan plan) => Run(plan, FactoryFor(plan.FixtureName));

    public BatchOutcome Run(BatchPlan plan, SetupFactory factory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(factory);

        var stopwatch = Stopwatch.StartNew();
        var accumulator = new SummaryAccumulator();
        var processed = 0;

        if (plan.Jobs <= 1)
        {
            var runner = new MatchRunner(_config);

            for (var offset = 0L; offset < plan.MatchCount; offset++)
            {
                var index = plan.SeedStart + offset;
                accumulator.Add(runner.Run(factory(BatchDriver.SeedFor(index))));
                processed += 1;
            }
        }
        else
        {
            processed = RunParallel(plan, factory, accumulator);
        }

        stopwatch.Stop();

        return new BatchOutcome
        {
            Plan = plan,
            Summary = accumulator,
            ConfigHash = ConfigHash,
            WallClock = stopwatch.Elapsed,
            PeakWorkingSetBytes = Environment.WorkingSet,
            ProcessedMatches = processed,
        };
    }

    /// <summary>
    /// Runs the same corpus across workers. Each index is independent: separate
    /// <c>MatchSetup</c>, separate <c>MatchSimulation</c>, separate
    /// <c>MatchRunner</c>. Nothing is shared but the read-only config.
    ///
    /// <para>Results land in a <see cref="ConcurrentQueue{T}"/> and are merged by
    /// the calling thread afterwards, so accumulation is single threaded and
    /// order independent. The queue is drained fully, so a partial batch can never
    /// be mistaken for a complete one.</para>
    /// </summary>
    private int RunParallel(BatchPlan plan, SetupFactory factory, SummaryAccumulator accumulator)
    {
        var completed = new ConcurrentQueue<MatchSummary>();
        var nextIndex = -1L;
        var processed = 0;

        void Worker()
        {
            var runner = new MatchRunner(_config);

            while (true)
            {
                var offset = Interlocked.Increment(ref nextIndex);

                if (offset >= plan.MatchCount)
                {
                    return;
                }

                var index = plan.SeedStart + offset;
                completed.Enqueue(runner.Run(factory(SeedFor(index))));
            }
        }

        var workers = new Thread[plan.Jobs];

        for (var worker = 0; worker < plan.Jobs; worker++)
        {
            workers[worker] = new Thread(Worker)
            {
                IsBackground = true,
                Name = $"simulator-match-{worker}",
            };

            workers[worker].Start();
        }

        foreach (var worker in workers)
        {
            worker.Join();
        }

        while (completed.TryDequeue(out var summary))
        {
            accumulator.Add(summary);
            processed += 1;
        }

        return processed;
    }
}
