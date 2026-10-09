using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Webhooks bounded-context hook. Empty.
/// This project does not reference Webhooks.API.
/// Webhooks.API Program.cs owns the one assembly scan when that service has slice types.
/// </summary>
public static class WebhooksContextRegistration
{
    public static void AddWebhooksContext(this BoundedContextBuilder context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}
