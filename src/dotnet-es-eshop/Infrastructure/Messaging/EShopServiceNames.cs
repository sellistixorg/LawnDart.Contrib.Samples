namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Subscription names for the RabbitMQ transport.
/// Instances of the same service share a name. Different services do not.
/// Each name is one quorum queue. The service binds that queue to each event type it handles.
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
