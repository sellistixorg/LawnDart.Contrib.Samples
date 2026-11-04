using HarborStreet.Catalog.Slice.Domain;
using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HarborStreet.Catalog.Slice.Hosting;

/// <summary>
/// Hosting skeleton for the Catalog slice — wiring only, no domain rules.
/// </summary>
public static class CatalogComposition
{
    public const string ContextName = "default";
    public const string DemoTenantId = "harbor";

    /// <summary>
    /// Registers LawnDart InMemory for the Catalog slice.
    /// </summary>
    public static IServiceCollection AddCatalogSlice(
        this IServiceCollection services,
        string dataDirectory,
        Action<ILoggingBuilder>? configureLogging = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddLogging(b =>
        {
            b.AddConsole().SetMinimumLevel(LogLevel.Warning);
            configureLogging?.Invoke(b);
        });

// start snippet register-catalog
        services.AddLawnDart(o =>
        {
            o.RequireTenantId = false;
            o.EnableAuthorization = false;
        });

        services
            .AddBoundedContext(ContextName)
            .UseInMemory()
            .WithEventTypes(CatalogEventTypes.All);
// end snippet register-catalog

        return services;
    }
}
