using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// The signed-in shopper from the authentication cookie.
/// </summary>
public sealed class ShopperSession(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// Claim that carries the shopper account id.
    /// </summary>
    public const string AccountIdClaim = "sub";

    /// <summary>
    /// Claim that carries the shopper access token. The cookie is encrypted. Do not log this claim.
    /// </summary>
    public const string AccessTokenClaim = "access_token";

    /// <summary>
    /// Claim that carries the access token expiry as unix seconds.
    /// </summary>
    public const string AccessTokenExpiresClaim = "access_token_exp";

    /// <summary>
    /// True when the cookie has a shopper account id and an access token that is still valid.
    /// </summary>
    public bool IsSignedIn => ActivePrincipal() is not null;

    /// <summary>
    /// Shopper account id from the cookie. Null when nobody is signed in.
    /// </summary>
    /// <remarks>
    /// A circuit call with no HTTP context uses <see cref="ShopperCallPrincipal"/> when one was pushed.
    /// </remarks>
    public Guid? AccountId => ReadAccountId(ActivePrincipal());

    /// <summary>
    /// Display name from the cookie. Empty when nobody is signed in.
    /// </summary>
    public string DisplayName =>
        ActivePrincipal()?.FindFirst(ClaimTypes.Name)?.Value ?? "";

    /// <summary>
    /// Access token from the cookie. Null when nobody is signed in. Do not log this value.
    /// </summary>
    public string? AccessToken =>
        ActivePrincipal()?.FindFirst(AccessTokenClaim)?.Value;

    /// <summary>
    /// Shopper account id on <paramref name="principal"/>. Null when the token is missing or expired.
    /// </summary>
    /// <param name="principal">Cookie principal or the principal pushed for a circuit call.</param>
    /// <returns>The account id. Null when <paramref name="principal"/> cannot be forwarded.</returns>
    public static Guid? ReadAccountId(ClaimsPrincipal? principal)
    {
        if (!HasFreshAccessToken(principal))
            return null;

        var value = principal!.FindFirst(AccountIdClaim)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }

    /// <summary>
    /// True when <paramref name="principal"/> has an account id and an access token that has not expired.
    /// </summary>
    /// <param name="principal">Cookie principal.</param>
    /// <returns>True when the token can be forwarded.</returns>
    public static bool HasFreshAccessToken(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return false;

        var account = principal.FindFirst(AccountIdClaim)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(account, out var accountId) || accountId == Guid.Empty)
            return false;

        if (string.IsNullOrEmpty(principal.FindFirst(AccessTokenClaim)?.Value))
            return false;

        return TryReadExpiry(principal, out var expires) && expires > DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Builds the cookie principal for a shopper.
    /// </summary>
    /// <param name="accountId">Shopper account id.</param>
    /// <param name="displayName">Name shown in the header.</param>
    /// <param name="accessToken">Access token from Identity. Stored in the encrypted cookie. Do not log it.</param>
    /// <param name="accessTokenExpiresAt">UTC expiry of <paramref name="accessToken"/>.</param>
    /// <returns>The principal. The password is not a claim.</returns>
    public static ClaimsPrincipal CreatePrincipal(
        Guid accountId,
        string displayName,
        string accessToken,
        DateTimeOffset accessTokenExpiresAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        var id = accountId.ToString("D");
        var claims = new List<Claim>
        {
            new(AccountIdClaim, id),
            new(ClaimTypes.NameIdentifier, id),
            new(ClaimTypes.Name, displayName ?? ""),
            new(AccessTokenClaim, accessToken),
            new(AccessTokenExpiresClaim, accessTokenExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static bool TryReadExpiry(ClaimsPrincipal principal, out DateTimeOffset expires)
    {
        expires = default;
        var raw = principal.FindFirst(AccessTokenExpiresClaim)?.Value;
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return false;

        expires = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }

    private ClaimsPrincipal? ActivePrincipal()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (HasFreshAccessToken(httpUser))
            return httpUser;

        var ambient = ShopperCallPrincipal.Current;
        if (HasFreshAccessToken(ambient))
            return ambient;

        return null;
    }
}
