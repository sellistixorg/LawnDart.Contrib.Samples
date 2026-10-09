using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;

/// <summary>
/// Adds the sign-in response body to POST /api/identity/sign-in-shopper.
/// </summary>
internal sealed class SignInShopperModule : IEShopSliceModule
{
    /// <summary>
    /// Registers the per-request receipt and the response writer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.TryAddScoped<SignInShopperReceipt>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, SignInShopperResponseStartup>());
    }

    private sealed class SignInShopperResponseStartup : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (!ShopperSignInHttp.IsSignIn(context.Request))
                    {
                        await nextMiddleware(context).ConfigureAwait(false);
                        return;
                    }

                    var receipt = context.RequestServices.GetRequiredService<SignInShopperReceipt>();
                    await ShopperSignInHttp.InvokeAsync(context, receipt, nextMiddleware).ConfigureAwait(false);
                });
                next(app);
            };
        }
    }
}
