using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrderSummary;

/// <summary>
/// Order detail projection for shopper and operator.
/// </summary>
public static class OrderSummaryEndpoint
{
    /// <summary>
    /// GET /views/order-summary/{orderNumber}.
    /// </summary>
    public const string Route = "/views/order-summary/{orderNumber:guid}";

    /// <summary>
    /// Order detail display.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// Query <c>buyerId</c> is the signed-in shopper subject. It must match the order buyer id.
    /// An unknown order, an empty order id, a missing buyer id, and an order for a different buyer return not found.
    /// The response fields are order id, buyer id, current status, placed at, lines, ship-to address, status text, and order total.
    /// </remarks>
    public static IEndpointRouteBuilder MapOrderSummary(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid orderNumber,
        Guid? buyerId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (orderNumber == Guid.Empty || buyerId is not Guid subject || subject == Guid.Empty)
            return Results.NotFound();

        var json = await ReadJsonAsync(httpContext.RequestServices, orderNumber, cancellationToken)
            .ConfigureAwait(false);
        if (json is null)
            return Results.NotFound();

        var view = JsonSerializer.Deserialize<OrderSummaryView>(json, ProjectionViewJson.Read);
        if (view is null || view.OrderId == Guid.Empty || view.BuyerId != subject)
            return Results.NotFound();

        return Results.Ok(view);
    }

    private static async Task<string?> ReadJsonAsync(
        IServiceProvider services,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var instanceId = OrderSummaryProjection.InstanceId(orderId);
        var contextName = EShopLawnDartHost.ContextName;
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(OrderSummaryProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            return liveJson;
        }

        var views = services.GetRequiredKeyedService<IViewStore>(contextName);
        return await views.GetViewAsync(
            OrderSummaryProjection.StorageKey,
            instanceId,
            cancellationToken).ConfigureAwait(false);
    }
}
