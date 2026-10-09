namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Subscription client names for the sample RabbitMQ transport.
/// Each name is one queue. Instances of the same service share that queue.
/// Different services do not.
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
