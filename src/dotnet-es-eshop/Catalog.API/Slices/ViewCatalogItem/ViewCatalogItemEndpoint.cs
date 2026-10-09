using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Partitioning;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.ViewCatalogItem;

/// <summary>
/// Shopper item detail including picture (GET item + pic).
/// </summary>
public static class ViewCatalogItemEndpoint
{
    /// <summary>
    /// GET /views/view-catalog-item/{itemId}.
    /// </summary>
    public const string Route = "/views/view-catalog-item/{itemId:guid}";

    /// <summary>
    /// Shopper item detail page.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// An unknown id and a removed item are not found. The response has the nine model fields. Brand and type display names are read from Catalog Brands And Types when that view has the id.
    /// </remarks>
    public static IEndpointRouteBuilder MapViewCatalogItem(this IEndpointRouteBuilder endpoints)
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

        var services = httpContext.RequestServices;
        var json = await ReadItemJsonAsync(services, itemId, cancellationToken).ConfigureAwait(false);
        if (json is null)
            return Results.NotFound();

        var row = JsonSerializer.Deserialize<ViewCatalogItemRow>(json, ProjectionViewJson.Read);
        if (row is null || row.Removed || row.ItemId == Guid.Empty)
            return Results.NotFound();

        ApplyNames(row, await ReadNamesAsync(services, cancellationToken).ConfigureAwait(false));
        return Results.Ok(ToView(row));
    }

    private static ViewCatalogItemView ToView(ViewCatalogItemRow row) =>
        new()
        {
            ItemId = row.ItemId,
            Name = row.Name,
            Description = row.Description,
            Price = row.Price,
            BrandName = row.BrandName,
            TypeName = row.TypeName,
            AvailableStock = row.AvailableStock,
            PictureUrl = row.PictureUrl,
            IsAvailable = row.IsAvailable
        };

    private static async Task<string?> ReadItemJsonAsync(
        IServiceProvider services,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var instanceId = ViewCatalogItemProjection.InstanceId(itemId);
        var contextName = EShopLawnDartHost.ContextName;
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(ViewCatalogItemProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            return liveJson;
        }

        var views = services.GetRequiredKeyedService<IViewStore>(contextName);
        return await views.GetViewAsync(
            ViewCatalogItemProjection.StorageKey,
            instanceId,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CatalogBrandsAndTypesView?> ReadNamesAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var contextName = EShopLawnDartHost.ContextName;
        var instanceId = BrandsInstanceId(services, contextName);
        string? json = null;
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(CatalogBrandsAndTypesProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            json = liveJson;
        }

        if (json is null)
        {
            var views = services.GetRequiredKeyedService<IViewStore>(contextName);
            json = await views.GetViewAsync(
                CatalogBrandsAndTypesProjection.StorageKey,
                instanceId,
                cancellationToken).ConfigureAwait(false);
        }

        return json is null
            ? null
            : JsonSerializer.Deserialize<CatalogBrandsAndTypesView>(json, ProjectionViewJson.Read);
    }

    private static void ApplyNames(ViewCatalogItemRow view, CatalogBrandsAndTypesView? names)
    {
        if (names is null)
            return;

        foreach (var brand in names.Brands)
        {
            if (brand.BrandId != view.BrandId)
                continue;

            view.BrandName = brand.Name;
            break;
        }

        foreach (var type in names.Types)
        {
            if (type.TypeId != view.TypeId)
                continue;

            view.TypeName = type.Name;
            break;
        }
    }

    private static string BrandsInstanceId(IServiceProvider services, string contextName)
    {
        var partitioning = services.GetKeyedService<IPartitioningService>(contextName);
        return partitioning is null
            ? ProjectionInstanceIds.UnpartitionedBase
            : ProjectionInstanceIds.ForUnpartitioned(partitioning);
    }
}
