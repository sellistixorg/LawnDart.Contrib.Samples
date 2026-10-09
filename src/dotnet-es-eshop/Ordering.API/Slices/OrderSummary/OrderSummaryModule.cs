using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrderSummary;

/// <summary>
/// Maps the order summary view route.
/// </summary>
internal sealed class OrderSummaryModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/order-summary/{orderNumber}.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapOrderSummary();
}
