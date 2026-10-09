using System.Text.Json;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Outbox;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Testing.Bdd;
using Microsoft.Extensions.DependencyInjection;
using StockLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.ValidateOrderStock;

public class ValidateOrderStockIntegrationTests
{
    [Fact]
    public async Task Confirmed_and_rejected_events_publish_with_their_fields()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(CatalogItemAdded),
            typeof(OrderStockConfirmed),
            typeof(OrderStockRejected));
        var confirmedOrderId = Guid.NewGuid();
        var rejectedOrderId = Guid.NewGuid();
        var inStockId = Guid.NewGuid();
        var shortId = Guid.NewGuid();
        await AddItem(ctx, inStockId, 6);
        await AddItem(ctx, shortId, 1);
        var handler = new ValidateOrderStockHandler(ctx.AggregateRepository);
        await handler.HandleAsync(new ValidateOrderStockCommand(
            Guid.NewGuid(),
            confirmedOrderId,
            [new StockLine(inStockId, 2)]));
        await handler.HandleAsync(new ValidateOrderStockCommand(
            Guid.NewGuid(),
            rejectedOrderId,
            [new StockLine(shortId, 3), new StockLine(inStockId, 1)]));

        var confirmed = Assert.IsType<OrderStockConfirmed>(
            Assert.Single(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(confirmedOrderId))).Event);
        var rejected = Assert.IsType<OrderStockRejected>(
            Assert.Single(await ctx.EventStore.ReadStreamAsync(OrderStockValidation.StreamName(rejectedOrderId))).Event);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryMessaging();
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IMessageTransport>();
        var seenConfirmed = new TaskCompletionSource<OrderStockConfirmed>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var seenRejected = new TaskCompletionSource<OrderStockRejected>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await transport.SubscribeAsync<OrderStockConfirmed>((message, _, _) =>
        {
            seenConfirmed.TrySetResult(message);
            return Task.CompletedTask;
        });
        await transport.SubscribeAsync<OrderStockRejected>((message, _, _) =>
        {
            seenRejected.TrySetResult(message);
            return Task.CompletedTask;
        });

        var publisher = new MessageTransportOutboxPublisher(
            transport,
            [typeof(OrderStockConfirmed), typeof(OrderStockRejected)]);
        await publisher.PublishAsync(Outbox(confirmed, "order-stock-confirmed", OrderStockValidation.StreamName(confirmedOrderId), 1));
        await publisher.PublishAsync(Outbox(rejected, "order-stock-rejected", OrderStockValidation.StreamName(rejectedOrderId), 1));

        var deliveredConfirmed = await seenConfirmed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var deliveredRejected = await seenRejected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(confirmedOrderId, deliveredConfirmed.OrderId);
        Assert.Equal(confirmed.Id, deliveredConfirmed.Id);
        var confirmedJson = JsonSerializer.Serialize(deliveredConfirmed);
        Assert.DoesNotContain("Alpine boot", confirmedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("HasStock", confirmedJson, StringComparison.Ordinal);

        Assert.Equal(rejectedOrderId, deliveredRejected.OrderId);
        Assert.Equal(2, deliveredRejected.ConfirmedOrderStockItems.Count);
        Assert.Equal(shortId, deliveredRejected.ConfirmedOrderStockItems[0].ProductId);
        Assert.False(deliveredRejected.ConfirmedOrderStockItems[0].HasStock);
        Assert.Equal(inStockId, deliveredRejected.ConfirmedOrderStockItems[1].ProductId);
        Assert.True(deliveredRejected.ConfirmedOrderStockItems[1].HasStock);
        var rejectedJson = JsonSerializer.Serialize(deliveredRejected);
        Assert.DoesNotContain("Alpine boot", rejectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("price", rejectedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", rejectedJson, StringComparison.OrdinalIgnoreCase);
    }

    private static OutboxMessage Outbox<TEvent>(TEvent @event, string eventType, string streamId, long sequence)
        where TEvent : IEvent
    {
        return new OutboxMessage
        {
            Id = @event.Id,
            EventType = eventType,
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = JsonSerializer.SerializeToUtf8Bytes(@event),
            Metadata = "{}",
            CreatedAt = @event.Timestamp,
            StreamId = streamId,
            SequencePosition = sequence
        };
    }

    private static async Task AddItem(BddTestContext ctx, Guid itemId, int availableStock)
    {
        var item = await ctx.AggregateRepository.GetOrCreateAsync<CatalogItem>(itemId);
        await ctx.AggregateRepository.HandleCommandAsync(
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
}
