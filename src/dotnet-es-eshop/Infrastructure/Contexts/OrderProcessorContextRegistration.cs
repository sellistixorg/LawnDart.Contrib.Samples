using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Order processor bounded-context hook. Empty.
/// This project does not reference OrderProcessor.
/// OrderProcessor Program.cs owns the one assembly scan when that service has slice types.
/// </summary>
public static class OrderProcessorContextRegistration
{
    public static void AddOrderProcessorContext(this BoundedContextBuilder context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}
