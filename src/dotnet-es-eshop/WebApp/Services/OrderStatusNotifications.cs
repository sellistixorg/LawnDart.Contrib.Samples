using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Registers the storefront subscription to order status messages.
/// </summary>
public static class OrderStatusNotifications
{
    /// <summary>
    /// Registers the message transport, the per-buyer notifier, and the subscription host.
    /// </summary>
    /// <param name="builder">The storefront host.</param>
    /// <returns>The same host.</returns>
    /// <remarks>
    /// A present <c>eventbus</c> connection string uses the sample RabbitMQ transport.
    /// A missing connection string uses in-memory messaging.
    /// </remarks>
    public static IHostApplicationBuilder AddOrderStatusNotifications(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEShopMessageTransport(EShopServiceNames.WebApp);
        builder.Services.AddSingleton<OrderStatusNotificationService>();
        builder.Services.AddSingleton(new OrderListRefresh());
        builder.Services.AddHostedService<OrderStatusSubscriptionHost>();
        return builder;
    }
}
