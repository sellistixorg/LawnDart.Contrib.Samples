using System.Net;
using System.Net.Http.Json;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Shopper item detail including picture (GET item + pic).
/// </summary>
public sealed class ViewCatalogItemClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Reads one catalog item.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The item, or null when the id is empty, the item is unknown, or the catalog client has no base address.
    /// </returns>
    /// <remarks>
    /// GET /views/view-catalog-item/{itemId} on the catalog HTTP client.
    /// The page catches a refused connection, unreadable JSON, and a timeout.
    /// Cancellation of <paramref name="cancellationToken"/> still throws.
    /// </remarks>
    public async Task<ViewCatalogItemView?> GetAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
            return null;

        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Catalog);
        if (client.BaseAddress is null)
            return null;

        using var response = await client.GetAsync(
            $"{ViewEndpoints.ViewCatalogItem}/{itemId:D}",
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ViewCatalogItemView>(cancellationToken)
            .ConfigureAwait(false);
    }
}
