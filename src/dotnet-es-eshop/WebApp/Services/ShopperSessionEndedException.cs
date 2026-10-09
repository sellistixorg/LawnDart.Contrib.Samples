using System.Net;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// The backend rejected the shopper access token.
/// </summary>
/// <remarks>
/// The cookie is cleared before this exception is thrown. The page sends the shopper to login.
/// </remarks>
public sealed class ShopperSessionEndedException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    public ShopperSessionEndedException()
        : base("Sign in again.")
    {
    }

    /// <summary>
    /// Throws when the backend rejected the access token.
    /// </summary>
    /// <param name="response">The backend response.</param>
    public static void ThrowIfEnded(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new ShopperSessionEndedException();
    }
}
