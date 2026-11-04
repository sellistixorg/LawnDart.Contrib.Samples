using HarborStreet.Checkout.Dcb.Commands;
using LawnDart.Dcb;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Dcb.Checkout;

/// <summary>
/// Place Order through a DCB decision loaded on <c>title:{titleId}</c>.
/// </summary>
public sealed class DcbCheckoutService
{
    private readonly IDcbRepository _dcb;

    public DcbCheckoutService(IDcbRepository dcb)
    {
        _dcb = dcb;
    }

    public async Task SeedLastCopyAsync(
        Guid titleId,
        string name = "The Lighthouse Keeper",
        decimal price = 52.00m,
        CancellationToken cancellationToken = default)
    {
        var tags = CheckoutDecision.TitleTags(titleId);
        var entity = await _dcb.GetOrCreateEntityAsync<CheckoutDecision>(tags, cancellationToken);
        await _dcb.HandleCommandAsync(
            entity,
            new AddTitleToCatalogCommand(Guid.NewGuid(), titleId, name, price, IsScarce: true),
            cancellationToken: cancellationToken);

        entity = await _dcb.GetOrCreateEntityAsync<CheckoutDecision>(tags, cancellationToken);
        await _dcb.HandleCommandAsync(
            entity,
            new RestockTitleCommand(Guid.NewGuid(), titleId, Quantity: 1),
            cancellationToken: cancellationToken);
    }

    public async Task<DcbCheckoutOutcome> PlaceOrderAsync(
        Guid orderId,
        Guid customerId,
        Guid titleId,
        int quantity = 1,
        CheckoutDecision? preloaded = null,
        CancellationToken cancellationToken = default)
    {
        var tags = CheckoutDecision.TitleTags(titleId);
        var entity = preloaded
            ?? await _dcb.GetOrCreateEntityAsync<CheckoutDecision>(tags, cancellationToken);

        try
        {
            await _dcb.HandleCommandAsync(
                entity,
                new PlaceOrderCommand(Guid.NewGuid(), orderId, customerId, titleId, quantity),
                cancellationToken: cancellationToken);

            return new DcbCheckoutOutcome(DcbCheckoutStatus.Succeeded, orderId, customerId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return new DcbCheckoutOutcome(
                DcbCheckoutStatus.RejectedUnavailable,
                orderId,
                customerId,
                ex.Message);
        }
        catch (ConcurrencyException ex)
        {
            // AppendCondition lost the race — nothing persisted for this order.
            return new DcbCheckoutOutcome(
                DcbCheckoutStatus.ConflictLost,
                orderId,
                customerId,
                ex.Message);
        }
    }
}
