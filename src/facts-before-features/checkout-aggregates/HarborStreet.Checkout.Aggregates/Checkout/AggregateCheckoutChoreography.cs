using HarborStreet.Checkout.Aggregates.Catalog;
using HarborStreet.Checkout.Aggregates.Catalog.Commands;
using HarborStreet.Checkout.Aggregates.Orders;
using HarborStreet.Checkout.Aggregates.Orders.Commands;
using LawnDart.Aggregates;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Aggregates.Checkout;

/// <summary>
/// Traditional aggregate checkout: optimistic stock check → Place Order → Reserve Stock.
/// Order and Title are different streams, so two checkouts that both observed the last
/// unit can commit two Order Placed facts and only one Stock Reserved (compensation required).
/// Contrast with checkout-dcb.
/// </summary>
public sealed class AggregateCheckoutChoreography
{
    private readonly IAggregateRepository _repository;

    public AggregateCheckoutChoreography(IAggregateRepository repository)
    {
        _repository = repository;
    }

    /// <param name="skipStockCheck">
    /// When true, models both racers having already observed available stock (stale read)
    /// and proceeding to Place Order. Used by the concurrent last-copy teaching test.
    /// </param>
    /// <param name="compensateOnReserveFailure">
    /// When true, cancel the orphan order after a failed reserve (still proves the pain path ran).
    /// </param>
    public async Task<CheckoutOutcome> PlaceOrderAsync(
        Guid orderId,
        Guid customerId,
        Guid titleId,
        int quantity = 1,
        bool skipStockCheck = false,
        bool compensateOnReserveFailure = false,
        CancellationToken cancellationToken = default)
    {
        if (!skipStockCheck)
        {
            // Optimistic read across aggregate boundary (stale under concurrency).
            var titleProbe = await _repository.GetOrCreateAsync<Title>(titleId, cancellationToken);
            if (!titleProbe.State.Exists || titleProbe.State.AvailableUnits < quantity)
            {
                return new CheckoutOutcome(
                    CheckoutStatus.RejectedUnavailable,
                    orderId,
                    customerId,
                    "Title unavailable");
            }
        }

// start snippet place-then-reserve
        // Commit Order on its own stream — survives even if reserve fails next.
        var order = await _repository.GetOrCreateAsync<Order>(orderId, cancellationToken);
        await _repository.HandleCommandAsync(
            order,
            new PlaceOrderCommand(Guid.NewGuid(), orderId, customerId, titleId, quantity),
            cancellationToken: cancellationToken);

        // Reserve on Title (separate consistency boundary).
        var title = await _repository.GetOrCreateAsync<Title>(titleId, cancellationToken);
        try
        {
            await _repository.HandleCommandAsync(
                title,
                new ReserveStockCommand(Guid.NewGuid(), titleId, orderId, quantity),
                cancellationToken: cancellationToken);

            return new CheckoutOutcome(CheckoutStatus.Succeeded, orderId, customerId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ConcurrencyException)
        {
            if (compensateOnReserveFailure)
            {
                order = await _repository.GetOrCreateAsync<Order>(orderId, cancellationToken);
                await _repository.HandleCommandAsync(
                    order,
                    new CancelOrderForFailedReservationCommand(
                        Guid.NewGuid(),
                        orderId,
                        "Title unavailable after Place Order — compensation"),
                    cancellationToken: cancellationToken);
            }

            return new CheckoutOutcome(
                CheckoutStatus.NeedsCompensation,
                orderId,
                customerId,
                ex.Message);
// end snippet place-then-reserve
        }
    }
}
