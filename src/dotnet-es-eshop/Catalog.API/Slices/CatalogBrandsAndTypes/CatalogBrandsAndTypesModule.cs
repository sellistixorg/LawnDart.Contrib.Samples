using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;

/// <summary>
/// Maps the catalog brands and types view route.
/// </summary>
internal sealed class CatalogBrandsAndTypesModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/catalog-brands-and-types.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapCatalogBrandsAndTypesView();
}
