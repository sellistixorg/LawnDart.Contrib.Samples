using HarborStreet.Coordination.Commands;
using HarborStreet.Coordination.Domain;
using HarborStreet.Coordination.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Coordination.Reactions;

/// <summary>
/// Process-style reactions after payment facts. Not part of the Place Order decision (Ch 7).
/// Payment Captured → Start Fulfillment; Payment Failed → Cancel Order + Stock Released.
/// </summary>
public sealed class PaymentFollowOnReactions
{
    private readonly IAggregateRepository _repository;

    public PaymentFollowOnReactions(IAggregateRepository repository)
    {
        _repository = repository;
    }

// start snippet react-to-payment
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
        var order = await _repository.GetOrCreateAsync<Order>(e.OrderId, cancellationToken);
        await _repository.HandleCommandAsync(
            order,
            new StartFulfillmentCommand(Guid.NewGuid(), e.OrderId),
            cancellationToken: cancellationToken);
    }
// end snippet react-to-payment

// start snippet payment-failed
    private async Task OnPaymentFailedAsync(PaymentFailed e, CancellationToken cancellationToken)
    {
        var order = await _repository.GetOrCreateAsync<Order>(e.OrderId, cancellationToken);
        var titleId = order.State.TitleId;
        var quantity = order.State.Quantity;

        await _repository.HandleCommandAsync(
            order,
            new CancelOrderForPaymentFailureCommand(
                Guid.NewGuid(), e.OrderId, "Cancelled after payment failure"),
            cancellationToken: cancellationToken);

        var title = await _repository.GetOrCreateAsync<Title>(titleId, cancellationToken);
        await _repository.HandleCommandAsync(
            title,
            new ReleaseStockCommand(Guid.NewGuid(), titleId, e.OrderId, quantity),
            cancellationToken: cancellationToken);
    }
// end snippet payment-failed
}
