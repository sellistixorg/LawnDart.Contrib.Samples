using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogItemDetailAdmin;

/// <summary>
/// Admin read model for a single catalog item after add/update/restock.
/// </summary>
public static class CatalogItemDetailAdminEndpoint
{
    /// <summary>
    /// GET /views/catalog-item-detail-admin/{itemId}.
    /// </summary>
    public const string Route = "/views/catalog-item-detail-admin/{itemId:guid}";

    /// <summary>
    /// Admin read model for a single catalog item after add/update/restock.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// An unknown id returns not found. A removed item is returned with Removed set. The response has the model fields plus Removed.
    /// </remarks>
    public static IEndpointRouteBuilder MapCatalogItemDetailAdmin(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid itemId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty)
            return Results.NotFound();

        var json = await ReadItemJsonAsync(httpContext.RequestServices, itemId, cancellationToken)
            .ConfigureAwait(false);
        if (json is null)
            return Results.NotFound();

        var view = JsonSerializer.Deserialize<CatalogItemDetailAdminView>(json, ProjectionViewJson.Read);
        if (view is null || view.ItemId == Guid.Empty)
            return Results.NotFound();

        return Results.Ok(view);
    }

    private static async Task<string?> ReadItemJsonAsync(
        IServiceProvider services,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var instanceId = CatalogItemDetailAdminProjection.InstanceId(itemId);
        var contextName = EShopLawnDartHost.ContextName;
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(CatalogItemDetailAdminProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            return liveJson;
        }

        var views = services.GetRequiredKeyedService<IViewStore>(contextName);
        return await views.GetViewAsync(
            CatalogItemDetailAdminProjection.StorageKey,
            instanceId,
            cancellationToken).ConfigureAwait(false);
    }
}
