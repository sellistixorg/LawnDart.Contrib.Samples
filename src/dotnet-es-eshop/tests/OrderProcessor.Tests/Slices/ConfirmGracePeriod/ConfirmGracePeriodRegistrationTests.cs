using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.OrderProcessor.Tests.Slices.ConfirmGracePeriod;

public class ConfirmGracePeriodRegistrationTests
{
    [Fact]
    public void Timer_and_reactor_are_registered_once()
    {
        var builder = Host.CreateApplicationBuilder();
        AddGraceConfiguration(builder.Configuration);
        builder.AddEShopSlices(typeof(TrackSubmittedOrderReactor).Assembly);
        var services = builder.Services;

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(EShopReactorHost<TrackSubmittedOrderReactor, OrderStarted>));
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(GracePeriodManagerService));
    }

    [Fact]
    public void Routing_keys_match_the_event_tokens()
    {
        Assert.Equal("order-started", MessageTypeNames.Require(typeof(OrderStarted)));
        Assert.Equal("grace-period-confirmed", MessageTypeNames.Require(typeof(GracePeriodConfirmed)));
    }

    [Fact]
    public async Task Due_order_becomes_awaiting_validation_once()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        using var host = StartHost(time);
        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscribersAsync(transport);
            var orderId = Guid.NewGuid();
            var buyerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            await PlaceAsync(host.Services, orderId, buyerId, productId);
            await PublishStartedAsync(transport, orderId, buyerId, time.GetUtcNow().UtcDateTime);

            var manager = host.Services.GetServices<IHostedService>().OfType<GracePeriodManagerService>().Single();
            await manager.ConfirmDueAsync();
            Assert.Equal(OrderAggregate.SubmittedStatus, (await LoadAsync(host.Services, orderId)).State.OrderStatus);

            time.Advance(TimeSpan.FromMinutes(1));
            await manager.ConfirmDueAsync();
            await manager.ConfirmDueAsync();

            var store = host.Services.GetRequiredService<LawnDart.EventStore.IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            var awaiting = Assert.IsType<OrderAwaitingValidation>(
                Assert.Single(recorded, row => row.Event is OrderAwaitingValidation).Event);
            Assert.Equal(orderId, awaiting.OrderId);
            Assert.Equal(buyerId, awaiting.BuyerId);
            var stock = Assert.Single(awaiting.OrderStockItems);
            Assert.Equal(productId, stock.ProductId);
            Assert.Equal(OrderAggregate.AwaitingValidationStatus, (await LoadAsync(host.Services, orderId)).State.OrderStatus);

            var views = host.Services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName);
            Assert.Null(await views.GetViewAsync(PendingGraceOrders.ViewType, orderId.ToString("D")));
            var again = new GracePeriodManagerService(
                new PendingGraceOrders(views),
                transport,
                Microsoft.Extensions.Options.Options.Create(new BackgroundTaskOptions()),
                time,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GracePeriodManagerService>.Instance);
            await again.ConfirmDueAsync();

            recorded = await store.ReadStreamAsync($"Order:{orderId}");
            Assert.Single(recorded, row => row.Event is OrderAwaitingValidation);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Order_not_yet_due_stays_submitted()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        using var host = StartHost(time);
        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscribersAsync(transport);
            var orderId = Guid.NewGuid();
            await PlaceAsync(host.Services, orderId, Guid.NewGuid(), Guid.NewGuid());
            await PublishStartedAsync(transport, orderId, Guid.NewGuid(), time.GetUtcNow().UtcDateTime);
            time.Advance(TimeSpan.FromSeconds(59));

            var manager = host.Services.GetServices<IHostedService>().OfType<GracePeriodManagerService>().Single();
            await manager.ConfirmDueAsync();

            Assert.Equal(OrderAggregate.SubmittedStatus, (await LoadAsync(host.Services, orderId)).State.OrderStatus);
            var store = host.Services.GetRequiredService<LawnDart.EventStore.IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            Assert.DoesNotContain(recorded, row => row.Event is OrderAwaitingValidation);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Order_that_left_submitted_writes_no_second_event()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        using var host = StartHost(time);
        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscribersAsync(transport);
            var orderId = Guid.NewGuid();
            await PlaceAsync(host.Services, orderId, Guid.NewGuid(), Guid.NewGuid());
            var marker = new MarkOrderAwaitingValidationHandler(
                host.Services.GetRequiredService<IAggregateRepository>());
            await marker.HandleAsync(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId));
            await PublishStartedAsync(transport, orderId, Guid.NewGuid(), time.GetUtcNow().UtcDateTime);
            time.Advance(TimeSpan.FromMinutes(2));

            var manager = host.Services.GetServices<IHostedService>().OfType<GracePeriodManagerService>().Single();
            await manager.ConfirmDueAsync();

            var store = host.Services.GetRequiredService<LawnDart.EventStore.IEventStore>();
            var recorded = await store.ReadStreamAsync($"Order:{orderId}");
            Assert.Single(recorded, row => row.Event is OrderAwaitingValidation);
            Assert.Equal(OrderAggregate.AwaitingValidationStatus, (await LoadAsync(host.Services, orderId)).State.OrderStatus);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IHost StartHost(TimeProvider time)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        var services = builder.Services;
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddOrderingContext(typeof(OrderPlaced).Assembly, typeof(OrderStarted).Assembly);
        AddGraceConfiguration(builder.Configuration);
        builder.AddEShopSlices(typeof(TrackSubmittedOrderReactor).Assembly);
        services.AddEShopReactors(typeof(ConfirmGracePeriodReactor).Assembly);
        foreach (var existing in services.Where(descriptor => descriptor.ServiceType == typeof(TimeProvider)).ToArray())
            services.Remove(existing);
        services.AddSingleton(time);
        return builder.Build();
    }

    private static void AddGraceConfiguration(IConfigurationBuilder configuration) =>
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{BackgroundTaskOptions.SectionName}:GracePeriodTime"] = "1",
            [$"{BackgroundTaskOptions.SectionName}:CheckUpdateTime"] = "30"
        });

    private static async Task WaitForSubscribersAsync(InMemoryMessageTransport transport)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (transport.SubscriberCount<OrderStarted>() == 0
               || transport.SubscriberCount<GracePeriodConfirmed>() == 0)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Grace period subscriptions did not start.");
            await Task.Delay(20);
        }
    }

    private static async Task PlaceAsync(IServiceProvider services, Guid orderId, Guid buyerId, Guid productId)
    {
        var orders = services.GetRequiredService<IAggregateRepository>();
        var order = await orders.GetOrCreateAsync<OrderAggregate>(orderId);
        await orders.HandleCommandAsync(
            order,
            new PlaceOrderCommand(
                orderId,
                orderId,
                buyerId,
                "Ada Lovelace",
                "1 Main",
                "Seattle",
                "WA",
                "USA",
                "98101",
                1,
                "Ada Lovelace",
                "4444",
                new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                [new OrderLine(productId, "Alpine boot", 120m, 2)]));
    }

    private static async Task PublishStartedAsync(
        IMessageTransport transport,
        Guid orderId,
        Guid buyerId,
        DateTime timestamp)
    {
        var started = new OrderStarted(Guid.NewGuid(), timestamp, orderId, buyerId);
        var context = new MessageContext
        {
            MessageId = started.Id.ToString("D"),
            EnqueuedAt = new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc))
        };
        await transport.PublishAsync(started, context);
    }

    private static async Task<OrderAggregate> LoadAsync(IServiceProvider services, Guid orderId) =>
        await services.GetRequiredService<IAggregateRepository>().GetOrCreateAsync<OrderAggregate>(orderId);

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _utc;

        public ManualTime(DateTimeOffset utc) => _utc = utc;

        public override DateTimeOffset GetUtcNow() => _utc;

        public void Advance(TimeSpan by) => _utc += by;
    }
}
