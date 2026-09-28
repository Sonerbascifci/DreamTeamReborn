using System.Collections.Concurrent;

namespace DreamTeam.Application.Runner;

/// <summary>
/// M7: matchId -> canli <see cref="MatchSession"/>.
///
/// <para><b>Neden bellek ici?</b> 03 §"API ve persistence": "Baslangic icin
/// moduler monolit ve tek mac yurutucusu onerilir." Tek instance oldugu icin
/// dagitik lease/fencing gereksizdir. Oturum <b>kalici degildir</b>: sunucu
/// yeniden basladiginda kaybolur ve D113'e gore mac Aborted sayilir.</para>
///
/// <para><b>Is parcacigi guvenligi.</b> <c>ConcurrentDictionary</c> yalnizca
/// OTURUMUN KENDISI degil, degiskenin degistirilmesini guvence altina alir.
/// Durumun kendisi <see cref="MatchSession"/> icindeki kilitle korunur.</para>
///
/// <para><b>Kimse ekleyemez.</b> Kayit yalniz <see cref="GetOrAdd"/> ile
/// yapilir; ayni matchId icin ikinci bir oturum YARATILAMAZ. Bu, 09'un "ayni
/// mac iki runner tarafindan eszamanli ilerletilmez" kabulunun yapisma
/// tarafi.</para>
/// </summary>
public sealed class MatchSessionStore
{
    private readonly ConcurrentDictionary<Guid, MatchSession> _sessions = new();

    public int Count => _sessions.Count;

    /// <summary>
    /// Canli mac kimlikleri. <b>Sirali</b> dondurulur: 
    /// <c>ConcurrentDictionary</c> anahtar sirasi garantisi vermedigi icin
    /// testler ve loglar ayni girdide ayni listeyi gormelidir.
    /// </summary>
    public IReadOnlyList<Guid> ActiveMatches => [.. _sessions.Keys.OrderBy(id => id)];

    /// <summary>
    /// Oturum varsa dondurur; yoksa <paramref name="factory"/> ile olusturur.
    ///
    /// <para><b>DIKKAT: fabrika birden fazla kez CAOGRULABILIR.</b>
    /// <c>ConcurrentDictionary.GetOrAdd</c> bunu belgeler: yarismali bir eklemede
    /// birden fazla cagiran fabrikayi calistirir, yalniz BIRI kazanir. Burada
    /// iki sey yapiliyor:</para>
    /// <list type="number">
    ///   <item><description>KAZANAN disinda uretilen oturumlar
    ///   <see cref="IDisposable"/> olduklari icin <b>dispose edilir</b>.
    ///   Dispose edilmezse her yaris bir motor durumu ve bir
    ///   <c>SemaphoreSlim</c> sizdirir; 100.000 yaris 100.000 sizinti demektir.</description></item>
    ///   <item><description>Cagiranlara <b>her zaman ayni</b> oturum doner.
    ///   Kaybeden bir oturum gorurse o oturumda adim atmis olabilir ve
    ///   09'un "ayni mac iki runner tarafindan ilerletilmez" kabulu
    ///   ihlal edilirdi.</description></item>
    /// </list>
    /// </summary>
    public MatchSession GetOrAdd(Guid matchId, Func<MatchSession> factory)
    {
        MatchIdGuard.Require(matchId);
        ArgumentNullException.ThrowIfNull(factory);

        // YOL 1 (SICAK): oturum zaten var. Fabrika HIC CALISMAZ.
        if (_sessions.TryGetValue(matchId, out var existing))
        {
            return existing;
        }

        // YOL 2 (SOGUK): bu yarisi kazanmayi deniyoruz.
        var candidate = factory();

        if (_sessions.TryAdd(matchId, candidate))
        {
            return candidate;
        }

        // KAYBETTIK: baska biri araya girdi. Bu oturum depoya girmedi ve
        // hicbir cagri tarafindan gorulmedi; birakip sizdirmamak icin
        // kapatiliyor. Sonra KAZANANIN oturumu dondurulur.
        //
        // <b>BURADA DONGU YOK.</b> Kazananin oturumu depoda kalicidir ve bir
        // daha silinmez; kaybeden her denemede ayni sonucu gorur ve bir kez
        // doner. Ilk denemede dongu kurulmus ve GetOrAdd her cagrida yeni
        // aday dondurdugu icin SONSUZA KADAR donmus; test asili kalmisti.
        candidate.Dispose();
        return _sessions[matchId];
    }

    public MatchSession? Find(Guid matchId) =>
        _sessions.TryGetValue(matchId, out var session) ? session : null;

    public bool Remove(Guid matchId, out MatchSession? session) =>
        _sessions.TryRemove(matchId, out session);

    /// <summary>Terminal olan oturumleri toplar ve kaldirir.</summary>
    public IReadOnlyList<MatchSession> DrainTerminal()
    {
        var drained = new List<MatchSession>();

        foreach (var pair in _sessions)
        {
            if (pair.Value.IsTerminal && _sessions.TryRemove(pair.Key, out var removed))
            {
                drained.Add(removed);
            }
        }

        return drained;
    }

    /// <summary>
    /// D113: sunucu yeniden basladi. Kalan tum oturumler kapatilir; canli mac
    /// kurtarilmaz. <c>Running</c> durumundaki kalici maclar
    /// <c>IMatchRepository.AbortRunningAsync</c> ile isaretlenir.
    /// </summary>
    public int ClearAll()
    {
        var count = 0;

        foreach (var pair in _sessions)
        {
            if (_sessions.TryRemove(pair.Key, out var removed))
            {
                removed.Dispose();
                count += 1;
            }
        }

        return count;
    }
}
