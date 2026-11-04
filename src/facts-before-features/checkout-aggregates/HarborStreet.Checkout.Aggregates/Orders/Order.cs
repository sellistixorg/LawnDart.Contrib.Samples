using HarborStreet.Checkout.Aggregates.Orders.Commands;
using HarborStreet.Checkout.Aggregates.Orders.Events;
using LawnDart;
using LawnDart.Aggregates;

namespace HarborStreet.Checkout.Aggregates.Orders;

/// <summary>
/// Order aggregate — commitment facts for one checkout attempt.
/// Does not own Title stock; choreography must reserve separately.
/// </summary>
public class Order : AggregateRoot<OrderState>
{
    public Order()
    {
        State = new OrderState();
    }

    private Task Handle(PlaceOrderCommand cmd)
    {
        if (State.IsPlaced)
            throw new InvalidOperationException("Order is already placed.");

        if (State.IsCancelled)
            throw new InvalidOperationException("Order is cancelled.");

        if (cmd.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(cmd), "Quantity must be positive.");

        Apply(new OrderPlaced(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.OrderId,
            cmd.CustomerId,
            cmd.TitleId,
            cmd.Quantity));

        return Task.CompletedTask;
    }

    private Task Handle(CancelOrderForFailedReservationCommand cmd)
    {
        if (!State.IsPlaced)
            throw new InvalidOperationException("Order is not placed.");

        if (State.IsCancelled)
            throw new InvalidOperationException("Order is already cancelled.");

        Apply(new OrderCancelled(
            Guid.NewGuid(),
            DateTime.UtcNow,
            cmd.OrderId,
            cmd.Reason));

        return Task.CompletedTask;
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

            case OrderCancelled:
                State.IsCancelled = true;
                break;
        }
    }
}
