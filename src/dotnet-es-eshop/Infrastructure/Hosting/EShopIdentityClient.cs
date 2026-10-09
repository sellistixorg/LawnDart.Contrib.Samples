using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Identity base URL from AppHost <c>Identity__Url</c> (<c>Identity:Url</c>).
/// </summary>
public sealed class EShopIdentityClientOptions
{
    public const string SectionName = "Identity";

    public const string HttpClientName = "identity";

    public string? Url { get; set; }
}

/// <summary>
/// Binds <see cref="EShopIdentityClientOptions"/> and, when the URL is absolute, an HTTP client.
/// </summary>
public static class EShopIdentityClientExtensions
{
    public static IServiceCollection AddEShopIdentityUrl(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EShopIdentityClientOptions>(configuration.GetSection(EShopIdentityClientOptions.SectionName));

        var url = configuration[$"{EShopIdentityClientOptions.SectionName}:Url"];
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var identityUri))
        {
            services.AddHttpClient(EShopIdentityClientOptions.HttpClientName, client => client.BaseAddress = identityUri);
        }

        return services;
    }
}
