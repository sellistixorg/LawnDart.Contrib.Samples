using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;
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
    /// A present <c>eventbus</c> connection string uses <c>LawnDart.Messaging.RabbitMQ</c>.
    /// A missing connection string uses in-memory messaging.
    /// Recorded message types are declared at startup when topology setup is on.
    /// </remarks>
    public static IHostApplicationBuilder AddOrderStatusNotifications(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEShopMessageTransport(EShopServiceNames.WebApp);
        builder.Services
            .AddEShopSubscription<OrderStarted>()
            .AddEShopSubscription<OrderAwaitingValidation>()
            .AddEShopSubscription<OrderStatusStockConfirmed>()
            .AddEShopSubscription<OrderPaid>()
            .AddEShopSubscription<OrderShippedMessage>()
            .AddEShopSubscription<OrderCancelledMessage>()
            .AddEShopSubscription<OrderCancelledForStockMessage>();
        builder.Services.AddSingleton<OrderStatusNotificationService>();
        builder.Services.AddSingleton(new OrderListRefresh());
        builder.Services.AddHostedService<OrderStatusSubscriptionHost>();
        return builder;
    }
}
