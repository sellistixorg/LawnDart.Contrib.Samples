using System.Text.Json;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.RabbitMQ;
using LawnDart.Messaging.Outbox;
using LawnDart.Outbox;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.WhenOrderPaidDeductStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaidLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.WhenOrderPaidDeductStock;

public class WhenOrderPaidDeductStockRegistrationTests
{
    [Fact]
    public void Reactor_is_registered_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        RegisterCatalog(services);

        var reactors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(EShopReactorHost<WhenOrderPaidDeductStockReactor, OrderPaid>))
            .ToArray();

        Assert.Single(reactors);
        Assert.Equal("order-paid", MessageTypeNames.Require(typeof(OrderPaid)));
        Assert.Equal(
            EShopServiceNames.Catalog,
            RabbitMqNames.Queue(
                EShopServiceNames.Catalog,
                RabbitMqQueueLayout.Subscription,
                MessageTypeNames.Require(typeof(OrderPaid))));
    }

    [Fact]
    public void Catalog_context_resolves_order_paid()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddCatalogContext(
            typeof(WhenOrderPaidDeductStockReactor).Assembly,
            typeof(OrderPaid).Assembly);

        Assert.True(context.EventCatalog!.TryResolveType("order-paid", out var signalType));
        Assert.Equal(typeof(OrderPaid), signalType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<DeductCatalogStockCommand>));
    }

    [Fact]
    public async Task Redelivery_of_the_same_message_id_deducts_once()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterCatalog(builder.Services);

        using var host = builder.Build();
        var orderId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var items = host.Services.GetRequiredService<IAggregateRepository>();
        await AddItem(items, bootId, 10);

        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscriberAsync(transport);

            var paid = Paid(orderId, new PaidLine(bootId, 3));
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, paid, messageId);
            await PublishAsync(transport, paid, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var deducted = await DeductedEvents(store, bootId);
            var stored = Assert.Single(deducted);
            Assert.Equal(orderId, stored.OrderId);
            Assert.Equal(3, Assert.Single(stored.Deductions).UnitsRemoved);
            Assert.Equal(7, (await items.GetOrCreateAsync<CatalogItem>(bootId)).State.AvailableStock);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Zero_stock_rejection_is_not_retried()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterCatalog(builder.Services);

        using var host = builder.Build();
        var orderId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var items = host.Services.GetRequiredService<IAggregateRepository>();
        await AddItem(items, bootId, 0);

        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscriberAsync(transport);

            var paid = Paid(orderId, new PaidLine(bootId, 1));
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, paid, messageId);
            await PublishAsync(transport, paid, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            Assert.Empty(await DeductedEvents(store, bootId));
            Assert.Equal(0, (await items.GetOrCreateAsync<CatalogItem>(bootId)).State.AvailableStock);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void RegisterCatalog(IServiceCollection services)
    {
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddCatalogContext(
            typeof(WhenOrderPaidDeductStockReactor).Assembly,
            typeof(OrderPaid).Assembly);
        services.AddEShopReactors(typeof(WhenOrderPaidDeductStockReactor).Assembly);
    }

    private static async Task AddItem(IAggregateRepository items, Guid itemId, int availableStock)
    {
        var item = await items.GetOrCreateAsync<CatalogItem>(itemId);
        await items.HandleCommandAsync(
            item,
            new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                availableStock,
                "High-ankle boot",
                "boot.png",
                10,
                100));
    }

    private static async Task WaitForSubscriberAsync(InMemoryMessageTransport transport)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (transport.SubscriberCount<OrderPaid>() == 0)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Order Paid subscription did not start.");
            await Task.Delay(20);
        }
    }

    private static async Task<IReadOnlyList<CatalogStockDeducted>> DeductedEvents(IEventStore store, Guid itemId)
    {
        var recorded = await store.ReadStreamAsync($"CatalogItem:{itemId}");
        return recorded.Select(row => row.Event).OfType<CatalogStockDeducted>().ToList();
    }

    private static OrderPaid Paid(Guid orderId, params PaidLine[] lines) =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, lines, Guid.NewGuid());

    private static async Task PublishAsync(IMessageTransport transport, OrderPaid paid, Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderPaid)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "order-paid",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(paid),
            Metadata = "{}",
            CreatedAt = paid.Timestamp,
            StreamId = $"Order:{paid.OrderId:D}",
            SequencePosition = 4
        });
    }
}
