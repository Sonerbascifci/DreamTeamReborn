using System.Collections.Immutable;

namespace DreamTeam.MatchEngine.Config;

/// <summary>
/// Savunma policy katsayıları (D57).
///
/// Her kanal **tek bir** sonucu etkiler; 05 §3 ve 05 §7, bir etkinin iki kez
/// sayılmasını yasaklar. Kanal eşlemesi:
/// <list type="bullet">
///   <item><description>Kalite baskısı -> <c>ShotQuality</c> (puan)</description></item>
///   <item><description>Blok -> blok çekilişi</description></item>
///   <item><description>Baskı -> top kaybı çekilişi</description></item>
///   <item><description>Disiplin -> faul çekilişi</description></item>
/// </list>
///
/// Dört sayı da <b>kalibre edilmemiştir</b>. 05 §6'daki eski yüzdeler ("Drop için
/// midrange +6% …") bilinçli olarak kopyalanmamıştır: 05 onları onaylı matris
/// olmadığını, yüzde/yüzde-puan ayrımının tanımsız olduğunu ve Drop etkisinin
/// personel derinliğine bağlı bulunduğunu açıkça söyler.
/// </summary>
public sealed record DefenseModel
{
    public required double BlockBase { get; init; }

    /// <summary>Savunmacının iç savunma composite'inin blok çekilişine etkisi.</summary>
    public required double BlockFromInteriorDefense { get; init; }

    /// <summary>Savunma baskısının top kaybı çekilişine eklediği taban pay.</summary>
    public required double PressureBase { get; init; }

    /// <summary>Savunmacının perimeter savunma composite'inin baskı payı.</summary>
    public required double PressureFromPerimeterDefense { get; init; }

    /// <summary>Savunma disiplininin faul çekilişine eklediği taban pay (0-1).</summary>
    public required double FoulFromAggression { get; init; }

    public static DefenseModel Baseline { get; } = new()
    {
        BlockBase = 0.04,
        BlockFromInteriorDefense = 0.04,
        PressureBase = 0.0,
        PressureFromPerimeterDefense = 0.02,
        FoulFromAggression = 0.0,
    };
}
