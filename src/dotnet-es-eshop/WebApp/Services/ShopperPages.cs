using Microsoft.AspNetCore.Components;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Sends a signed-out shopper to the login page.
/// </summary>
public static class ShopperPages
{
    /// <summary>
    /// Redirects to <c>/user/login</c> when <paramref name="shopper"/> has no account id.
    /// </summary>
    /// <param name="shopper">The current cookie.</param>
    /// <param name="navigation">The current page.</param>
    /// <returns>True when a redirect was requested.</returns>
    public static bool RedirectIfSignedOut(ShopperSession shopper, NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(shopper);
        ArgumentNullException.ThrowIfNull(navigation);
        if (shopper.IsSignedIn)
            return false;

        try
        {
            navigation.NavigateTo(ShopperReturnUrl.Login(navigation), forceLoad: true);
        }
        catch (InvalidOperationException)
        {
            // Streaming already started the response. Callers show a login link.
        }

        return true;
    }

    /// <summary>
    /// Sends the shopper to login after the backend rejected the access token.
    /// </summary>
    /// <param name="navigation">The current page.</param>
    /// <param name="httpContext">The current request. When its response has started, the caller shows a login link.</param>
    public static void SendToLogin(NavigationManager navigation, HttpContext? httpContext)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        if (httpContext?.Response.HasStarted == true)
            return;

        try
        {
            navigation.NavigateTo(ShopperReturnUrl.Login(navigation), forceLoad: true);
        }
        catch (InvalidOperationException)
        {
            // Streaming already started the response. Callers show a login link.
        }
    }
}
