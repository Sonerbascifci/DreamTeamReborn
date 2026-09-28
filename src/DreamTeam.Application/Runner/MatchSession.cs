using System.Collections.Immutable;
using DreamTeam.Application.Ports;
using DreamTeam.MatchEngine.Commands;
using DreamTeam.MatchEngine.Config;
using DreamTeam.MatchEngine.Core;
using DreamTeam.MatchEngine.Events;
using DreamTeam.MatchEngine.Replay;

namespace DreamTeam.Application.Runner;

/// <summary>
/// M7: biten macin motor tarafindan uretilmis ozeti. Tum sayilar event
/// akisindan gelir; hicbiri istemciden alinmaz.
/// </summary>
public sealed record SessionOutcome(
    MatchState State,
    ImmutableArray<MatchEvent> Events,
    MatchStatus Status,
    int HomeScore,
    int AwayScore,
    int HomePossessions,
    int AwayPossessions,
    long ElapsedGameTimeMs,
    int PeriodsPlayed,
    string? AbortReason)
{
    public bool IsTie => Status == MatchStatus.Completed && HomeScore == AwayScore;
}

/// <summary>M7: tek bir adimin sonucu. 03 §StepResult ile uyumlu.</summary>
public sealed record SessionStepResult(
    MatchState State,
    ImmutableArray<MatchEvent> Events,
    ImmutableArray<CommandResult> CommandResults,
    long Epoch,
    bool IsTerminal);

/// <summary>
/// M7: reconnect yaniti. 07 §8: snapshot + event siniri ATOMIK.
///
/// <para><b>SEQUENCE SOZLESMESI (Dikkat: iki farkli sayim var).</b></para>
/// <list type="number">
///   <item><description><c>SnapshotSequence</c> ve <c>CurrentSequence</c> =
///   <b>son TESLIM EDILEN</b> sequence'dir. "Bana bundan sonrasini ver"
///   sorusunda kullanilacak deger budur.</description></item>
///   <item><description>Motorun snapshot JSON'undaki <c>StateSequence</c> ise
///   motorun <c>NextSequence</c> alanidir, yani <b>sonraki</b> sequence'dir
///   ve <c>SnapshotSequence + 1</c>'dir. M5'teki format bu yuzden
///   DEGISTIRILMEDI; fark burada belgeleniyor.</description></item>
/// </list>
///
/// <para><b>SONUC:</b> Bir istemci snapshot'tan devam edecekse
/// <c>StateSequence - 1</c>'i istemelidir. API katmani bunu
/// <see cref="SnapshotSequence"/> olarak zaten saglar; ham JSON'i okuyan bir
/// istemci <c>StateSequence</c>'yi OLCEK OLCEK <c>CurrentSequence</c> sanmamalidir.</para>
/// </summary>
public sealed record SessionCapture(
    long SnapshotSequence,
    string ConfigHash,
    string? SnapshotJson,
    ImmutableArray<MatchEvent> Events,
    long CurrentSequence,
    long RequestedFrom)
{
    /// <summary>
    /// Ham snapshot JSON'undaki <c>StateSequence</c> ("sonraki" isaretcisi).
    /// Yalnizca <see cref="SnapshotJson"/>'u cozup sunucuyla konusacak bir
    /// istemci icindir. HTTP/SignalR yanitinda <b>kullanilmaz</b>.
    /// </summary>
    public long RawSnapshotNextSequence => SnapshotSequence + 1;
}

