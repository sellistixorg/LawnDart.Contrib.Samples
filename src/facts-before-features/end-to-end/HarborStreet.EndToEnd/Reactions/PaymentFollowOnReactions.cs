using HarborStreet.EndToEnd.Commands;
using HarborStreet.EndToEnd.Domain;
using HarborStreet.EndToEnd.Events;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Dcb;

namespace HarborStreet.EndToEnd.Reactions;

/// <summary>
/// Payment Captured → Start Fulfillment; Payment Failed → Cancel Order + Stock Released (DCB).
/// </summary>
public sealed class PaymentFollowOnReactions
{
    private readonly IAggregateRepository _orders;
    private readonly IDcbRepository _dcb;

    public PaymentFollowOnReactions(IAggregateRepository orders, IDcbRepository dcb)
    {
        _orders = orders;
        _dcb = dcb;
    }

    public async Task ReactAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        switch (@event)
        {
            case PaymentCaptured e:
                await OnPaymentCapturedAsync(e, cancellationToken);
                break;
            case PaymentFailed e:
                await OnPaymentFailedAsync(e, cancellationToken);
                break;
        }
    }

    private async Task OnPaymentCapturedAsync(PaymentCaptured e, CancellationToken cancellationToken)
    {
        var order = await _orders.GetOrCreateAsync<Order>(e.OrderId, cancellationToken);
        await _orders.HandleCommandAsync(
            order,
            new StartFulfillmentCommand(Guid.NewGuid(), e.OrderId),
            cancellationToken: cancellationToken);
    }

    private async Task OnPaymentFailedAsync(PaymentFailed e, CancellationToken cancellationToken)
    {
        var order = await _orders.GetOrCreateAsync<Order>(e.OrderId, cancellationToken);
        var titleId = order.State.TitleId;
        var quantity = order.State.Quantity;

        await _orders.HandleCommandAsync(
            order,
            new CancelOrderForPaymentFailureCommand(
                Guid.NewGuid(), e.OrderId, "Cancelled after payment failure"),
            cancellationToken: cancellationToken);

        var inventory = await _dcb.GetOrCreateEntityAsync<InventoryDecision>(
            InventoryDecision.ForTitle(titleId), cancellationToken);
        await _dcb.HandleCommandAsync(
            inventory,
            new ReleaseStockCommand(Guid.NewGuid(), titleId, e.OrderId, quantity),
            cancellationToken: cancellationToken);
    }
}
