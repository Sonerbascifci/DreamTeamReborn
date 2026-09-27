using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DreamTeam.MatchEngine.Replay;

/// <summary>
/// <c>ImmutableArray&lt;byte&gt;</c> icin hex converter.
///
/// <para><b>Neden gerekli?</b> Bu oturumda olculdu: <c>ImmutableArray&lt;byte&gt;</c>,
/// <c>System.Text.Json</c> ile varsayilan olarak <b>ham sayi dizisi</b> olarak
/// serilestirilir (<c>[1,2,3,4]</c>). RNG state'i 8 bayt oldugu icin bu hem
/// okunmaz hem de gereksiz uzun. Hex (<c>"0102030405060708"</c>) hem kisa hem
/// insan-okunur; ayrica byte degerlerinin kaybolmasi (kayip hassasiyet gibi)
/// bir durum yaratmaz.</para>
///
/// <para><b>Neden ozel converter, ayar degil?</b> Kayitli snapshot'in bayt
/// duyarliligi bir tesaduf olmamalidir. Simplex serialization ayari degistirilse
/// bile bu converter hep uygulanir.</para>
/// </summary>
public sealed class ImmutableArrayByteConverter : JsonConverter<ImmutableArray<byte>>
{
    public override ImmutableArray<byte> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        var text = reader.GetString()
            ?? throw new JsonException("byte[] alani null metin olarak geldi.");

        return [.. Convert.FromHexString(text)];
    }

    public override void Write(
        Utf8JsonWriter writer,
        ImmutableArray<byte> value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(Convert.ToHexString(value.AsSpan()));
}
