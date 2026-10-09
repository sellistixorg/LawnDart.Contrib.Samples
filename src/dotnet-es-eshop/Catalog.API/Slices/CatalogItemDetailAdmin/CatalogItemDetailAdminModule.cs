using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogItemDetailAdmin;

/// <summary>
/// Maps the catalog item detail admin view route.
/// </summary>
internal sealed class CatalogItemDetailAdminModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/catalog-item-detail-admin/{itemId}.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapCatalogItemDetailAdmin();
}
