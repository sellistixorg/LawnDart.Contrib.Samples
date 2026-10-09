using System.Reflection;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Payment bounded-context hook.
/// This project does not reference PaymentProcessor.
/// The parameterless method leaves the catalog empty.
/// The assembly method scans the service assembly once for event types, command handlers, and projections,
/// and scans the shared contract assembly for integration events.
/// PaymentProcessor Program.cs calls the assembly method once.
/// </summary>
public static class PaymentContextRegistration
{
    /// <summary>
    /// Leaves the Payment catalog empty.
    /// </summary>
    /// <param name="context">The Payment bounded context.</param>
    public static void AddPaymentContext(this BoundedContextBuilder context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Scans <paramref name="serviceAssembly"/> once for event types, command handlers, and projections.
    /// Scans <paramref name="sharedContractAssembly"/> for shared integration events.
    /// </summary>
    /// <param name="context">The Payment bounded context.</param>
    /// <param name="serviceAssembly">The PaymentProcessor assembly.</param>
    /// <param name="sharedContractAssembly">The Shared assembly that holds cross-service events.</param>
    public static void AddPaymentContext(
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
                "Payment context already has an event catalog. " +
                "Call AddPaymentContext(Assembly, Assembly) once from PaymentProcessor Program.cs.");
        }

        context.WithEventTypes(serviceAssembly, sharedContractAssembly);
        context.WithCommandHandlers(serviceAssembly);
        context.WithProjections([serviceAssembly]);
    }
}
