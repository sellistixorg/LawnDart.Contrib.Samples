using LawnDart;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

public sealed partial class Order
{
    /// <summary>
    /// Sets order status to AwaitingValidation and notifies Catalog.
    /// </summary>
    /// <param name="command">Mark Order Awaiting Validation.</param>
    /// <remarks>
    /// Only a <see cref="SubmittedStatus"/> order moves to <see cref="AwaitingValidationStatus"/>.
    /// Any other status writes no event.
    /// An unknown order is rejected with <c>ORDER_NOT_FOUND</c>.
    /// An empty order id is rejected with <c>ORDER_ID_REQUIRED</c>.
    /// An empty command id is rejected with <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// The command appends Order Awaiting Validation.
    /// Applying it sets the status to <see cref="AwaitingValidationStatus"/>.
    /// Order Awaiting Validation carries the order id, the buyer id, and each line's product id and units.
    /// </remarks>
    public void Handle(MarkOrderAwaitingValidationCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(RequestIdRequiredMessage, RequestIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (Version == 0)
            throw new DomainException(OrderNotFoundMessage, OrderNotFoundTitle);

        if (State.OrderStatus != SubmittedStatus)
            return;

        var now = DateTime.UtcNow;
        var stockItems = new List<OrderStockItem>(State.OrderItems.Count);
        foreach (var line in State.OrderItems)
            stockItems.Add(new OrderStockItem(line.ProductId, line.Units));

        Apply(new OrderAwaitingValidation(
            Guid.NewGuid(),
            now,
            command.OrderId,
            stockItems,
            State.BuyerId));
    }
}

public sealed partial class OrderState
{
    internal void Apply(OrderAwaitingValidation signaled)
    {
        OrderId = signaled.OrderId;
        BuyerId = signaled.BuyerId;
        OrderStatus = Order.AwaitingValidationStatus;
    }
}
