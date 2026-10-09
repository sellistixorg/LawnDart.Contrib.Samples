using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Display of Shopper Account Profile read model.
/// </summary>
public sealed class ShopperAccountProfileClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Reads one shopper account profile.
    /// </summary>
    /// <param name="accountId">Account id.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The profile, or null when the account is unknown, the identity client has no base address,
    /// or the call fails.
    /// </returns>
    /// <remarks>
    /// GET /views/shopper-account-profile on the identity HTTP client.
    /// The address is <c>EShopStorefrontLinks.IdentityUrl</c>.
    /// A refused connection, an HTTP error, unreadable JSON, or a timeout returns null.
    /// Cancellation of <paramref name="cancellationToken"/> still throws.
    /// </remarks>
    public async Task<ShopperAccountProfileView?> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Identity);
        if (client.BaseAddress is null)
            return null;

        try
        {
            using var response = await client.GetAsync(
                $"{ViewEndpoints.ShopperAccountProfile}?accountId={accountId:D}",
                cancellationToken).ConfigureAwait(false);

            ShopperSessionEndedException.ThrowIfEnded(response);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShopperAccountProfileView>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
