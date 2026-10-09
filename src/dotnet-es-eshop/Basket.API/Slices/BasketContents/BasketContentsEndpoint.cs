using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;

/// <summary>
/// Shopper read model of current basket lines and totals (gRPC GetBasket / WebApp basket UI).
/// </summary>
public static class BasketContentsEndpoint
{
    /// <summary>
    /// GET /views/basket-contents.
    /// </summary>
    public const string Route = "/views/basket-contents";

    /// <summary>
    /// Shopper basket page display.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// Query <c>buyerId</c> is required. A missing or empty buyer id returns 400.
    /// An unknown buyer returns an empty basket. A basket with no lines returns an empty basket.
    /// Prices are the unit prices captured when the line was added.
    /// </remarks>
    public static IEndpointRouteBuilder MapBasketContents(this IEndpointRouteBuilder endpoints)
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

        var json = await ReadJsonAsync(httpContext.RequestServices, id, cancellationToken).ConfigureAwait(false);
        if (json is null)
            return Results.Json(BasketContentsView.Empty(id), ProjectionViewJson.Write);

        var view = JsonSerializer.Deserialize<BasketContentsView>(json, ProjectionViewJson.Read);
        if (view is null)
            return Results.Json(BasketContentsView.Empty(id), ProjectionViewJson.Write);

        view.BuyerId = id;
        BasketContentsProjection.ApplyTotals(view);
        return Results.Json(view, ProjectionViewJson.Write);
    }

    private static async Task<string?> ReadJsonAsync(
        IServiceProvider services,
        Guid buyerId,
        CancellationToken cancellationToken)
    {
        var instanceId = BasketContentsProjection.InstanceId(buyerId);
        var contextName = EShopLawnDartHost.ContextName;
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(BasketContentsProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            return liveJson;
        }

        var views = services.GetRequiredKeyedService<IViewStore>(contextName);
        return await views.GetViewAsync(
            BasketContentsProjection.StorageKey,
            instanceId,
            cancellationToken).ConfigureAwait(false);
    }
}
