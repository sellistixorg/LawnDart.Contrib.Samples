using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Identity.API.Slices.ShopperAccountProfile;

/// <summary>
/// Maps the shopper account profile view route.
/// </summary>
internal sealed class ShopperAccountProfileModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/shopper-account-profile.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapShopperAccountProfile();
}
