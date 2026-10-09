using System.Security.Claims;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Shopper principal for a storefront call that has no HTTP context.
/// </summary>
/// <remarks>
/// The orders page sets this around a view read on the circuit.
/// <see cref="ShopperBearerHandler"/> reads it when the cookie is not on the current HTTP context.
/// The value stays on the async flow that pushed it.
/// </remarks>
public static class ShopperCallPrincipal
{
    private static readonly AsyncLocal<ClaimsPrincipal?> CurrentPrincipal = new();

    /// <summary>
    /// Principal for the current async flow. Null when nobody pushed one.
    /// </summary>
    public static ClaimsPrincipal? Current => CurrentPrincipal.Value;

    /// <summary>
    /// Sets <paramref name="principal"/> until the returned value is disposed.
    /// </summary>
    /// <param name="principal">Shopper principal. Null clears it for this flow.</param>
    /// <returns>A scope that restores the previous principal.</returns>
    public static IDisposable Push(ClaimsPrincipal? principal)
    {
        var prior = CurrentPrincipal.Value;
        CurrentPrincipal.Value = principal;
        return new Pop(prior);
    }

    private sealed class Pop(ClaimsPrincipal? prior) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            CurrentPrincipal.Value = prior;
        }
    }
}
