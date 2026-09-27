namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Üç ayrı sayaç. Hepsi integer milisaniye (H02). Motor duvar saati tutmaz ve
/// hiçbir yerde <c>DateTime</c> okumaz.
///
/// <see cref="GameClockMs"/> bu periyotta kalan süredir, <see cref="ShotClockMs"/>
/// bu hücumda kalan süredir; ikisi aynı şey değildir. <see cref="ElapsedGameTimeMs"/>
/// toplam oynanan süredir. Serbest atış, substitution ve inbound gibi dead-ball
/// işlemleri M2'de canlı süre tüketmez.
/// </summary>
public readonly record struct MatchClock
{
    /// <summary>1-based. 1-4 normal periyot; 5+ uzatma (M3).</summary>
    public required int Period { get; init; }

    /// <summary>Bu periyotta kalan süre. Negatif olamaz.</summary>
    public required long GameClockMs { get; init; }

    /// <summary>Bu hücumda kalan süre. Negatif olamaz.</summary>
    public required long ShotClockMs { get; init; }

    /// <summary>Maç boyunca oynanan toplam canlı süre.</summary>
    public required long ElapsedGameTimeMs { get; init; }

    public bool IsGameTimeExhausted => GameClockMs <= 0;

    public bool IsShotClockExhausted => ShotClockMs <= 0;

    /// <summary>
    /// Canlı oyun süresi tükettiğinde üç sayacı da aynı miktarda ilerletir.
    /// Tüketilen süre kalan süreyi aşıyorsa kalanla sınırlanır; sayaç asla negatif olmaz
    /// ve <see cref="ElapsedGameTimeMs"/> daima gerçekten oynanan miktarı artar.
    /// </summary>
    public MatchClock ConsumeLiveTime(long durationMs)
    {
        if (durationMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Canlı süre negatif olamaz.");
        }

        var consumed = Math.Min(durationMs, Math.Max(0, GameClockMs));

        return this with
        {
            GameClockMs = GameClockMs - consumed,
            ShotClockMs = Math.Max(0, ShotClockMs - consumed),
            ElapsedGameTimeMs = ElapsedGameTimeMs + consumed,
        };
    }

    /// <summary>
    /// Yeni periyot başlangıcı. Hücum saati ve oyun saati yenilenir;
    /// <b>toplam oynanan süre korunur</b> — periyot başı toplam süreyi sıfırlamaz.
    /// </summary>
    public MatchClock BeginPeriod(int period, long periodDurationMs, long shotClockMs) => this with
    {
        Period = period,
        GameClockMs = periodDurationMs,
        ShotClockMs = shotClockMs,
    };

    /// <summary>Başlangıç durumu: henüz periyot başlamadı, sayaçlar sıfır.</summary>
    public static MatchClock Initial() => new()
    {
        Period = 0,
        GameClockMs = 0,
        ShotClockMs = 0,
        ElapsedGameTimeMs = 0,
    };
}
