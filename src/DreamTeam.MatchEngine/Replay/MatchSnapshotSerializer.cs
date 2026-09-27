using System.Text.Json;
using System.Text.Json.Serialization;

namespace DreamTeam.MatchEngine.Replay;

/// <summary>
/// Snapshot serilestirici. <b>Sifir NuGet paketi</b> (D88: bu oturumda olculdu,
/// <c>System.Text.Json</c> <c>net10.0</c> sinif kutuphanesinde paket gerektirmeden
/// calisiyor).
///
/// <para><b>Enum'lar isim tabanli</b> (D88). Varsayilan <c>System.Text.Json</c>
/// enum'lari <b>sayisal</b> yazar; bu oturumda olculdu
/// (<c>InsidePost</c> -&gt; <c>3</c>). Enum ordering'i bir kez bile degisse
/// kayitli snapshot <b>sessizce</b> bozulur. Isim tabanli serilestirmede ayni
/// sorun olmaz, cunki isim degismedikce okunabilir kalir.</para>
///
/// <para><b>Alan sirasi kanoniktir</b> (D39 usulu): ayarlar degistiginde bile ayni
/// durum ayni bayti uretir.</para>
///
/// <para><b>Bu tip bir MOTOR yuzeyi degil, replay aletidir.</b> Canli API
/// serilestirmesi M7'nin konusudur; burada tek sey var: ayni durumdan ayni
/// bayt, ve ayni bayttan ayni durum.</para>
/// </summary>
public static class MatchSnapshotSerializer
{
    /// <summary>Kanonik ayarlar. Her cagri ayni ayarlari kullanir.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            // Sekme ve bosluk yazmaz: bayt duyarliligi ve kucuk dosya.
            WriteIndented = false,

            // Alan adlari C# PascalCase olarak kalir; ayri bir politika
            // getirmek 07 §1'deki "frontend ile birlikte kilitlenir" maddesine
            // dokunurdu ve M7'ye birakilir.
            PropertyNamingPolicy = null,

            // Kaybolmus deger: enum tanimli degilse sessizce 0'a dusmesin.
            NumberHandling = JsonNumberHandling.Strict,

            Converters =
            {
                new JsonStringEnumConverter(),
                new ImmutableArrayByteConverter(),
            },
        };

        return options;
    }

    public static string ToJson(MatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static MatchSnapshot FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<MatchSnapshot>(json, Options)
            ?? throw new JsonException("Snapshot JSON'u null dondu.");
    }

    /// <summary>Serialize + deserialize. T16 testlerinin ana yardimcisi.</summary>
    public static MatchSnapshot RoundTrip(MatchSnapshot snapshot) => FromJson(ToJson(snapshot));
}
