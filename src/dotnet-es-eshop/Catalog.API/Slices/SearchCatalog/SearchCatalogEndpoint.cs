using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Partitioning;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.SearchCatalog;

/// <summary>
/// Plain name-prefix search: matches catalog items whose names start with the query text (upstream Catalog.API name filter).
/// </summary>
public static class SearchCatalogEndpoint
{
    /// <summary>
    /// GET /views/search-catalog.
    /// </summary>
    public const string Route = "/views/search-catalog";

    /// <summary>
    /// Shopper search results display.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// Query <c>text</c> is the search text. Query <c>pageIndex</c> starts at 0. Query <c>pageSize</c> defaults to 10 and is capped at 100.
    /// The prefix match is case-sensitive. Removed items never appear. <c>TotalItems</c> counts every match, not only this page.
    /// </remarks>
    public static IEndpointRouteBuilder MapSearchCatalog(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        string? text,
        int? pageIndex,
        int? pageSize,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(httpContext.RequestServices, cancellationToken).ConfigureAwait(false);
        var view = SearchCatalogProjection.Match(
            state,
            text,
            pageIndex ?? SearchCatalogProjection.DefaultPageIndex,
            pageSize ?? SearchCatalogProjection.DefaultPageSize);
        return Results.Json(view, ProjectionViewJson.Write);
    }

    private static async Task<SearchCatalogState?> ReadStateAsync(
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
                SearchCatalogProjection.StorageKey,
                instanceId,
                cancellationToken).ConfigureAwait(false);
        }

        return json is null
            ? null
            : JsonSerializer.Deserialize<SearchCatalogState>(json, ProjectionViewJson.Read);
    }

    private static string? LiveJson(IServiceProvider services, string contextName, string instanceId)
    {
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(SearchCatalogProjection.StorageKey, instanceId, out var liveJson, out _))
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
