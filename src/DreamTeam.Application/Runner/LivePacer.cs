using System.Diagnostics;
using DreamTeam.Application.Ports;

namespace DreamTeam.Application.Runner;

/// <summary>
/// M7, D109: duvar saati ile simule zamanin eslenmesi.
///
/// <para><b>HIZ CARPANI 6.0.</b> 48 simule dakika / 8 gercek dakika.
/// 8 dakika bir <i>urun kararidir</i>; carpan ondan turer ve
/// <see cref="LivePacerOptions.Speedup"/> uzerinden okunur.</para>
///
/// <para><b>ALGORITMA: "lag-behind".</b> Motor ondeyse BEKLER, gerideyse
/// BEKLEMEZ:</para>
/// <pre>
/// target = duvarSaati * speedup
/// sim    = state.Clock.ElapsedGameTimeMs
/// if (sim &lt; target) bekle(target - sim);
/// </pre>
///
/// <para><b>Neden bu, neden "her adimda sabit bekle" degil?</b> Sabit bekleme
/// 8 dakikalik hedefi ASARDI (motor bazi adimlarda 9.5 saniye ilerler) ya da
/// hedefi tutturmak icin motoru YAVASLATMAK gerekirdi. Yavaslatmak
/// <b>domain sonucunu degistirir</b> — ki bu motorun tek kuralidir. Lag-behind
/// ile toplam sure 8 dakikaya yaklasir ve <b>hicbir zaman 8 dakikayi gecmez</b>,
/// motor hic yavaslatilmaz.</para>
///
/// <para><b>DOMAN ETKILEYIMI YOK.</b> Bekleme <c>Advance</c> DONdukten SONRA
/// olur. Motorun girdisi degismez. Dogrulayan test:
/// <c>ALiveMatchProducesTheSameEventsAsSimulate</c> — ayni seed + ayni komut
/// listesi icin canli kosu, <c>Simulate</c> ile bayt bayt ayni event akisini
/// uretir.</para>
///
/// <para><b>TESTLENEBILIRLIK.</b> Zaman <see cref="IMonotonicClock"/>, bekleme
/// <see cref="IDelay"/> portlarindan gelir. Testte sahte saat ve sahte bekleme
/// kullanilir; gercek duvar saati hic cagrilmaz.</para>
/// </summary>
public sealed record LivePacerOptions
{
    /// <summary>Yapilandirma anahtari. Program.cs bu adiyla okur.</summary>
    public const string SectionName = "LivePacer";

    /// <summary>48 / 8. D109.</summary>
    public const double DefaultSpeedup = 6.0;

    public double Speedup { get; init; } = DefaultSpeedup;

    /// <summary>
    /// Tek beklemede ust sinir. Bir motor adimi beklemeyi hak ediyorsa bu
    /// kadar beklenmez; kalan fark bir sonraki adimda yakalanir. Bu, tek bir
    /// uzun adimin butun maci bloklamasini engeller.
    /// </summary>
    public int MaxSingleDelayMs { get; init; } = 2_000;

    /// <summary>Hedef sure. D109: 8 dakika.</summary>
    public int TargetWallClockMs { get; init; } = 8 * 60 * 1000;
}

/// <summary>
/// Canli yurutmenin zaman tabani. <b>Motor bu tipi GORMEZ</b>; motor yalniz
/// <c>Advance</c> cagirir. Zaman burada olcumlenir.
/// </summary>
public sealed class LivePacer
{
    private readonly IMonotonicClock _clock;
    private readonly IDelay _delay;
    private readonly LivePacerOptions _options;

    public LivePacer(IMonotonicClock clock, IDelay delay, LivePacerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(delay);

        _clock = clock;
        _delay = delay;
        _options = options ?? new LivePacerOptions();

        if (_options.Speedup <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.Speedup,
                "Hiz carpani pozitif olmalidir; sifir bolme hatasi verir.");
        }
    }

    public double Speedup => _options.Speedup;

    public long WallClockMs => _clock.ElapsedMilliseconds;

    /// <summary>Bu an icin beklenmesi gereken duvar saati.</summary>
    public long TargetSimulatedMs => (long)(_clock.ElapsedMilliseconds * _options.Speedup);

    /// <summary>
    /// Bir adimdan sonra bekler. Beklemezse 0 doner.
    ///
    /// <para><b>Testler bunu sahte <see cref="IDelay"/> ile dogrular</b>:
    /// sahte saatte beklenen sure kaydedilir, gercek sure gecmez.</para>
    /// </summary>
    public async Task<int> WaitAfterStepAsync(long simulatedElapsedMs, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(simulatedElapsedMs);

        var wait = (int)Math.Min(
            _options.MaxSingleDelayMs,
            Math.Max(0, TargetSimulatedMs - simulatedElapsedMs));

        if (wait <= 0)
        {
            return 0;
        }

        await _delay.DelayAsync(wait, cancellationToken).ConfigureAwait(false);
        return wait;
    }

    /// <summary>
    /// Hedef sure asildi mi? Raporlama ve testler icin. Pacerin davranisini
    /// DEGISTIRMEZ; yalnizca sorar.
    /// </summary>
    public bool ExceededBudget() => _clock.ElapsedMilliseconds > _options.TargetWallClockMs;
}

/// <summary>Test ve prod uyumlu sahte bekleme. Gercek sure gecmez.</summary>
public sealed class RecordingDelay : IDelay
{
    private readonly List<int> _waits = [];

    public IReadOnlyList<int> Waits => _waits;

    public long TotalWaitMs => _waits.Sum();

    public int WaitCount => _waits.Count;

    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        _waits.Add(milliseconds);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Testler icin elle ilerletilen saat. <c>Advance</c> saniyeleri uzerinden
/// surulur; duvar saati hic okunmaz.
/// </summary>
public sealed class ManualClock : IMonotonicClock
{
    public long ElapsedMilliseconds { get; private set; }

    public void Advance(long milliseconds) => ElapsedMilliseconds += milliseconds;
}

/// <summary>Prod: gercek monotonik duvar saati. Yalniz Application tarafinda.</summary>
public sealed class SystemMonotonicClock : IMonotonicClock
{
    private readonly long _origin = Stopwatch.GetTimestamp();
    private readonly double _toMilliseconds = 1000.0 / Stopwatch.Frequency;

    public long ElapsedMilliseconds =>
        (long)((Stopwatch.GetTimestamp() - _origin) * _toMilliseconds);
}
