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
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasketWhenOrderStarted;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.ClearBasketWhenOrderStarted;

public class ClearBasketWhenOrderStartedRegistrationTests
{
    [Fact]
    public void Reactor_is_registered_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        RegisterBasket(services);

        var reactors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(EShopReactorHost<ClearBasketWhenOrderStartedReactor, OrderStarted>))
            .ToArray();

        Assert.Single(reactors);
    }

    [Fact]
    public async Task Subscribed_order_started_clears_the_basket_once()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        RegisterBasket(builder.Services);

        using var host = builder.Build();
        var buyerId = Guid.NewGuid();
        var baskets = host.Services.GetRequiredService<IAggregateRepository>();
        var basket = await baskets.GetOrCreateAsync<BasketAggregate>(buyerId);
        await baskets.HandleCommandAsync(
            basket,
            new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                Guid.NewGuid(),
                "Alpine boot",
                120m,
                2,
                "boot.png"));

        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (transport.SubscriberCount<OrderStarted>() == 0)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Order Started subscription did not start.");
                await Task.Delay(20);
            }

            var started = new OrderStarted(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), buyerId);
            await PublishAsync(transport, started, started.Id);
            await PublishAsync(transport, started, started.Id);

            var store = host.Services.GetRequiredService<IEventStore>();
            var recorded = await store.ReadStreamAsync(BasketContentsProjection.InstanceId(buyerId));
            var cleared = Assert.IsType<BasketCleared>(
                Assert.Single(recorded, row => row.Event is BasketCleared).Event);
            Assert.Equal(buyerId, cleared.BuyerId);
            Assert.Equal(ClearBasketWhenOrderStartedReactor.Reason, cleared.Reason);
            Assert.Empty((await baskets.GetOrCreateAsync<BasketAggregate>(buyerId)).State.Items);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void RegisterBasket(IServiceCollection services)
    {
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.UseInMemory();
        context.AddBasketContext(typeof(BasketItemAdded).Assembly);
        services.AddEShopReactors(typeof(ClearBasketWhenOrderStartedReactor).Assembly);
    }

    private static async Task PublishAsync(IMessageTransport transport, OrderStarted started, Guid messageId)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(OrderStarted)]);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = messageId,
            EventType = "order-started",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(started),
            Metadata = "{}",
            CreatedAt = started.Timestamp,
            StreamId = $"Order:{started.OrderId:D}",
            SequencePosition = 2
        });
    }
}
