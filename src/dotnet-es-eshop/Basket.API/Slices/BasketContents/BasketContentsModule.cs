using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;

/// <summary>
/// Maps the basket contents view route.
/// </summary>
internal sealed class BasketContentsModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/basket-contents.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapBasketContents();
}
