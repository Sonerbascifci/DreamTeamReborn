using System.Security.Claims;
using DreamTeam.Application.Ports;

namespace DreamTeam.Api.Auth;

/// <summary>
/// M7: HTTP/SignalR baglamindan kullanici kimligi.
///
/// <para><b>INFRASTRUCTURE'DA DEGIL, BURADA.</b> Altyapi bir HTTP projesi
/// degildir; <c>IHttpContextAccessor</c> FrameworkReference gerektirir ve
/// katman sinirini ters cevirirdi. Kimligin <i>nereden</i> okunmasi bir
/// HTTP adaptoru meselesidir.</para>
///
/// <para><b>KIMLIK IKI KAYNAKTAN GELEBILIR:</b> HTTP istegi (JWT) ya da
/// SignalR baglantisi (握手 sirasi da JWT ile kimlik dogrulanir). Ikisi de ayni
/// <c>sub</c> claim'ini doldurur.</para>
///
/// <para><b>KIMLIK YOKSA ISTISNA.</b> Sessizce bos bir kimlik uretmek her
/// yetki denetimini bypass ederdi. Endpoint'ler <c>RequireAuthorization()</c>
/// ile korunur; kimlik olmayan bir istek zaten middleware'da elenir, bu
/// tip ise ikinci savunma hattidir.</para>
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        _accessor = accessor;
    }

    public Guid UserId
    {
        get
        {
            var principal = _accessor.HttpContext?.User;
            var subject = Read(principal);

            return Guid.TryParse(subject, out var userId) && userId != Guid.Empty
                ? userId
                : throw Unauthorized();
        }
    }

    public bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var subject = Read(_accessor.HttpContext?.User);

        if (subject is null || !Guid.TryParse(subject, out userId) || userId == Guid.Empty)
        {
            userId = Guid.Empty;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Once <c>sub</c>, sonra <c>nameidentifier</c>. <c>MapInboundClaims =
    /// false</c> ayarindan dolayi <c>sub</c> oldugu gibi kalir; eski
    /// adlandirma yalniz geriye donuk uyum icin.
    /// </summary>
    private static string? Read(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst("sub")?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static UnauthorizedAccessException Unauthorized() => new(
        "Baglamda gecerli bir kullanici kimligi yok. Endpoint [Authorize] ile "
        + "korunmali ve istek gecerli bir JWT tasimali.");
}
