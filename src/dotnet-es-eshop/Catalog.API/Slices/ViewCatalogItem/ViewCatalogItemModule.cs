using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.ViewCatalogItem;

/// <summary>
/// Maps the catalog item view route.
/// </summary>
internal sealed class ViewCatalogItemModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/view-catalog-item/{itemId}.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapViewCatalogItem();
}
