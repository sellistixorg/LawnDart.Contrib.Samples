using HarborStreet.Coordination.Commands;
using HarborStreet.Coordination.Domain;
using HarborStreet.Coordination.Events;
using HarborStreet.Coordination.Payments;
using HarborStreet.Coordination.Reactions;
using LawnDart.Aggregates;

namespace HarborStreet.Coordination.Coordination;

/// <summary>
/// Follow-on Capture Payment: call gateway, record Payment Captured or Payment Failed, then run reactions.
/// </summary>
public sealed class PaymentCaptureService
{
    private readonly IAggregateRepository _repository;
    private readonly IPaymentGateway _gateway;
    private readonly PaymentFollowOnReactions _reactions;

    public PaymentCaptureService(
        IAggregateRepository repository,
        IPaymentGateway gateway,
        PaymentFollowOnReactions reactions)
    {
        _repository = repository;
        _gateway = gateway;
        _reactions = reactions;
    }

    public async Task CaptureAsync(Guid orderId, decimal amount, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetOrCreateAsync<Order>(orderId, cancellationToken);
        if (!order.State.IsPlaced || order.State.IsCancelled || order.State.IsPaid)
            throw new InvalidOperationException("Order is not awaiting payment.");

        var gatewayResult = await _gateway.CaptureAsync(orderId, amount, cancellationToken);

        if (gatewayResult.Succeeded)
        {
            order = await _repository.GetOrCreateAsync<Order>(orderId, cancellationToken);
            await _repository.HandleCommandAsync(
                order,
                new CapturePaymentCommand(Guid.NewGuid(), orderId, gatewayResult.ReferenceOrReason),
                cancellationToken: cancellationToken);

            await _reactions.ReactAsync(
                new PaymentCaptured(Guid.NewGuid(), DateTime.UtcNow, orderId, gatewayResult.ReferenceOrReason),
                cancellationToken);
        }
        else
        {
            order = await _repository.GetOrCreateAsync<Order>(orderId, cancellationToken);
            await _repository.HandleCommandAsync(
                order,
                new RecordPaymentFailureCommand(Guid.NewGuid(), orderId, gatewayResult.ReferenceOrReason),
                cancellationToken: cancellationToken);

            await _reactions.ReactAsync(
                new PaymentFailed(Guid.NewGuid(), DateTime.UtcNow, orderId, gatewayResult.ReferenceOrReason),
                cancellationToken);
        }
    }
}
