using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Registers shopper access tokens on Identity.
/// </summary>
public static class ShopperAccessTokenRegistration
{
    /// <summary>
    /// Registers the signing key, the issuer, the JWKS document, and local bearer validation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <remarks>
    /// Call this after <see cref="ShopperLoginStoreRegistration.AddShopperLoginStore{TBuilder}"/>.
    /// <see cref="ShopperLoginStoreRegistration.InitializeShopperLoginStoreAsync"/> creates the key row.
    /// Basket and Ordering validate the same tokens through <c>Identity:Url</c>.
    /// </remarks>
    public static IServiceCollection AddShopperAccessTokens(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ShopperSigningKeyRing>();
        services.AddSingleton<IShopperAccessTokenIssuer, ShopperAccessTokenIssuer>();
        services.AddShopperBearerAccess(new ShopperValidationKeys());
        return services;
    }

    /// <summary>
    /// Maps the public JWKS document and the discovery document.
    /// The discovery document <c>jwks_uri</c> points at the JWKS path on this host.
    /// </summary>
    /// <param name="app">The Identity host.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapShopperJwks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet(ShopperAccessTokens.JwksPath, async (ShopperSigningKeyRing keys, CancellationToken cancellationToken) =>
        {
            var stored = await keys.GetAsync(cancellationToken).ConfigureAwait(false);
            return Results.Text(ShopperAccessTokens.WritePublicJwks(stored), "application/json");
        });
        app.MapGet(ShopperAccessTokens.OpenIdConfigurationPath, (HttpRequest request) =>
        {
            var jwks = $"{request.Scheme}://{request.Host}{ShopperAccessTokens.JwksPath}";
            return Results.Json(new
            {
                issuer = ShopperAccessTokens.Issuer,
                jwks_uri = jwks
            });
        });
        return app;
    }
}
