using System.Reflection;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Order processor bounded-context hook.
/// This project does not reference OrderProcessor.
/// OrderProcessor Program.cs passes the shared contract assembly.
/// </summary>
public static class OrderProcessorContextRegistration
{
    /// <summary>
    /// Leaves the catalog empty. The host then registers the stand-in event.
    /// </summary>
    /// <param name="context">The Order Processor bounded context.</param>
    public static void AddOrderProcessorContext(this BoundedContextBuilder context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Registers the shared integration events Ordering already stores in <c>orderingdb</c>.
    /// </summary>
    /// <param name="context">The Order Processor bounded context.</param>
    /// <param name="sharedContractAssembly">The Shared assembly that holds cross-service events.</param>
    /// <remarks>
    /// Order Processor publishes <c>grace-period-confirmed</c> and consumes <c>order-started</c>.
    /// Those types are in <paramref name="sharedContractAssembly"/>. Ordering.API setup already
    /// records them. This process does not add a type of its own, and it does not create tables.
    /// </remarks>
    public static void AddOrderProcessorContext(
        this BoundedContextBuilder context,
        Assembly sharedContractAssembly)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sharedContractAssembly);
        if (context.EventCatalog is not null)
        {
            throw new InvalidOperationException(
                "Order processor context already has an event catalog. " +
                "Call AddOrderProcessorContext(Assembly) once from OrderProcessor Program.cs.");
        }

        context.WithEventTypes(sharedContractAssembly);
    }
}
