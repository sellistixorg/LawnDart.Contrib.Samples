using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Partitioning;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;

/// <summary>
/// GET /views/catalog-brands-and-types. Returns every catalog brand and type with its display name.
/// </summary>
public static class CatalogBrandsAndTypesEndpoint
{
    /// <summary>
    /// Route for the brand and type name list.
    /// </summary>
    public const string Route = "/views/catalog-brands-and-types";

    /// <summary>
    /// Maps <see cref="Route"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapCatalogBrandsAndTypesView(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, (HttpContext http, CancellationToken cancellationToken) =>
            ReadAsync(http.RequestServices, cancellationToken));
        return endpoints;
    }

    /// <summary>
    /// Loads the projected name list. An empty list is returned when no brand or type has been defined yet.
    /// </summary>
    /// <param name="services">Request services.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The brand and type name list.</returns>
    public static async Task<IResult> ReadAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var contextName = EShopLawnDartHost.ContextName;
        var instanceId = InstanceId(services, contextName);
        var json = LiveJson(services, contextName, instanceId);
        if (json is null)
        {
            var store = services.GetRequiredKeyedService<IViewStore>(contextName);
            json = await store.GetViewAsync(
                CatalogBrandsAndTypesProjection.StorageKey,
                instanceId,
                cancellationToken).ConfigureAwait(false);
        }

        var view = json is null
            ? new CatalogBrandsAndTypesView()
            : JsonSerializer.Deserialize<CatalogBrandsAndTypesView>(json, ProjectionViewJson.Read)
                ?? new CatalogBrandsAndTypesView();

        return Results.Json(view, ProjectionViewJson.Write);
    }

    private static string? LiveJson(IServiceProvider services, string contextName, string instanceId)
    {
        var runner = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runner is not null
            && runner.TryGetView(CatalogBrandsAndTypesProjection.StorageKey, instanceId, out var liveJson, out _))
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
