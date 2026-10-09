using System.Text.Json;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Outbox;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.WhenOrderAwaitingValidationValidateStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.WhenOrderAwaitingValidationValidateStock;

public class WhenOrderAwaitingValidationValidateStockRegistrationTests
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
                    == typeof(EShopReactorHost<WhenOrderAwaitingValidationValidateStockReactor, OrderAwaitingValidation>))
            .ToArray();

        Assert.Single(reactors);
        Assert.Equal(
            "order-awaiting-validation",
            EShopRabbitMq.RoutingKey(typeof(OrderAwaitingValidation)));
        Assert.Equal("eshop.catalog", EShopRabbitMq.QueueName(EShopServiceNames.Catalog));
    }

    [Fact]
    public void Catalog_context_resolves_order_awaiting_validation()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddCatalogContext(
            typeof(WhenOrderAwaitingValidationValidateStockReactor).Assembly,
            typeof(OrderAwaitingValidation).Assembly);

        Assert.True(context.EventCatalog!.TryResolveType("order-awaiting-validation", out var signalType));
        Assert.Equal(typeof(OrderAwaitingValidation), signalType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<ValidateOrderStockCommand>));
    }

    [Fact]
    public async Task Same_message_id_twice_validates_once()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterCatalog(builder.Services);

        using var host = builder.Build();
        var orderId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var items = host.Services.GetRequiredService<IAggregateRepository>();
        var item = await items.GetOrCreateAsync<CatalogItem>(bootId);
        await items.HandleCommandAsync(
            item,
            new AddCatalogItemCommand(
                Guid.NewGuid(),
                bootId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                4,
                "High-ankle boot",
                "boot.png",
                10,
                100));

        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (transport.SubscriberCount<OrderAwaitingValidation>() == 0)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Order Awaiting Validation subscription did not start.");
                await Task.Delay(20);
            }

            var awaiting = new OrderAwaitingValidation(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [new StockLine(bootId, 2)],
                Guid.NewGuid());
            var messageId = Guid.NewGuid();
            await PublishAsync(transport, awaiting, messageId);
            await PublishAsync(transport, awaiting, messageId);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync(OrderStockValidation.StreamName(orderId));
            var confirmed = Assert.IsType<OrderStockConfirmed>(Assert.Single(recorded).Event);
            Assert.Equal(orderId, confirmed.OrderId);
            var check = await items.GetOrCreateAsync<OrderStockValidation>(orderId);
            Assert.Equal(1, check.Version);
            Assert.True(check.State.Confirmed);
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
            typeof(WhenOrderAwaitingValidationValidateStockReactor).Assembly,
            typeof(OrderAwaitingValidation).Assembly);
        services.AddEShopReactors(typeof(WhenOrderAwaitingValidationValidateStockReactor).Assembly);
    }

    private static async Task PublishAsync(
        IMessageTransport transport,
        OrderAwaitingValidation awaiting,
        Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderAwaitingValidation)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "order-awaiting-validation",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(awaiting),
            Metadata = "{}",
            CreatedAt = awaiting.Timestamp,
            StreamId = $"Order:{awaiting.OrderId:D}",
            SequencePosition = 3
        });
    }
}
