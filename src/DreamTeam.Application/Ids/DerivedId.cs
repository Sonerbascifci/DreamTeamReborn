using System.Security.Cryptography;
using System.Text;

namespace DreamTeam.Application.Ids;

/// <summary>
/// M7: <b>kimlik turetme</b>. Uygulama kimlikleri GIRDIDEN turetilir,
/// <c>Guid.NewGuid()</c>'den degil.
///
/// <para><b>Neden?</b> Uc somut neden, ucu da bu projede olcum yapti:</para>
/// <list type="number">
///   <item><description><b>Tekrar uretilebilirlik.</b> Ayni girdi ayni
///   kimligi verir. Test, "bu setup'i yeniden kur" dediginde ayni maci
///   kurar; <c>Guid.NewGuid</c> her seferinde farkli bir kimlik verirdi.</description></item>
///   <item><description><b>Idempotency.</b> 07 §5 "ayni CommandId yeniden
///   gelirse ayni sonuc dondurulur" dediginde bu, istemcinin
///   <c>CommandId</c>'sidir. Sunucu tarafinda uretilen her kimlik icin de
///   ayni ilke gecerli: ayni istek iki kez gelirse iki farkli satir
///   olusmaz.</description></item>
///   <item><description><b>Izlenebilirlik.</b> Kimlik girdiden turetildigi
///   icin "bu mac nereden geldi" sorusu cevaplanabilir. Rastgele bir
///   kimlikte cevap yoktur.</description></item>
/// </list>
///
/// <para><b>BILINCLI BIR SINIR: cakisma.</b> SHA-256'in 128 bitine
/// kirpmak, pratikte carpisma olasiligini ihmal edilebilir kilar ama
/// <i>matematiksel olarak sifir degildir</i>. Bu bir garanti degil, bir
/// mühendislik tercihidir. Gercek bir carpisma bir hata olarak
/// gorunur: <c>CreatePlayer</c> ikinci yazimi reddeder.</para>
///
/// <para><b>Ayirici.</b> <c>parts</c> arasina <c>'|'</c> konur. Bu olmadan
/// ("ab", "c") ile ("a", "bc") ayni girdi olurdu.</para>
///
/// <para><b>Alan adina gore on ek.</b> <paramref name="kind"/> girdiye dahil
/// DEGILDIR; ayirici olarak eklenir. Ayni metinler icin "player" ve "match"
/// kimlikleri carpisirsa hata ayiklaması imkansiz bir durum olurdu.</para>
/// </summary>
public static class DerivedId
{
    /// <summary>
    /// Girdiden deterministik bir <see cref="Guid"/> uretir. Ayni girdi her
    /// zaman ayni sonucu verir; surec, makine veya kultur fark etmez.
    /// </summary>
    public static Guid From(string kind, params string[] parts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(parts);

        var builder = new StringBuilder();

        builder.Append(kind).Append('|');

        foreach (var part in parts)
        {
            builder.Append(part).Append('|');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));

        return new Guid(hash.AsSpan(0, 16));
    }
}
