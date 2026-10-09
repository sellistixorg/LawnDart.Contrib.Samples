using System.Net.Http.Json;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Plain name-prefix search: matches catalog items whose names start with the query text (upstream Catalog.API name filter).
/// </summary>
public sealed class SearchCatalogClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Reads one page of catalog items whose names start with <paramref name="text"/>.
    /// </summary>
    /// <param name="text">Search text (matched as a name prefix).</param>
    /// <param name="pageIndex">The index of the page of results to return.</param>
    /// <param name="pageSize">Number of items to return in a single page of results.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The page of matches. An empty page when the catalog client has no base address.</returns>
    /// <remarks>
    /// GET /views/search-catalog on the catalog HTTP client.
    /// The page catches a refused connection, unreadable JSON, and a timeout.
    /// Cancellation of <paramref name="cancellationToken"/> still throws.
    /// This client keeps no search result.
    /// </remarks>
    public async Task<SearchCatalogView> GetAsync(
        string? text,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = text ?? "";
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Catalog);
        if (client.BaseAddress is null)
            return Empty(query);

        using var response = await client.GetAsync(
            $"{ViewEndpoints.SearchCatalog}?text={Uri.EscapeDataString(query)}&pageIndex={pageIndex}&pageSize={pageSize}",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchCatalogView>(cancellationToken).ConfigureAwait(false)
            ?? Empty(query);
    }

    private static SearchCatalogView Empty(string query) =>
        new()
        {
            Query = query,
            Items = [],
            TotalItems = 0
        };
}
