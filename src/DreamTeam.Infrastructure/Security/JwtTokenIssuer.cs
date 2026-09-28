using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DreamTeam.Infrastructure.Security;

/// <summary>
/// M7 (D110): JWT imzalama ve dogrulama ayarlari.
///
/// <para><b>ALGORTMA SABIT: HS256.</b> Secenekler basta HMAC-SHA256
/// (paylasilan bir sifre) ve RSA (asimetrik, acma/kapama anahtari ayri)
/// vardi. HS256 secildi cunku sunucu TEK instance (03) ve kimlik dogrulama
/// disinda biri bu jetonu okumuyor. RSA'nin avantaji — iki taraf ayri
/// anahtar tutar — burada kullanilmiyor; tek anahtari iki yerde tutmak
/// (uygulama + calisma ortami) HS256'da tek bir sifre olur.</para>
///
/// <para><b>AYAR ZORUNLULUĞU.</b> Anahtar bos olamaz ve en az 32 bayt
/// olmalidir. Kisa bir anahtar HMAC guvenligini dusurur; derleme zamaninda
/// degil, <b>acilista</b> hata veriyoruz cunku gizli deger hicbir yerde
/// tutulmamalidir (bkz. <c>config/auth/jwt-signing-key.example.txt</c>).</para>
///
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Issuer. Tokenin kim tarafindan uretildigi.</summary>
    public string Issuer { get; set; } = "dreamteam";

    /// <summary>Audience. Tokenin kimin icin gecerli oldugu.</summary>
    public string Audience { get; set; } = "dreamteam-client";

    /// <summary>
    /// HMAC anahtari. <b>Gizli deger.</b> Ortam degiskeninden gelir; dosyaya
    /// yazilmaz. Bos veya kisa ise kurulum hata verir.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Jeton omru. 09 §M7 kabulu: oturum kisa omurlu olmali.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Jwt:Audience zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey zorunludur. Deger ortam degiskeninden gelmelidir; "
                + "dosyaya yazilmaz. Ornek: config/auth/jwt-signing-key.example.txt");
        }

        // HS256 icin 256 bit = 32 bayt. Daha kisa anahtar guvenligi dusurur.
        var bytes = Encoding.UTF8.GetBytes(SigningKey);

        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey en az 32 bayt olmali, {bytes.Length} bayt verildi.");
        }

        if (Lifetime <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Jwt:Lifetime pozitif olmalidir.");
        }
    }
}

/// <summary>
/// M7: jeton uretir. Dogrulama <b>ASP.NET Core JWTBearer</b> middleware'ine
/// birakilir; burada ikinci bir dogrulayici kurmuyoruz.
///
/// <para><b>NEDEN AYRI DOGRULAYICI YOK?</b> iki dogrulayici, iki kural
/// seti demektir. Uretimde calisan orta katmanin bildigiyle, testte
/// dogrulayan seyin AYNI olmasi gerekir. Testler de middleware'i kullanir.</para>
/// </summary>
public sealed class JwtTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _handler = new();

    /// <param name="options">
    /// Dogrudan verilir, <c>IOptions&lt;JwtOptions&gt;</c> DEGIL. Boylece bu
    /// proje framework paketine baglanmaz ve ayarin nereden geldigi (dosya mi,
    /// ortam degiskeni mi) yalniz composition root'ta gorunur.
    /// </param>
    public JwtTokenIssuer(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();
        _options = options;
    }

    public SecurityTokenDescriptor DescriptorFor(Guid userId, string displayName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Kullanici kimligi bos olamaz.", nameof(userId));
        }

        return new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(
            [
                // 07 §5: kimlik BAGLANTIDAN cozulur. Claim adı "sub" (subject)
                // standarttır; ASP.NET Core varsayılan olarak bunu okur.
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
                new Claim("display_name", displayName),
            ]),

            // NotNull uyari: SigningCredentials'i sonra atiyoruz, cunku
            // anahtari burada uretmek icin gereken algoritmayi bilmeliyiz.
            Expires = DateTime.UtcNow.Add(_options.Lifetime),
            NotBefore = DateTime.UtcNow,
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = Credentials(),
        };
    }

    public string Issue(Guid userId, string displayName)
    {
        var descriptor = DescriptorFor(userId, displayName);
        descriptor.SigningCredentials = Credentials();

        return _handler.WriteToken(_handler.CreateToken(descriptor));
    }

    /// <summary>Uretilen jetonun bitis ani. Istemciye bildirilir.</summary>
    public DateTimeOffset ExpiresAtUtc => DateTimeOffset.UtcNow.Add(_options.Lifetime);

    private SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(_options.SigningKey));

    private SigningCredentials Credentials() => new(Key(), SecurityAlgorithms.HmacSha256);

    /// <summary>
    /// Kimlik dogrulama icin gereken parametreler. API katmani bunlari
    /// middleware'e verir. <see cref="SecretKey"/> disariya SIZDIRILMAZ;
    /// yalnizca ayni surec icinde kullanilir.
    /// </summary>
    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer,
        ValidateAudience = true,
        ValidAudience = _options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = Key(),
        ValidateLifetime = true,
        ClockSkew = _options.ClockSkew,
        NameClaimType = ClaimTypes.NameIdentifier,

        // Rol/grup uydurmak: sunucu bu turden bir iddiayi dogrulamis olsa
        // bile istemcinin ekledigi claim'lere guvenilmez. Tek yetki kaynagi
        // veritabanindaki kayit ve oturumun sahibidir (T19).
        RequireSignedTokens = true,
        RequireExpirationTime = true,
    };

    /// <summary>Testler icin: gizli olmayan, sabit bir anahtar uretir.</summary>
    public static string CreateDevelopmentKey()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes);
    }
}
