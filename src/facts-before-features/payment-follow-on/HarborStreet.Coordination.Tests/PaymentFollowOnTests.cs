using HarborStreet.Coordination;
using HarborStreet.Coordination.Coordination;
using HarborStreet.Coordination.Domain;
using HarborStreet.Coordination.Events;
using HarborStreet.Coordination.Payments;
using HarborStreet.Coordination.Reactions;
using HarborStreet.Coordination.Seeding;
using LawnDart.EventStore;
using LawnDart.Testing.Bdd;
using Xunit;

namespace HarborStreet.Coordination.Tests;

/// <summary>
/// Payment follow-on coordination — Capture → fulfill; fail → cancel + Stock Released.
/// Source names: COMMANDS_AND_EVENTS.md §4–5. Not part of last-copy Place Order GWT.
/// </summary>
public class PaymentFollowOnTests
{
    [Fact]
    public async Task payment_captured_starts_fulfillment()
    {
        // Given order placed with stock reserved
        // When Capture Payment succeeds
        // Then Payment Captured and Fulfillment Started
        await using var ctx = BddTestContext.CreateInMemory(CoordinationEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await CheckoutSeed.SeedReservedOrderAsync(ctx.AggregateRepository, titleId, orderId, Guid.NewGuid());

        var gateway = new FakePaymentGateway { NextCaptureSucceeds = true };
        var payments = new PaymentCaptureService(
            ctx.AggregateRepository, gateway, new PaymentFollowOnReactions(ctx.AggregateRepository));

        await payments.CaptureAsync(orderId, 52.00m);

        var order = await ctx.AggregateRepository.GetOrCreateAsync<Order>(orderId);
        Assert.True(order.State.IsPaid);
        Assert.True(order.State.FulfillmentStarted);
        Assert.False(order.State.IsCancelled);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is PaymentCaptured));
        Assert.Equal(1, events.Count(e => e.Event is FulfillmentStarted));
        Assert.Equal(0, events.Count(e => e.Event is StockReleased));
    }

// start snippet payment-failed
    [Fact]
    public async Task payment_failed_cancels_order_and_releases_stock()
    {
        // Given order placed with stock reserved (available=0, reserved=1)
        // When Capture Payment declines
        // Then Payment Failed, Order Cancelled, Stock Released; available=1, reserved=0
        await using var ctx = BddTestContext.CreateInMemory(CoordinationEventTypes.All);
        var titleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await CheckoutSeed.SeedReservedOrderAsync(ctx.AggregateRepository, titleId, orderId, Guid.NewGuid());

        var titleBefore = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        Assert.Equal(0, titleBefore.State.AvailableUnits);
        Assert.Equal(1, titleBefore.State.ReservedUnits);

        var gateway = new FakePaymentGateway { NextCaptureSucceeds = false };
        var payments = new PaymentCaptureService(
            ctx.AggregateRepository, gateway, new PaymentFollowOnReactions(ctx.AggregateRepository));

        await payments.CaptureAsync(orderId, 52.00m);

        var order = await ctx.AggregateRepository.GetOrCreateAsync<Order>(orderId);
        Assert.True(order.State.PaymentFailed);
        Assert.True(order.State.IsCancelled);
        Assert.False(order.State.FulfillmentStarted);

        var title = await ctx.AggregateRepository.GetOrCreateAsync<Title>(titleId);
        Assert.Equal(1, title.State.AvailableUnits);
        Assert.Equal(0, title.State.ReservedUnits);

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(1, events.Count(e => e.Event is PaymentFailed));
        Assert.Equal(1, events.Count(e => e.Event is OrderCancelled));
        Assert.Equal(1, events.Count(e => e.Event is StockReleased));
        Assert.Equal(0, events.Count(e => e.Event is FulfillmentStarted));
// end snippet payment-failed
    }

    [Fact]
    public async Task reactions_are_separate_from_place_order_seed()
    {
        // Guardrail: seed (checkout) does not emit payment or fulfillment facts.
        await using var ctx = BddTestContext.CreateInMemory(CoordinationEventTypes.All);
        await CheckoutSeed.SeedReservedOrderAsync(
            ctx.AggregateRepository, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var events = (await ctx.EventStore.ReadByQueryAsync(Query.All())).Events;
        Assert.Equal(0, events.Count(e => e.Event is PaymentCaptured or PaymentFailed));
        Assert.Equal(0, events.Count(e => e.Event is FulfillmentStarted or OrderCancelled));
        Assert.Equal(1, events.Count(e => e.Event is OrderPlaced));
        Assert.Equal(1, events.Count(e => e.Event is StockReserved));
    }
}
