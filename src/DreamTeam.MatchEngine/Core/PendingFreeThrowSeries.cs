namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Devam eden serbest atış serisi (M3).
///
/// 06 §91: "FT arası miss için canlı rebound yok. Son FT miss'i yalnız top canlıysa
/// rebound üretir." Bu yüzden seri boyunca yalnız <see cref="FinalShotIsLive"/>
/// atışında canlı rebound yolu açılır ve bu, and-one durumudur (tek atış).
///
/// <see cref="IsLast"/> serinin bittiğini bildirir; seriyi kapatan adım bu değeri
/// okuyup topun gitişini belirler.
/// </summary>
public sealed record PendingFreeThrowSeries
{
    public required long FTSeriesId { get; init; }

    /// <summary>Atışları kullanan oyuncu. Tüm seride aynıdır.</summary>
    public required Guid ShooterId { get; init; }

    /// <summary>0 tabanlı atış indeksi.</summary>
    public required int Index { get; init; }

    /// <summary>Serideki toplam atış sayısı (1, 2 veya 3).</summary>
    public required int Count { get; init; }

    /// <summary>Son atış canlı mı? Yalnız and-one serisinde (Count == 1) doğrudur.</summary>
    public required bool FinalShotIsLive { get; init; }

    /// <summary>Seri, hücumun elindeki oyuncunun hücumu mu? Hangi taraf top devralacak?</summary>
    public required TeamSide Offense { get; init; }

    /// <summary>Seri, hangi possession içinde oynanıyor? OREB sonrası da korunur.</summary>
    public required int PossessionId { get; init; }

    /// <summary>Şut sonucu isabetse puan zaten işlendi mi? Serbest atış puanı ayrıdır.</summary>
    public required bool FieldGoalWasCounted { get; init; }

    public bool IsLast => Index >= Count - 1;

    public PendingFreeThrowSeries WithIndex(int index) => this with { Index = index };
}
