using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Partitioning;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;

/// <summary>
/// Shopper order list / history (GET api/orders).
/// </summary>
public static class OrdersByShopperEndpoint
{
    /// <summary>
    /// GET /views/orders-by-shopper.
    /// </summary>
    public const string Route = "/views/orders-by-shopper";

    /// <summary>
    /// Orders list page.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// Query <c>buyerId</c> is required. A missing or empty buyer id returns 400.
    /// An unknown buyer returns an empty list.
    /// Rows are that shopper's orders, newest first.
    /// Each row has the order id, the date it was placed, the status, and the total.
    /// </remarks>
    public static IEndpointRouteBuilder MapOrdersByShopper(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid? buyerId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (buyerId is not Guid id || id == Guid.Empty)
            return Results.BadRequest();

        var state = await ReadStateAsync(httpContext.RequestServices, cancellationToken).ConfigureAwait(false);
        var view = OrdersByShopperProjection.ForBuyer(state, id);
        return Results.Json(view, ProjectionViewJson.Write);
    }

    private static async Task<OrdersByShopperState?> ReadStateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var contextName = EShopLawnDartHost.ContextName;
        var instanceId = InstanceId(services, contextName);
        var json = LiveJson(services, contextName, instanceId);
        if (json is null)
        {
            var views = services.GetRequiredKeyedService<IViewStore>(contextName);
            json = await views.GetViewAsync(
                OrdersByShopperProjection.StorageKey,
                instanceId,
                cancellationToken).ConfigureAwait(false);
        }

        return json is null
            ? null
            : JsonSerializer.Deserialize<OrdersByShopperState>(json, ProjectionViewJson.Read);
    }

    private static string? LiveJson(IServiceProvider services, string contextName, string instanceId)
    {
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(OrdersByShopperProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            return liveJson;
        }

        return null;
    }

    private static string InstanceId(IServiceProvider services, string contextName)
    {
        var partitioning = services.GetKeyedService<IPartitioningService>(contextName);
        return partitioning is null
            ? ProjectionInstanceIds.UnpartitionedBase
            : ProjectionInstanceIds.ForUnpartitioned(partitioning);
    }
}
