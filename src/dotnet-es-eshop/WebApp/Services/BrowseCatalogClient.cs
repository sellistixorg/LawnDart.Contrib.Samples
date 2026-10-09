using System.Net.Http.Json;
using System.Text;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Shopper catalog list with brand/type facets and filters (WebApp Catalog page / GET items + facets).
/// </summary>
public sealed class BrowseCatalogClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Reads one page of catalog items and the facet counts for the active filters.
    /// </summary>
    /// <param name="pageIndex">Current page index.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="brandIds">Active brand filters.</param>
    /// <param name="typeIds">Active type filters.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The page. An empty page when the catalog client has no base address.</returns>
    /// <remarks>
    /// GET /views/browse-catalog on the catalog HTTP client.
    /// The page catches a refused connection, unreadable JSON, and a timeout.
    /// Cancellation of <paramref name="cancellationToken"/> still throws.
    /// This client keeps no catalog page.
    /// </remarks>
    public async Task<BrowseCatalogView> GetAsync(
        int pageIndex,
        int pageSize,
        IReadOnlyList<Guid>? brandIds,
        IReadOnlyList<Guid>? typeIds,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Catalog);
        if (client.BaseAddress is null)
            return Empty(pageIndex, pageSize, brandIds, typeIds);

        using var response = await client.GetAsync(
            BuildBrowseUri(pageIndex, pageSize, brandIds, typeIds),
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BrowseCatalogView>(cancellationToken).ConfigureAwait(false)
            ?? Empty(pageIndex, pageSize, brandIds, typeIds);
    }

    /// <summary>
    /// Reads every catalog brand and type with its display name.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The name list. An empty list when the catalog client has no base address.</returns>
    /// <remarks>
    /// GET /views/catalog-brands-and-types on the catalog HTTP client.
    /// This client keeps no name list.
    /// </remarks>
    public async Task<CatalogBrandsAndTypesView> GetNamesAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Catalog);
        if (client.BaseAddress is null)
            return new CatalogBrandsAndTypesView();

        using var response = await client.GetAsync(ViewEndpoints.CatalogBrandsAndTypes, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogBrandsAndTypesView>(cancellationToken)
                .ConfigureAwait(false)
            ?? new CatalogBrandsAndTypesView();
    }

    private static string BuildBrowseUri(
        int pageIndex,
        int pageSize,
        IReadOnlyList<Guid>? brandIds,
        IReadOnlyList<Guid>? typeIds)
    {
        var query = new StringBuilder();
        query.Append(ViewEndpoints.BrowseCatalog);
        query.Append("?pageIndex=");
        query.Append(pageIndex);
        query.Append("&pageSize=");
        query.Append(pageSize);
        AppendIds(query, "brand", brandIds);
        AppendIds(query, "type", typeIds);
        return query.ToString();
    }

    private static void AppendIds(StringBuilder query, string name, IReadOnlyList<Guid>? ids)
    {
        if (ids is null)
            return;

        foreach (var id in ids)
        {
            if (id == Guid.Empty)
                continue;

            query.Append('&');
            query.Append(name);
            query.Append('=');
            query.Append(Uri.EscapeDataString(id.ToString("D")));
        }
    }

    private static BrowseCatalogView Empty(
        int pageIndex,
        int pageSize,
        IReadOnlyList<Guid>? brandIds,
        IReadOnlyList<Guid>? typeIds) =>
        new()
        {
            Items = [],
            TotalItems = 0,
            PageIndex = pageIndex,
            PageSize = pageSize,
            BrandFacets = [],
            TypeFacets = [],
            SelectedBrandIds = brandIds is null ? [] : [.. brandIds],
            SelectedTypeIds = typeIds is null ? [] : [.. typeIds]
        };
}
