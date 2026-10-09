using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Cookie authentication for the storefront.
/// </summary>
public static class ShopperAuthentication
{
    /// <summary>
    /// Authentication cookie name.
    /// </summary>
    public const string CookieName = ".AdventureWorks.Auth";

    /// <summary>
    /// Registers the shopper cookie.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddShopperAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<ShopperEndedAccess>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.LoginPath = "/user/login";
                options.LogoutPath = "/user/logout";
                options.ReturnUrlParameter = "returnUrl";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.Events.OnSigningIn = context =>
                {
                    context.HttpContext.RequestServices.GetRequiredService<ShopperEndedAccess>().Forget(context.Principal);
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = async context =>
                {
                    var ended = context.HttpContext.RequestServices.GetRequiredService<ShopperEndedAccess>();
                    if (ended.Consume(context.Principal) || !ShopperSession.HasFreshAccessToken(context.Principal))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                            .ConfigureAwait(false);
                    }
                };
            });
        services.AddTransient<ShopperBearerHandler>();
        services.AddHttpClient(EShopStorefrontClients.Basket).AddHttpMessageHandler<ShopperBearerHandler>();
        services.AddHttpClient(EShopStorefrontClients.Ordering).AddHttpMessageHandler<ShopperBearerHandler>();
        services.AddHttpClient(EShopStorefrontClients.Identity).AddHttpMessageHandler<ShopperBearerHandler>();
        services.AddAuthorization();
        return services;
    }
}
