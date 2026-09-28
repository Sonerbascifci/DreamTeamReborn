using System.Text.Json;
using System.Text.Json.Serialization;
using DreamTeam.Domain.Players;

namespace DreamTeam.Infrastructure.Persistence;

/// <summary>
/// M7: <c>PlayerRatings</c> &lt;-&gt; JSONB.
///
/// <para><b>Neden JSONB ve 18 sutun degil?</b> Gercek maliyeti olcuyorum:
/// 18 sutun, 18 migration adimi ve her yeni alanda 3 dosya degisikligi
/// demek. M7'de rating'ler DEGISMEZ (degisim M11+ kapsaminda, Q15/Q17
/// acik). Tek sutun yeterli. Kararin bedeli kayitlidir: alan sayisi
/// buyurse JSONB okunabilirligini kaybeder ve tabloya donusme zamani
/// gelir.</para>
///
/// <para><b>ALAN SIRASI SABITTIR.</b> JSONB anahtar sirasi KORUMAZ, ama biz
/// yazarken <see cref="FieldOrder"/> sirasiyla yaziyoruz ve okurken ayni
/// sirayi bekliyoruz. Boylece satirin metni sabit kalir: "bu iki satir
/// ayni mi" sorusu ham metinle cevaplanabilir.</para>
///
/// <para><b>BILINEN ALAN ZORUNLUDUR.</b> Eksik alan sessizce varsayilan
/// degere dusmez; <c>Deserialize</c> hatasi verir. Nedeni: sessizce 0'a
/// dusen bir rating, donen bir maci sessizce bozar ve hata yalnizca
/// "macta kimse oynamiyor" gibi gorunur.</para>
///
/// <para><b>ALAN SAYISI UYUMLULUGU.</b> Motor 18 alan istiyorsa JSON'da
/// da 18 alan olmali. Fazla alan yazilirsa ileride okunmayan veri
/// birikir; az alan yazilirsa sessiz veri kaybi olur. Test her ikisini de
/// reddeder.</para>
/// </summary>
public static class RatingsCodec
{
    /// <summary>
    /// Alan sirasi TEK KAYNAKTIR. Siralama burada degistirilirse yazma ve
    /// okuma birlikte degisir, eski satirlar okunmaya devam eder.
    /// </summary>
    public static readonly IReadOnlyList<string> FieldOrder =
    [
        nameof(PlayerRatings.Speed),
        nameof(PlayerRatings.Strength),
        nameof(PlayerRatings.Vertical),
        nameof(PlayerRatings.Stamina),
        nameof(PlayerRatings.Inside),
        nameof(PlayerRatings.MidRange),
        nameof(PlayerRatings.ThreePoint),
        nameof(PlayerRatings.FreeThrow),
        nameof(PlayerRatings.BallHandling),
        nameof(PlayerRatings.Passing),
        nameof(PlayerRatings.OffBall),
        nameof(PlayerRatings.PostOffense),
        nameof(PlayerRatings.PerimeterDefense),
        nameof(PlayerRatings.InteriorDefense),
        nameof(PlayerRatings.Steal),
        nameof(PlayerRatings.Block),
        nameof(PlayerRatings.Rebounding),
        nameof(PlayerRatings.BasketballIQ),
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Encode(PlayerRatings ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        var ordered = new List<KeyValuePair<string, object>>(FieldOrder.Count)
        {
            new(nameof(PlayerRatings.Speed), ratings.Speed),
            new(nameof(PlayerRatings.Strength), ratings.Strength),
            new(nameof(PlayerRatings.Vertical), ratings.Vertical),
            new(nameof(PlayerRatings.Stamina), ratings.Stamina),
            new(nameof(PlayerRatings.Inside), ratings.Inside),
            new(nameof(PlayerRatings.MidRange), ratings.MidRange),
            new(nameof(PlayerRatings.ThreePoint), ratings.ThreePoint),
            new(nameof(PlayerRatings.FreeThrow), ratings.FreeThrow),
            new(nameof(PlayerRatings.BallHandling), ratings.BallHandling),
            new(nameof(PlayerRatings.Passing), ratings.Passing),
            new(nameof(PlayerRatings.OffBall), ratings.OffBall),
            new(nameof(PlayerRatings.PostOffense), ratings.PostOffense),
            new(nameof(PlayerRatings.PerimeterDefense), ratings.PerimeterDefense),
            new(nameof(PlayerRatings.InteriorDefense), ratings.InteriorDefense),
            new(nameof(PlayerRatings.Steal), ratings.Steal),
            new(nameof(PlayerRatings.Block), ratings.Block),
            new(nameof(PlayerRatings.Rebounding), ratings.Rebounding),
            new(nameof(PlayerRatings.BasketballIQ), ratings.BasketballIQ),
        };

        // Sifir uzunlukta nesne yazmaz; JSONB bir nesne bekliyor.
        return JsonSerializer.Serialize(ordered, Options);
    }

    public static PlayerRatings Decode(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            var pairs = JsonSerializer.Deserialize<List<KeyValuePair<string, JsonElement>>>(json, Options)
                ?? throw new InvalidOperationException("Ratings JSON'u bos.");

            var actual = pairs.Select(p => p.Key).ToList();

            var missing = FieldOrder.Except(actual).ToList();

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Ratings eksik alan: {string.Join(", ", missing)}. "
                    + "Eksik alan sessizce varsayilana dusmez.");
            }

            var extra = actual.Except(FieldOrder).ToList();

            if (extra.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Ratings fazladan alan: {string.Join(", ", extra)}. "
                    + "Bilinmeyen alan yazilmis; motor onu okumaz, veri kaybolur.");
            }

            return new PlayerRatings
            {
                Speed = Read(pairs, nameof(PlayerRatings.Speed)),
                Strength = Read(pairs, nameof(PlayerRatings.Strength)),
                Vertical = Read(pairs, nameof(PlayerRatings.Vertical)),
                Stamina = Read(pairs, nameof(PlayerRatings.Stamina)),
                Inside = Read(pairs, nameof(PlayerRatings.Inside)),
                MidRange = Read(pairs, nameof(PlayerRatings.MidRange)),
                ThreePoint = Read(pairs, nameof(PlayerRatings.ThreePoint)),
                FreeThrow = Read(pairs, nameof(PlayerRatings.FreeThrow)),
                BallHandling = Read(pairs, nameof(PlayerRatings.BallHandling)),
                Passing = Read(pairs, nameof(PlayerRatings.Passing)),
                OffBall = Read(pairs, nameof(PlayerRatings.OffBall)),
                PostOffense = Read(pairs, nameof(PlayerRatings.PostOffense)),
                PerimeterDefense = Read(pairs, nameof(PlayerRatings.PerimeterDefense)),
                InteriorDefense = Read(pairs, nameof(PlayerRatings.InteriorDefense)),
                Steal = Read(pairs, nameof(PlayerRatings.Steal)),
                Block = Read(pairs, nameof(PlayerRatings.Block)),
                Rebounding = Read(pairs, nameof(PlayerRatings.Rebounding)),
                BasketballIQ = Read(pairs, nameof(PlayerRatings.BasketballIQ)),
            };
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException(
                $"Ratings JSON'u okunamadi: {error.Message}", error);
        }
    }

    private static int Read(List<KeyValuePair<string, JsonElement>> pairs, string name) =>
        pairs.First(p => p.Key == name).Value.GetInt32();
}
