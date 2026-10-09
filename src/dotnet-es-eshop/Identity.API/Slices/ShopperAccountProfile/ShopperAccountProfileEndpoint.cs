using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Identity.API.Slices.ShopperAccountProfile;

/// <summary>
/// Display of Shopper Account Profile read model.
/// </summary>
public static class ShopperAccountProfileEndpoint
{
    /// <summary>
    /// GET /views/shopper-account-profile.
    /// </summary>
    public const string Route = "/views/shopper-account-profile";

    /// <summary>
    /// Display of Shopper Account Profile read model.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// The account id is a query parameter. An unknown account is not found.
    /// The host bearer gate requires a shopper access token and rejects a subject that is not this account.
    /// </remarks>
    public static IEndpointRouteBuilder MapShopperAccountProfile(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid? accountId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (accountId is not Guid id || id == Guid.Empty)
            return Results.NotFound();

        var instanceId = ShopperAccountProfileProjection.InstanceId(id);
        var services = httpContext.RequestServices;
        var runners = services.GetKeyedService<IProjectionRunnerManager>(EShopLawnDartHost.ContextName);
        if (runners is not null
            && runners.TryGetView(ShopperAccountProfileProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            return ToResult(liveJson);
        }

        var views = services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName);
        var stored = await views.GetViewAsync(
            ShopperAccountProfileProjection.StorageKey,
            instanceId,
            cancellationToken).ConfigureAwait(false);
        return stored is null ? Results.NotFound() : ToResult(stored);
    }

    private static IResult ToResult(string viewJson)
    {
        var view = JsonSerializer.Deserialize<ShopperAccountProfileView>(viewJson, ProjectionViewJson.Read);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
