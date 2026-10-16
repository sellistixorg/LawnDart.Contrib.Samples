namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Subscription names for the RabbitMQ transport.
/// Instances of the same service share a name. Different services do not.
/// Each event type gets its own queue, named with the event type and this subscription.
/// </summary>
public static class EShopServiceNames
{
    public const string Basket = "basket";

    public const string Catalog = "catalog";

    public const string Ordering = "ordering";

    public const string OrderProcessor = "order-processor";

    public const string PaymentProcessor = "payment-processor";

    public const string Webhooks = "webhooks";

    /// <summary>
    /// Storefront queue. It receives order status messages for the orders page.
    /// </summary>
    public const string WebApp = "webapp";
}
