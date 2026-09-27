namespace DreamTeam.MatchEngine.Core;

/// <summary>
/// Bir oyuncunun maç içi durumu. 04_DOMAIN_AND_DATA_MODEL.md §11'deki
/// <c>PlayerMatchState</c> kaydının M4'e kadar olan alt kümesi.
///
/// <para><b>Enerji neden <c>double</c>?</b> Drain bir canlı saniye başına
/// ~0.02 puan. Eğer enerji <c>int</c> olsaydı ve her dilimden sonra yuvarlansaydı,
/// dilim başına düşen 0.02'lik kayıp <c>Math.Round</c> ile tekrar 0'a döner ve
/// enerji <b>hiç düşmezdi</b>. Bu hata uygulamada gerçekten oluştu: 8 saniyelik
/// dilimlerde <c>Math.Round(99.98)</c> = 100. Kesirli kısım korunmalıdır; kayıp
/// ancak süre biriktikçe görünür olur.</para>
///
/// <para><b>Enerji, Stamina DEĞİLDİR.</b> 04 §33 bunu açıkça ayırır:
/// <c>Stamina</c> dayanıklılık <b>kapasitesidir</b> ve statik snapshot'ta durur;
/// <c>Energy</c> maç içindeki kalan enerjidir ve burada ilerler.</para>
///
/// <para><b>GameForm burada YOKTUR</b> (D60). 05 §13 GameForm'un ilk kalibrasyonda
/// kapalı kalmasını önerir; 0 olan bir alan eklemek 05 §3'te yasaklanan "etkisiz
/// mekanizmayı gizleme" hatasıdır.</para>
///
/// <para><b>Kişisel fauller burada YOKTUR.</b> M3'ten beri <c>FoulCounters.Personal</c>
/// onları tutuyor; ikinci bir kaynak çift sayım üretirdi.</para>
/// </summary>
public sealed record PlayerMatchState
{
    public required Guid PlayerId { get; init; }

    /// <summary>
    /// Kalan enerji, <c>[0,100]</c>. Her güncellemede kırpılır. Yuvarlama yalnız
    /// <b>görüntüleme</b> ve performans sorgusunda yapılır, hesapta değil.
    /// </summary>
    public required double Energy { get; init; }

    /// <summary>
    /// Sahada geçen canlı süre, <b>saniye cinsinden kesirli değer</b>. Yalnız
    /// sahadayken artar; periyot arası toparlanma bu sayacı <b>artırmaz</b> (T12c).
    ///
    /// <para>Neden <c>double</c>? Uzun bir tip, tam sayıya <b>yuvarlamadan</b>
    /// biriktirilemez. M4'te tam sayı kullanıldığında her 1500 ms'lik şut uçuşu
    /// <c>1500/1000 = 1</c> saniye olarak yazıldı ve her uçuşta 0.5 saniye
    /// kayboldu; toplam oynama süresi <c>5 × elapsed</c> eşitliğinden sapıyordu
    /// (T12c).</para>
    /// </summary>
    public required double SecondsOnCourt { get; init; }
}
