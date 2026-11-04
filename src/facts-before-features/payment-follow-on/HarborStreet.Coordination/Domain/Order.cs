using HarborStreet.Coordination.Commands;
using HarborStreet.Coordination.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Coordination.Domain;

/// <summary>
/// Order lifecycle after Place Order. Payment and fulfillment are follow-on facts—not part of checkout DCB.
/// </summary>
public class Order : AggregateRoot<OrderState>
{
    public Order() => State = new OrderState();

    private Task Handle(PlaceOrderCommand cmd)
    {
        if (State.IsPlaced)
            throw new InvalidOperationException("Order is already placed.");

        Apply(new OrderPlaced(
            Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId, cmd.CustomerId, cmd.TitleId, cmd.Quantity));
        return Task.CompletedTask;
    }

    private Task Handle(CapturePaymentCommand cmd)
    {
        EnsurePlacedAndOpen();
        if (State.IsPaid)
            throw new InvalidOperationException("Payment already captured.");

        Apply(new PaymentCaptured(Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId, cmd.PaymentReference));
        return Task.CompletedTask;
    }

    private Task Handle(RecordPaymentFailureCommand cmd)
    {
        EnsurePlacedAndOpen();
        if (State.IsPaid)
            throw new InvalidOperationException("Order is already paid.");

        Apply(new PaymentFailed(Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId, cmd.Reason));
        return Task.CompletedTask;
    }

    private Task Handle(CancelOrderForPaymentFailureCommand cmd)
    {
        if (!State.IsPlaced)
            throw new InvalidOperationException("Order is not placed.");
        if (State.IsCancelled)
            throw new InvalidOperationException("Order is already cancelled.");
        if (State.FulfillmentStarted)
            throw new InvalidOperationException("Order fulfillment already started.");
        if (!State.PaymentFailed)
            throw new InvalidOperationException("Order is not cancellable for payment failure.");

        Apply(new OrderCancelled(Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId, cmd.Reason));
        return Task.CompletedTask;
    }

    private Task Handle(StartFulfillmentCommand cmd)
    {
        if (!State.IsPlaced)
            throw new InvalidOperationException("Order is not placed.");
        if (State.IsCancelled)
            throw new InvalidOperationException("Order is cancelled.");
        if (!State.IsPaid)
            throw new InvalidOperationException("Order is not paid / not reserved for fulfillment.");
        if (State.FulfillmentStarted)
            throw new InvalidOperationException("Fulfillment already started.");

        Apply(new FulfillmentStarted(Guid.NewGuid(), DateTime.UtcNow, cmd.OrderId));
        return Task.CompletedTask;
    }

    private void EnsurePlacedAndOpen()
    {
        if (!State.IsPlaced)
            throw new InvalidOperationException("Order is not placed.");
        if (State.IsCancelled)
            throw new InvalidOperationException("Order is cancelled.");
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case OrderPlaced e:
                State.OrderId = e.OrderId;
                State.CustomerId = e.CustomerId;
                State.TitleId = e.TitleId;
                State.Quantity = e.Quantity;
                State.IsPlaced = true;
                break;
            case PaymentCaptured:
                State.IsPaid = true;
                break;
            case PaymentFailed:
                State.PaymentFailed = true;
                break;
            case OrderCancelled:
                State.IsCancelled = true;
                break;
            case FulfillmentStarted:
                State.FulfillmentStarted = true;
                break;
        }
    }
}
