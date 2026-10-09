using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Storefront links from AppHost <c>IdentityUrl</c> and <c>CallBackUrl</c>.
/// </summary>
public sealed class EShopStorefrontLinks
{
    public string? IdentityUrl { get; init; }

    public string? CallBackUrl { get; init; }

    public static EShopStorefrontLinks Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new EShopStorefrontLinks
        {
            IdentityUrl = configuration["IdentityUrl"],
            CallBackUrl = configuration["CallBackUrl"]
        };
    }
}

/// <summary>
/// Aspire service-discovery clients for the storefront, plus the identity HTTP client when <c>IdentityUrl</c> is absolute.
/// WebApp maps Razor pages. <see cref="MapEShopStorefront"/> still maps a JSON document at <c>/</c>.
/// </summary>
public static class EShopStorefrontClients
{
    public const string Catalog = "catalog";

    public const string Basket = "basket";

    public const string Ordering = "ordering";

    public const string Identity = "identity";

    public static readonly Uri CatalogAddress = new("https+http://catalog-api");

    public static readonly Uri BasketAddress = new("https+http://basket-api");

    public static readonly Uri OrderingAddress = new("https+http://ordering-api");

    public static IServiceCollection AddEShopStorefront(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddClient(services, Catalog, CatalogAddress);
        AddClient(services, Basket, BasketAddress);
        AddClient(services, Ordering, OrderingAddress);

        var links = EShopStorefrontLinks.Read(configuration);
        if (!string.IsNullOrWhiteSpace(links.IdentityUrl)
            && Uri.TryCreate(links.IdentityUrl, UriKind.Absolute, out var identityUri))
        {
            services.AddHttpClient(Identity, client => client.BaseAddress = identityUri);
        }

        services.AddSingleton(links);
        return services;
    }

    public static WebApplication MapEShopStorefront(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/", (EShopStorefrontLinks links) => Results.Ok(new
        {
            status = "scaffold",
            service = "webapp",
            clients = new
            {
                catalog = CatalogAddress.OriginalString,
                basket = BasketAddress.OriginalString,
                ordering = OrderingAddress.OriginalString
            },
            identityUrl = links.IdentityUrl,
            callBackUrl = links.CallBackUrl
        }));

        return app;
    }

    private static void AddClient(IServiceCollection services, string name, Uri address)
    {
        services.AddHttpClient(name, client => client.BaseAddress = address);
    }
}
