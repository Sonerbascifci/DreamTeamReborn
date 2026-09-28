using DreamTeam.Application.Ports;
using DreamTeam.Application.Runner;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;

namespace DreamTeam.Infrastructure;

/// <summary>
/// M7: gercek monotonik saat. Application tarafinda duvar saati yalniz BURADA
/// olcumlenir.
///
/// <para><b>STOPWATCH NEDEN <c>DateTime.UtcNow</c> DEGIL?</b> Sistem saati
/// geriye veya ileriye atlayabilir (NTP, kullanici degisikligi). Bir macin
/// 8 dakika surmesi gerekiyor; saat atlamasi maci 7 dakika yapar ya da
/// 10 dakika uzatir. <c>Stopwatch</c> monotoniktir.</para>
///
/// <para><b>MOTOR BU TIPE BAKMAZ.</b> Motor duvar saati bilmez; pacer bekler,
/// motor ilerler. Ayrim 05 §14 ve D101'in geregidir.</para>
/// </summary>
public sealed class SystemClock : IMonotonicClock
{
    private readonly long _origin = System.Diagnostics.Stopwatch.GetTimestamp();
    private readonly double _toMilliseconds = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    public long ElapsedMilliseconds =>
        (long)((System.Diagnostics.Stopwatch.GetTimestamp() - _origin) * _toMilliseconds);
}

/// <summary>
/// M7: gercek bekleme. Yalniz <see cref="LivePacer"/> bunu cagirir ve
/// <c>Advance</c> dondukten SONRA.
///
/// <para><b>NEDEN BURADA?</b> Motor icinde <c>Task.Delay</c> olsaydi motor
/// duvar saatine baglanirdi. 05 §14 bunu yasakliyor ve M6'nin T15'i bunu
/// ayrica olculdu: ac/kapa diagnostics ayni sonucu verdi. Burada bekleme
/// motorun <i>disinda</i> oldugu icin ayni ozellik yapida garanti edilir.</para>
/// </summary>
public sealed class SystemDelay : IDelay
{
    public async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds > 0)
        {
            await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// M7: denge belgesinden motor config'i.
///
/// <para><b>TEK OKUYUCU.</b> D115: <c>BalanceConfigStore</c> motorun
/// <c>Config</c> katmaninda yasar; CLI de, sunucu da ayni kodu kullanir.
/// Burada ikinci bir JSON okuyucu yazsaydi "bu rapor hangi config ile
/// uretildi" sorusu tekrar cevapsiz kalirdi (M6 D103).</para>
///
/// <para><b>BELLEKTE BIR KEZ OKUNUR.</b> Config her mac basinda okunsa
/// dosya sistemi mac basina bir gezinme olurdu. Sunucu acilirken bir kez
/// okunur ve <see cref="Hash"/> ile birlikte saklanir.</para>
///
/// <para><b>HASH MOTORDEN GELIR.</b> Burada yeniden hesaplanmaz; motor
/// kendi config'inden turetir. Boylece kayitlanan hash ile motorun kullandigi
/// hash ayni sey olur.</para>
/// </summary>
public sealed class FileMatchConfigProvider : IMatchConfigProvider
{
    private readonly EngineConfig _config;
    private readonly MatchSimulationHash _hash;

    public FileMatchConfigProvider(string balanceConfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(balanceConfigPath);

        var document = BalanceConfigStore.Load(balanceConfigPath);

        SourcePath = balanceConfigPath;
        _config = document.ToEngineConfig();
        _hash = new MatchSimulationHash(
            new MatchSimulation(_config).ConfigHash);
    }

    public EngineConfig GetConfig(out string configHash)
    {
        configHash = _hash.Value;
        return _config;
    }

    public string ConfigHash => _hash.Value;

    /// <summary>Kaynak dosyanin yolu; tanilama ve loglar icin.</summary>
    public string SourcePath { get; }

    private sealed record MatchSimulationHash(string Value);
}
