using Microsoft.AspNetCore.Components;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Local return path for the storefront sign-in page.
/// </summary>
public static class ShopperReturnUrl
{
    /// <summary>
    /// Sign-in route with a local return path.
    /// </summary>
    /// <param name="navigation">The current page.</param>
    /// <returns><c>/user/login?returnUrl=</c> for the current path.</returns>
    public static string Login(NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return Login(navigation.ToBaseRelativePath(navigation.Uri));
    }

    /// <summary>
    /// Sign-in route with <paramref name="returnUrl"/> when that path is local.
    /// </summary>
    /// <param name="returnUrl">Requested return path.</param>
    /// <returns><c>/user/login?returnUrl=</c> using <see cref="Local"/>.</returns>
    public static string Login(string? returnUrl)
    {
        var local = Local(returnUrl);
        return "/user/login?returnUrl=" + Uri.EscapeDataString(local);
    }

    /// <summary>
    /// A path on this site. An absolute URL, a protocol-relative URL, or a blank value becomes <c>/</c>.
    /// </summary>
    /// <param name="returnUrl">Requested return path.</param>
    /// <returns>The local path, or <c>/</c> when the value is not local.</returns>
    public static string Local(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        var candidate = returnUrl.Trim();
        if (!candidate.StartsWith('/'))
            candidate = "/" + candidate;

        if (candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.StartsWith("/\\", StringComparison.Ordinal)
            || candidate.Contains('\\', StringComparison.Ordinal)
            || candidate.Contains(':')
            || candidate.Contains('\0')
            || candidate.Any(char.IsControl))
        {
            return "/";
        }

        if (candidate.StartsWith("/user/login", StringComparison.OrdinalIgnoreCase))
            return "/";

        return candidate;
    }
}
