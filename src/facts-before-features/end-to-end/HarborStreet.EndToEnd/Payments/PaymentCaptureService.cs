using HarborStreet.EndToEnd.Commands;
using HarborStreet.EndToEnd.Domain;
using HarborStreet.EndToEnd.Events;
using HarborStreet.EndToEnd.Reactions;
using LawnDart.Aggregates;

namespace HarborStreet.EndToEnd.Payments;

public sealed class PaymentCaptureService
{
    private readonly IAggregateRepository _orders;
    private readonly IPaymentGateway _gateway;
    private readonly PaymentFollowOnReactions _reactions;

    public PaymentCaptureService(
        IAggregateRepository orders,
        IPaymentGateway gateway,
        PaymentFollowOnReactions reactions)
    {
        _orders = orders;
        _gateway = gateway;
        _reactions = reactions;
    }

    public async Task CaptureAsync(Guid orderId, decimal amount, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetOrCreateAsync<Order>(orderId, cancellationToken);
        if (!order.State.IsPlaced || order.State.IsCancelled || order.State.IsPaid)
            throw new InvalidOperationException("Order is not awaiting payment.");

        var gatewayResult = await _gateway.CaptureAsync(orderId, amount, cancellationToken);

        if (gatewayResult.Succeeded)
        {
            order = await _orders.GetOrCreateAsync<Order>(orderId, cancellationToken);
            await _orders.HandleCommandAsync(
                order,
                new CapturePaymentCommand(Guid.NewGuid(), orderId, gatewayResult.ReferenceOrReason),
                cancellationToken: cancellationToken);

            await _reactions.ReactAsync(
                new PaymentCaptured(Guid.NewGuid(), DateTime.UtcNow, orderId, gatewayResult.ReferenceOrReason),
                cancellationToken);
        }
        else
        {
            order = await _orders.GetOrCreateAsync<Order>(orderId, cancellationToken);
            await _orders.HandleCommandAsync(
                order,
                new RecordPaymentFailureCommand(Guid.NewGuid(), orderId, gatewayResult.ReferenceOrReason),
                cancellationToken: cancellationToken);

            await _reactions.ReactAsync(
                new PaymentFailed(Guid.NewGuid(), DateTime.UtcNow, orderId, gatewayResult.ReferenceOrReason),
                cancellationToken);
        }
    }
}
