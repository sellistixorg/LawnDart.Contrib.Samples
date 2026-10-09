using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Callback URLs AppHost injects into Identity.
/// </summary>
public sealed class IdentityCallbackOptions
{
    public string? BasketApiClient { get; init; }

    public string? OrderingApiClient { get; init; }

    public string? WebhooksApiClient { get; init; }

    public string? WebhooksWebClient { get; init; }

    public string? WebAppClient { get; init; }

    public static IdentityCallbackOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new IdentityCallbackOptions
        {
            BasketApiClient = configuration["BasketApiClient"],
            OrderingApiClient = configuration["OrderingApiClient"],
            WebhooksApiClient = configuration["WebhooksApiClient"],
            WebhooksWebClient = configuration["WebhooksWebClient"],
            WebAppClient = configuration["WebAppClient"]
        };
    }
}

/// <summary>
/// Obvious stand-in token. It does not authenticate a caller.
/// Identity slices replace this endpoint.
/// </summary>
public sealed record IdentityDemoToken(string TokenType, string AccessToken, int ExpiresIn)
{
    public static IdentityDemoToken Create() => new("demo", "demo-token", 3600);
}

/// <summary>
/// Demo auth surface so other hosts can call Identity before account slices exist.
/// </summary>
public static class IdentityDemoAuthExtensions
{
    public static IServiceCollection AddIdentityDemoAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(IdentityCallbackOptions.Read(configuration));
        return services;
    }

    public static WebApplication MapIdentityDemoAuth(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/", () => Results.Ok(new
        {
            status = "scaffold",
            service = "identity-api",
            demoAuth = true
        }));

        app.MapGet("/identity/callbacks", (IdentityCallbackOptions callbacks) => Results.Ok(callbacks));

        app.MapPost("/identity/demo-token", () => Results.Ok(IdentityDemoToken.Create()));

        return app;
    }
}
