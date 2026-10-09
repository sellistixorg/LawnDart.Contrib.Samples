using System.Reflection;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Identity bounded-context hook.
/// This project does not reference Identity.API.
/// The parameterless method leaves the catalog empty.
/// The assembly method scans that assembly once for event types, command handlers, and projections.
/// Identity.API Program.cs calls the assembly method once.
/// </summary>
public static class IdentityContextRegistration
{
    /// <summary>
    /// Leaves the Identity catalog empty.
    /// </summary>
    /// <param name="context">The Identity bounded context.</param>
    public static void AddIdentityContext(this BoundedContextBuilder context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    /// Scans <paramref name="serviceAssembly"/> once for event types, command handlers, and projections.
    /// </summary>
    /// <param name="context">The Identity bounded context.</param>
    /// <param name="serviceAssembly">The Identity.API assembly.</param>
    public static void AddIdentityContext(this BoundedContextBuilder context, Assembly serviceAssembly)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceAssembly);
        if (context.EventCatalog is not null)
        {
            throw new InvalidOperationException(
                "Identity context already has an event catalog. " +
                "Call AddIdentityContext(Assembly) once from Identity.API Program.cs.");
        }

        context.WithEventTypes(serviceAssembly);
        context.WithCommandHandlers(serviceAssembly);
        context.WithProjections([serviceAssembly]);
    }
}