/// <summary>
/// M7: bir macin <b>TEK SAHIBI</b>.
///
/// <para><b>03 §"API ve persistence":</b> "Match runner ayni macin state'ini tek
/// sahip altinda seri degistirir." Bu tip o sahiptir. State asla disaridan
/// degistirilmez; disaridan yalniz <b>kilit altinda</b> okunur ve komutlar
/// <b>kuyruga eklenir</b>.</para>
///
/// <para><b>Neden kilit?</b> 09'un M7 kabul maddesi: "ayni mac iki runner
/// tarafindan eszamanli ilerletilmez." Iki is parcacigi ayni anda
/// <c>StepAsync</c> cagirirsa ikincisi birincinin SONRAKI durumunu gorur.
/// Kilit disinda okunan bir durum yaris (torn) olurdu.</para>
///
/// <para><b>EPOCH.</b> Her adimda bir artar. Disaridan gelen bir istek
/// (ornegin komut gonderimi) epoch'u okur; kilit altinda yeniden dogrulanir.
/// Kilit disinda dogrulanmasaydi iki cagiran arasindaki adimda yanlis
/// kabul edilirdi.</para>
///
/// <para><b>MOTOR SAHİPLİĞİ BURADA BİTMİYOR.</b> Bu tip motora <b>dokunmaz</b>;
/// yalniz <c>Create</c>/<c>Advance</c> cagirir. Motor saf kalir (05 §14).</para>
/// </summary>
public sealed class MatchSession : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly MatchSimulation _engine;
    private readonly List<MatchEvent> _events = [];
    private readonly List<ScheduledManagerCommand> _incoming = [];
    private readonly Guid _matchId;
    private readonly Guid _ownerUserId;
    private readonly string _configHash;

    private MatchState _state;
    private long _epoch;
    private bool _disposed;

    public MatchSession(Guid matchId, Guid ownerUserId, MatchSetup setup, EngineConfig config)
    {
        MatchIdGuard.Require(matchId);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(config);

        _matchId = matchId;
        _ownerUserId = ownerUserId;
        _engine = new MatchSimulation(config);
        _configHash = _engine.ConfigHash;
        _state = _engine.Create(setup);
    }

    public Guid MatchId => _matchId;

    /// <summary>07 S5: sahiplik sunucuda cozulur; istemci beyani guvenilmez.</summary>
    public Guid OwnerUserId => _ownerUserId;

    public string ConfigHash => _configHash;

    /// <summary>Adim sayaci. Kilit DISINDA okunabilir; yalnizca bilgi icin.</summary>
    public long Epoch => Interlocked.Read(ref _epoch);

    /// <summary>
    /// Guncel sequence. KILIT ALTINDA okunur.
    ///
    /// <para><b>Neden Interlocked yok?</b> <c>MatchState</c> bir record'dur ve
    /// <c>NextSequence</c> bir <i>property</i>'dir; <c>ref</c> ile alinamaz.
    /// Salt-okunur bir kopya tutmak da ayni yaris (torn) riskini tasirdi:
    /// property dondurulmus bir record oldugu icin okuma aninda tutarli
    /// DEGILDIR. Tek dogru yol kilidi almaktir.</para>
    /// </summary>
    public long CurrentSequence
    {
        get
        {
            _gate.Wait();

            try
            {
                return _state.NextSequence - 1;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public bool IsTerminal
    {
        get
        {
            _gate.Wait();

            try
            {
                return _state.IsTerminal;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    /// <summary>
    /// Komutu kuyruga ekler. Motor YALNIZCA bir sonraki adimda gorur.
    ///
    /// <para><b>Neden dogrudan uygulanmaz?</b> 07 §5: komut MANTIKSAL sinira
    /// baglanir, duvar saatine degil. M5'in <c>CommandQueue</c>'si sinir gelince
    /// bosaltir; sunucu bu sirayi bozmaz.</para>
    /// </summary>
    /// <param name="expectedEpoch">
    /// Istemcinin gordugu epoch. <c>null</c> ise denetlenmez. Cagirmak
    /// ANINDA okunur; kuyruga ekleme KILIT ALTINDA olur.
    /// </param>
    public bool TryEnqueueCommand(ScheduledManagerCommand command, long? expectedEpoch = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (expectedEpoch is { } expected && expected != Epoch)
        {
            return false;
        }

        _gate.Wait();

        try
        {
            if (expectedEpoch is { } checkedEpoch && checkedEpoch != _epoch)
            {
                return false;
            }

            if (_state.IsTerminal)
            {
                // 07 §6: mac bittiginde bekleyen komutlar Expired/Rejected olur.
                return false;
            }

            _incoming.Add(command);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Tek bir motor adimi. KILIT ALTINDA. Iki eszamanli cagri sirali olur.
    /// </summary>
    public async Task<SessionStepResult> StepAsync(CancellationToken cancellationToken)
    {
        // Kapatilmis oturum yeni adim ALMAZ. Yarim kalmis bir cagri
        // (kilit icindeyken) normal sekilde tamamlanir; yalniz yeni
        // baslayan bir cagri reddedilir.
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_state.IsTerminal)
            {
                return new SessionStepResult(_state, [], [], _epoch, IsTerminal: true);
            }

            var batch = ImmutableArray.CreateRange(_incoming);
            _incoming.Clear();

            var step = _engine.Advance(_state, batch);
            _state = step.State;
            _events.AddRange(step.Events);

            _epoch = Interlocked.Increment(ref _epoch);

            return new SessionStepResult(
                _state,
                [.. step.Events],
                [.. step.CommandResults],
                _epoch,
                _state.IsTerminal);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reconnect yaniti. 07 §8: "Snapshot ile eventlerin ortusme/bosluk siniri
    /// ATOMIK tanimlanir."
    ///
    /// <para><b>Neden kilit altinda?</b> Sequence bir kez okunur ve hem
    /// snapshot hem event listesi AYNI okumanin sonucu kullanir. Iki ayri
    /// okuma yapilsaydi araya yeni bir adimin event'i girerdi ve istemci
    /// bosluk gormeden kayardi.</para>
    /// </summary>
    public async Task<SessionCapture> CaptureForAsync(
        long lastAppliedSequence,
        bool includeSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lastAppliedSequence);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var current = _state.NextSequence - 1;

            if (lastAppliedSequence > current)
            {
                // Istemci bize olmadik bir sequence soyluyor. Sessizce bos
                // donmek, istemciyi sonsuza kadar bekletir.
                throw new InvalidOperationException(
                    $"Istemci sequence {lastAppliedSequence} sunucudan ileride " +
                    $"(sunucu en fazla {current}).");
            }

            var snapshotSequence = includeSnapshot ? current : -1;
            var floor = snapshotSequence >= 0 ? snapshotSequence : lastAppliedSequence;

            var events = _events
                .Where(matchEvent => matchEvent.Sequence > floor)
                .ToImmutableArray();

            var json = includeSnapshot
                ? MatchSnapshotSerializer.ToJson(
                    MatchSnapshot.Capture(_state, _configHash))
                : null;

            // <b>RequestedFrom NEDEN GEREKIYOR?</b> API katmani istemciye
            // "bu yanitta hangi sequence'ten sonrasini VERDIM" demelidir.
            // Snapshot varsa bu deger snapshot'in sequence'idir; yoksa
            // istemcinin gonderdigi afterSequence'in KENDISIDIR.
            //
            // Ilk yazimda API, snapshot istendiginde olmadigi icin
            // FromSequence yerine CurrentSequence donuyordu. Yanit 1..1051
            // eventi icerirken from=1051 diyordu: istemci "1'den basladim"
            // bilgisini kaybediyor ve bir sonraki istekte nereye
            // devam edecegini bilemiyordu. Tek hata, sessiz ve surekli.
            return new SessionCapture(
                snapshotSequence, _configHash, json, events, current, lastAppliedSequence);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Macin tamamini duraklama olmadan yurutur.
    ///
    /// <para><b>Pacer YOK.</b> Bu yol sunucu zamanina baglanmaz; hiz testleri
    /// ve "canli == cevrimdisi" dogrulamasindadir. Canli yurutme
    /// <see cref="RunLiveAsync"/> ile yapilir.</para>
    ///
    /// <para><b>Neden <c>MatchResult</c> DEGIL?</b> Motorun <c>Project</c> adimi
    /// ozel (private). Application tarafinda bir kopya yazmak iki hesap yolu
    /// demektir ve 05 §"cift istatistik" yasagini ihlal eder. Oturum motorun
    /// <b>gercek ciktisini</b> (state + event) dondurur; sonucu kullanan use
    /// case kurar.</para>
    /// </summary>
    public async Task<SessionOutcome> RunToCompletionAsync(CancellationToken cancellationToken)
    {
        var events = new List<MatchEvent>();

        while (!_state.IsTerminal)
        {
            var step = await StepAsync(cancellationToken).ConfigureAwait(false);
            events.AddRange(step.Events);

            if (step.Events.Length == 0 && !step.IsTerminal)
            {
                throw new InvalidOperationException(
                    "Ilerleme yok; motor durdu. Bu bir hata durumudur (06 §2).");
            }
        }

        return BuildOutcome(events);
    }

    /// <summary>
    /// Terminal oturumun ozetini dondurur. <c>RunToCompletionAsync</c> bunu
    /// kendisi kurar; ayrica cagrilabilir cunku <c>Advance</c> adim adim
    /// surulmus bir oturumun <b>birikmis</b> event akisini da ayni ozete
    /// cevirebilmelidir.
    ///
    /// <para><b>Terminal olmayan oturumda cagirmak yanlistir</b>: ozet
    /// eksik olur. Bunu sessizce kabul etmek yerine reddediyoruz.</para>
    /// </summary>
    public SessionOutcome CompletedOutcome()
    {
        _gate.Wait();

        try
        {
            if (!_state.IsTerminal)
            {
                throw new InvalidOperationException(
                    "Ozet yalnizca terminal bir oturum icin uretilebilir; bu oturum hala suruyor.");
            }

            return BuildOutcome(_events);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Iptal ozeti. <b>TERMINAL OLMAYAN</b> oturum icin de uretilebilir —
    /// D113 ve yurutucu hatasi yollarinda mac bitmeden iptal edilir.
    ///
    /// <para><b>Skor neden 0 degil, mevcut skor?</b> Bunlar MOTORUN durumundan
    /// okunur. Iptal edilen bir macin "kim kazandi" sorusu olmaz; ama kayit
    /// gercek oldugu icin ayiklama (analiz) yapilabilir ve skor uydurmak
    /// yerine dogru olani yazmak daha degerlidir.</para>
    /// </summary>
    public SessionOutcome AbortOutcome(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _gate.Wait();

        try
        {
            return BuildOutcome(_events) with
            {
                Status = MatchStatus.Aborted,
                AbortReason = reason,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Canli yurutme: her adimdan sonra pacer bekler.</summary>
    public async Task<SessionOutcome> RunLiveAsync(LivePacer pacer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pacer);

        var events = new List<MatchEvent>();

        while (!_state.IsTerminal)
        {
            var step = await StepAsync(cancellationToken).ConfigureAwait(false);
            events.AddRange(step.Events);

            if (step.Events.Length == 0 && !step.IsTerminal)
            {
                throw new InvalidOperationException(
                    "Ilerleme yok; motor durdu. Bu bir hata durumudur (06 §2).");
            }

            await pacer
                .WaitAfterStepAsync(_state.Clock.ElapsedGameTimeMs, cancellationToken)
                .ConfigureAwait(false);
        }

        return BuildOutcome(events);
    }

    /// <summary>
    /// Terminal durumu ve event akisindan turetilen ozeti cikarir. Tum sayilar
    /// MOTORUN eventlerinden gelir; hicbiri yeniden hesaplanmaz ve hicbiri
    /// istemciden gelmez (09: "client'in gonderdigi skor dikkate alinmaz").
    /// </summary>
    private SessionOutcome BuildOutcome(List<MatchEvent> events)
    {
        var completed = _state.Phase == MatchPhase.Completed;
        var homePossessions = 0;
        var awayPossessions = 0;
        string? abortReason = null;

        foreach (var matchEvent in events)
        {
            switch (matchEvent.Type)
            {
                case MatchEventType.PossessionStarted:
                    if (matchEvent.PayloadAs<PossessionStartedPayload>().Offense == TeamSide.Home)
                    {
                        homePossessions += 1;
                    }
                    else
                    {
                        awayPossessions += 1;
                    }

                    break;

                case MatchEventType.MatchAborted:
                    abortReason = matchEvent.PayloadAs<MatchAbortedPayload>().Reason;
                    break;
            }
        }

        return new SessionOutcome(
            _state,
            [.. events],
            completed ? MatchStatus.Completed : MatchStatus.Aborted,
            _state.HomeScore,
            _state.AwayScore,
            homePossessions,
            awayPossessions,
            _state.Clock.ElapsedGameTimeMs,
            _state.Clock.Period,
            abortReason);
    }

    /// <summary>
    /// Oturumu kapatir.
    ///
    /// <para><b>NEDEN KILIT DISARILMIYOR?</b> Ilk yazimda
    /// <c>_gate.Dispose()</c> cagriliyordu ve bu bir <b>yarisma kosulu</b>
    /// yaratti: canli mac biterken yurutucu oturumu depodan cikarip
    /// dispose ediyor, ayni anda yeniden baglanan bir istemci
    /// <c>CaptureForAsync</c> icinde kilidi tutmus oluyor. <c>Release</c>
    /// cagrisi <c>ObjectDisposedException</c> atiyor ve kullanici 500
    /// aliyordu. M7 API testlerinde 4 kosudan 1'i bu yuzden kiriliydi.</para>
    ///
    /// <para><b>COZUM YETERLI MI?</b> <c>SemaphoreSlim</c> yalniz
    /// <c>AvailableWaitHandle</c> okundugunda yonetilemez bir kaynak tutar.
    /// Bu kod o ozelligi KULLANMAZ; dolayisiyla dispose etmek hicbir seyi
    /// serbest birakmaz, yalnizca yarisma yaratir. Kapatma, oturumu
    /// depodan cikarmak ve <c>_disposed</c> isaretini set etmektir; yarim
    /// kalmis bir cagri normal sekilde tamamlanir.</para>
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>Sayim sifiri olan Guid'i reddetmek icin kucuk yardimci.</summary>
internal static class MatchIdGuard
{
    public static void Require(Guid matchId)
    {
        if (matchId == Guid.Empty)
        {
            throw new ArgumentException("MatchId bos olamaz.", nameof(matchId));
        }
    }
}
