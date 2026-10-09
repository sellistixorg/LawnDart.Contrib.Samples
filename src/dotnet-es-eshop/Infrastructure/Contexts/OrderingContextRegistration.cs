using System.Reflection;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Ordering bounded-context hook.
/// This project does not reference Ordering.API.
/// The parameterless method leaves the catalog empty.
/// The assembly method scans the service assembly once for event types, command handlers, and projections,
/// and scans the shared contract assembly for integration events.
/// Ordering.API Program.cs calls the assembly method once.
/// </summary>
public static class OrderingContextRegistration
{
    /// <summary>
    /// Leaves the Ordering catalog empty.
    /// </summary>
    /// <param name="context">The Ordering bounded context.</param>
    public static void AddOrderingContext(this BoundedContextBuilder context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Scans <paramref name="serviceAssembly"/> once for event types, command handlers, and projections.
    /// Scans <paramref name="sharedContractAssembly"/> for shared integration events.
    /// </summary>
    /// <param name="context">The Ordering bounded context.</param>
    /// <param name="serviceAssembly">The Ordering.API assembly.</param>
    /// <param name="sharedContractAssembly">The Shared assembly that holds cross-service events.</param>
    public static void AddOrderingContext(
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
                "Ordering context already has an event catalog. " +
                "Call AddOrderingContext(Assembly, Assembly) once from Ordering.API Program.cs.");
        }

        context.WithEventTypes(serviceAssembly, sharedContractAssembly);
        context.WithCommandHandlers(serviceAssembly);
        context.WithProjections([serviceAssembly]);
    }
}
