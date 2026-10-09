using System.Net;
using System.Net.Http.Json;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Calls Identity Sign Out Shopper.
/// </summary>
public sealed class ShopperSignOutClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Posts Sign Out Shopper for <paramref name="accountId"/>.
    /// </summary>
    /// <param name="accountId">Account ending the session.</param>
    /// <param name="cancellationToken">Token used to cancel the post.</param>
    /// <returns>A task that completes when the call finishes or fails.</returns>
    /// <remarks>
    /// POST /api/identity/sign-out-shopper on the identity HTTP client.
    /// A refused connection, a 422, or an unsupported address is ignored here.
    /// The storefront still clears its cookie.
    /// Cancellation of <paramref name="cancellationToken"/> still throws.
    /// </remarks>
    public async Task SignOutAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Identity);
        if (client.BaseAddress is null)
            return;

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/identity/sign-out-shopper",
                new
                {
                    Id = Guid.NewGuid(),
                    AccountId = accountId
                },
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.UnprocessableEntity)
                return;

            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }
}
