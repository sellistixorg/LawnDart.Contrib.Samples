using System.Reflection;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Catalog bounded-context hook.
/// This project does not reference Catalog.API.
/// The parameterless method leaves the catalog empty.
/// The assembly method scans that assembly once for event types, command handlers, and projections.
/// The shared-assembly method also scans cross-service events so the outbox can publish them.
/// Catalog.API Program.cs calls the shared-assembly method once.
/// </summary>
public static class CatalogContextRegistration
{
    public static void AddCatalogContext(this BoundedContextBuilder context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public static void AddCatalogContext(this BoundedContextBuilder context, Assembly serviceAssembly)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceAssembly);
        if (context.EventCatalog is not null)
        {
            throw new InvalidOperationException(
                "Catalog context already has an event catalog. " +
                "Call AddCatalogContext(Assembly) once. " +
                "Catalog.API Program.cs calls AddCatalogContext(Assembly, Assembly).");
        }

        context.WithEventTypes(serviceAssembly);
        context.WithCommandHandlers(serviceAssembly);
        context.WithProjections([serviceAssembly]);
    }

    /// <summary>
    /// Scans <paramref name="serviceAssembly"/> once for event types, command handlers, and projections.
    /// Scans <paramref name="sharedContractAssembly"/> for shared integration events.
    /// </summary>
    /// <param name="context">The Catalog bounded context.</param>
    /// <param name="serviceAssembly">The Catalog.API assembly.</param>
    /// <param name="sharedContractAssembly">The Shared assembly that holds cross-service events.</param>
    public static void AddCatalogContext(
        this BoundedContextBuilder context,
        Assembly serviceAssembly,
        Assembly sharedContractAssembly)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceAssembly);
        ArgumentNullException.ThrowIfNull(sharedContractAssembly);
        if (context.EventCatalog is not null)
        {
            throw new InvalidOperationException(
                "Catalog context already has an event catalog. " +
                "Call AddCatalogContext(Assembly, Assembly) once from Catalog.API Program.cs.");
        }

        context.WithEventTypes(serviceAssembly, sharedContractAssembly);
        context.WithCommandHandlers(serviceAssembly);
        context.WithProjections([serviceAssembly]);
    }
}
