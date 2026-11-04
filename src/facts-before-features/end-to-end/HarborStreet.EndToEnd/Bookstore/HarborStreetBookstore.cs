using HarborStreet.EndToEnd.Checkout;
using HarborStreet.EndToEnd.Commands;
using HarborStreet.EndToEnd.Domain;
using HarborStreet.EndToEnd.Payments;
using LawnDart.Aggregates;
using LawnDart.Dcb;
using LawnDart.EventStore;

namespace HarborStreet.EndToEnd.Bookstore;

/// <summary>
/// Capstone facade: DCB Place Order (last-copy) then Order-stream materialization + payment follow-on.
/// </summary>
public sealed class HarborStreetBookstore
{
    private readonly IDcbRepository _dcb;
    private readonly IAggregateRepository _orders;
    private readonly PaymentCaptureService _payments;

    public HarborStreetBookstore(
        IDcbRepository dcb,
        IAggregateRepository orders,
        PaymentCaptureService payments)
    {
        _dcb = dcb;
        _orders = orders;
        _payments = payments;
    }

    public async Task SeedLastCopyAsync(
        Guid titleId,
        string name = "The Lighthouse Keeper",
        decimal price = 52.00m,
        CancellationToken cancellationToken = default)
    {
        var tags = InventoryDecision.TitleTags(titleId);
        var entity = await _dcb.GetOrCreateEntityAsync<InventoryDecision>(tags, cancellationToken);
        await _dcb.HandleCommandAsync(
            entity,
            new AddTitleToCatalogCommand(Guid.NewGuid(), titleId, name, price, IsScarce: true),
            cancellationToken: cancellationToken);

        entity = await _dcb.GetOrCreateEntityAsync<InventoryDecision>(tags, cancellationToken);
        await _dcb.HandleCommandAsync(
            entity,
            new RestockTitleCommand(Guid.NewGuid(), titleId, Quantity: 1),
            cancellationToken: cancellationToken);
    }

// start snippet place-order-and-capture
    public async Task<CheckoutOutcome> PlaceOrderAsync(
        Guid orderId,
        Guid customerId,
        Guid titleId,
        int quantity = 1,
        InventoryDecision? preloaded = null,
        CancellationToken cancellationToken = default)
    {
        var tags = InventoryDecision.TitleTags(titleId);
        var entity = preloaded
            ?? await _dcb.GetOrCreateEntityAsync<InventoryDecision>(tags, cancellationToken);

        try
        {
            await _dcb.HandleCommandAsync(
                entity,
                new PlaceOrderCommand(Guid.NewGuid(), orderId, customerId, titleId, quantity),
                cancellationToken: cancellationToken);

            // Materialize order stream for payment lifecycle (same business fact; separate stream).
            var order = await _orders.GetOrCreateAsync<Order>(orderId, cancellationToken);
            await _orders.HandleCommandAsync(
                order,
                new PlaceOrderCommand(Guid.NewGuid(), orderId, customerId, titleId, quantity),
                cancellationToken: cancellationToken);

            return new CheckoutOutcome(CheckoutStatus.Succeeded, orderId, customerId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return new CheckoutOutcome(CheckoutStatus.RejectedUnavailable, orderId, customerId, ex.Message);
        }
        catch (ConcurrencyException ex)
        {
            return new CheckoutOutcome(CheckoutStatus.ConflictLost, orderId, customerId, ex.Message);
        }
    }

    public Task CapturePaymentAsync(Guid orderId, decimal amount, CancellationToken cancellationToken = default)
        => _payments.CaptureAsync(orderId, amount, cancellationToken);
// end snippet place-order-and-capture

    public Task<InventoryDecision> GetInventoryAsync(Guid titleId, CancellationToken cancellationToken = default)
        => _dcb.GetOrCreateEntityAsync<InventoryDecision>(
            InventoryDecision.TitleTags(titleId), cancellationToken);

    public Task<Order> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _orders.GetOrCreateAsync<Order>(orderId, cancellationToken);
}
