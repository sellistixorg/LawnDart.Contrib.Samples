using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;

/// <summary>
/// Maps the orders by shopper view route.
/// </summary>
internal sealed class OrdersByShopperModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/orders-by-shopper.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapOrdersByShopper();
}
