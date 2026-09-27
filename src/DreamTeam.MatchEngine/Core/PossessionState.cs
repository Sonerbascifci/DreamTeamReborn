namespace DreamTeam.MatchEngine.Core;

/// <summary>Possession'ın kapanma nedeni. 06 §4 tablosunun M2 alt kümesi.</summary>
public enum PossessionEndReason
{
    Scored,
    DefensiveRebound,
    Turnover,

    /// <summary>
    /// M3: bonuslu savunma non-shooting faulu. Serbest atislar oynandiktan
    /// sonra top rakibe gider. 06 §4 tablosunda bu satir yoktu; ayrim
    /// turnover'i da degil, D40 profilinin sonucunu adlandirir.
    /// </summary>
    BonusFreeThrows,

    /// <summary>Periyot saati dolduğunda yarım kalan hücum.</summary>
    PeriodExpired,
}

/// <summary>
/// Bir hücumun kimliği ve bağlamı. 06 §4'teki operasyonel tanıma göre kimlik,
/// hücum takımı kontrolü kazanmadan başlar ve topu rakibe devredene kadar korunur.
///
/// <b>Offensive rebound kimliği değiştirmez.</b> OREB sonrası aynı <see cref="PossessionId"/>
/// devam eder; DREB, turnover ve sayı yeni kimlik başlatır. Bu, 08'in T05 testinin
/// dayandığı kuraldır.
/// </summary>
public sealed record PossessionState
{
    /// <summary>Maç içi benzersiz ve asla yeniden kullanılmayan artan kimlik.</summary>
    public required int PossessionId { get; init; }

    public required TeamSide Offense { get; init; }

    /// <summary>Bu possession içinde oynanan aksiyon sayısı.</summary>
    public required int ActionCount { get; init; }

    /// <summary>Bu possession içinde atılan şut denemesi sayısı.</summary>
    public required int ShotCount { get; init; }
}
