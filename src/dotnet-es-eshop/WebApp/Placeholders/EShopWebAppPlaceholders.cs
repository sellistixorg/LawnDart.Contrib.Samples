using LawnDart.Samples.EShop.WebApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.WebApp.Placeholders;

public static class EShopWebAppPlaceholders
{
    public static IServiceCollection AddEShopWebAppPlaceholders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ExampleStore>();
        services.AddSingleton<ShopperAccountProfileClient>();
        services.AddSingleton<ViewCatalogItemClient>();
        services.AddSingleton<SearchCatalogClient>();
        services.AddSingleton<BrowseCatalogClient>();
        services.AddScoped<ShopperSession>();
        services.AddSingleton<ShopperSignInClient>();
        services.AddSingleton<ShopperSignOutClient>();
        services.AddScoped<IBasketContents, BasketContentsClient>();
        services.AddScoped<ICheckout, CheckoutClient>();
        services.AddScoped<IShopperOrders, OrdersByShopperClient>();
        services.AddSingleton<IProductImageUrlProvider, ExampleProductImageUrlProvider>();
        return services;
    }
}
