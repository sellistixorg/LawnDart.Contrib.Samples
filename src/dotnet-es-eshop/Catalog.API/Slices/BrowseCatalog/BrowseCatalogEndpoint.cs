using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Partitioning;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog;

/// <summary>
/// Shopper catalog list with brand/type facets and filters (WebApp Catalog page / GET items + facets).
/// </summary>
public static class BrowseCatalogEndpoint
{
    /// <summary>
    /// GET /views/browse-catalog.
    /// </summary>
    public const string Route = "/views/browse-catalog";

    /// <summary>
    /// Shopper catalog browsing screen.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// Query <c>pageIndex</c> starts at 0. Query <c>pageSize</c> defaults to 10 and is capped at 100.
    /// Query <c>brand</c> and <c>type</c> are optional Guid filters. Several values of one filter match any of those ids.
    /// A brand count uses the type filter. A type count uses the brand filter.
    /// Removed items never appear. Rows are ordered by name, then item id.
    /// </remarks>
    public static IEndpointRouteBuilder MapBrowseCatalog(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        int? pageIndex,
        int? pageSize,
        Guid[]? brand,
        Guid[]? type,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(httpContext.RequestServices, cancellationToken).ConfigureAwait(false);
        var view = BrowseCatalogProjection.Page(
            state,
            brand,
            type,
            pageIndex ?? BrowseCatalogProjection.DefaultPageIndex,
            pageSize ?? BrowseCatalogProjection.DefaultPageSize);
        return Results.Json(view, ProjectionViewJson.Write);
    }

    private static async Task<BrowseCatalogState?> ReadStateAsync(
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
                BrowseCatalogProjection.StorageKey,
                instanceId,
                cancellationToken).ConfigureAwait(false);
        }

        return json is null
            ? null
            : JsonSerializer.Deserialize<BrowseCatalogState>(json, ProjectionViewJson.Read);
    }

    private static string? LiveJson(IServiceProvider services, string contextName, string instanceId)
    {
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(BrowseCatalogProjection.StorageKey, instanceId, out var liveJson, out _))
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
