using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.SearchCatalog;

/// <summary>
/// Maps the search catalog view route.
/// </summary>
internal sealed class SearchCatalogModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/search-catalog.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapSearchCatalog();
}
