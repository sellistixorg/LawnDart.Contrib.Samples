using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Adds the shopper access token to Basket, Ordering, and Identity calls.
/// </summary>
/// <remarks>
/// The token comes from the authentication cookie. It is not read from JavaScript.
/// A circuit call with no HTTP context uses <see cref="ShopperCallPrincipal"/> when one was pushed.
/// This handler does not log the token. An expired token is left off the request.
/// A 401 still marks the token. When the HTTP context is present, the cookie is signed out.
/// </remarks>
public sealed class ShopperBearerHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ShopperEndedAccess _ended;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="httpContextAccessor">Current request. The cookie principal is read when the call is sent.</param>
    /// <param name="ended">Tokens a backend rejected. The next request signs that cookie out.</param>
    public ShopperBearerHandler(IHttpContextAccessor httpContextAccessor, ShopperEndedAccess ended)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(ended);
        _httpContextAccessor = httpContextAccessor;
        _ended = ended;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var http = _httpContextAccessor.HttpContext;
        var principal = FreshPrincipal(http?.User);
        if (ShopperSession.HasFreshAccessToken(principal))
        {
            var token = principal!.FindFirst(ShopperSession.AccessTokenClaim)!.Value;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return SendAndEndSessionAsync(request, http, principal, cancellationToken);
    }

    private static ClaimsPrincipal? FreshPrincipal(ClaimsPrincipal? httpUser)
    {
        if (ShopperSession.HasFreshAccessToken(httpUser))
            return httpUser;

        var ambient = ShopperCallPrincipal.Current;
        return ShopperSession.HasFreshAccessToken(ambient) ? ambient : httpUser;
    }

    private async Task<HttpResponseMessage> SendAndEndSessionAsync(
        HttpRequestMessage request,
        HttpContext? http,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        _ended.Mark(principal);
        if (http is null)
            return response;

        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        if (!http.Response.HasStarted)
        {
            try
            {
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Streaming already sent the headers. The page shows a login link.
            }
        }

        return response;
    }
}

/// <summary>
/// Access tokens a backend rejected. The next request signs that cookie out.
/// </summary>
public sealed class ShopperEndedAccess
{
    private readonly ConcurrentDictionary<string, byte> _tokens = new(StringComparer.Ordinal);

    /// <summary>
    /// Remembers the access token on <paramref name="principal"/>.
    /// </summary>
    /// <param name="principal">The cookie principal that the backend rejected.</param>
    public void Mark(ClaimsPrincipal? principal)
    {
        var token = Token(principal);
        if (token is not null)
            _tokens[token] = 0;
    }

    /// <summary>
    /// Takes the mark so a later sign-in with the same token is accepted.
    /// </summary>
    /// <param name="principal">The cookie principal for this request.</param>
    /// <returns>True when this token was rejected and the cookie should be cleared.</returns>
    public bool Consume(ClaimsPrincipal? principal)
    {
        var token = Token(principal);
        return token is not null && _tokens.TryRemove(token, out _);
    }

    /// <summary>
    /// Drops a mark when the shopper signs in again.
    /// </summary>
    /// <param name="principal">The principal being signed in.</param>
    public void Forget(ClaimsPrincipal? principal)
    {
        var token = Token(principal);
        if (token is not null)
            _tokens.TryRemove(token, out _);
    }

    private static string? Token(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ShopperSession.AccessTokenClaim)?.Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
