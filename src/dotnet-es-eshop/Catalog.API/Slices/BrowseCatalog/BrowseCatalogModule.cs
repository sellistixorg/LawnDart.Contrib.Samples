using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog;

/// <summary>
/// Maps the browse catalog view route.
/// </summary>
internal sealed class BrowseCatalogModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/browse-catalog.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapBrowseCatalog();
}
