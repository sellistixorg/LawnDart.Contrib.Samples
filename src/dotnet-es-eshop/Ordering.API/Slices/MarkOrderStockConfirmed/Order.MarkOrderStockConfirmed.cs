using LawnDart;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

public sealed partial class Order
{
    /// <summary>
    /// All the items were confirmed with available stock.
    /// </summary>
    public const string StockConfirmedDescription = "All the items were confirmed with available stock.";

    /// <summary>
    /// Sets order status to StockConfirmed.
    /// </summary>
    /// <param name="command">Mark Order Stock Confirmed.</param>
    /// <remarks>
    /// Only an <see cref="AwaitingValidationStatus"/> order moves to <see cref="StockConfirmedStatus"/>.
    /// Any other status writes no event.
    /// An unknown order is rejected with <c>ORDER_NOT_FOUND</c>.
    /// An empty order id is rejected with <c>ORDER_ID_REQUIRED</c>.
    /// An empty command id is rejected with <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// The command appends Order Status Stock Confirmed.
    /// Applying it sets the status to <see cref="StockConfirmedStatus"/> and the status description to
    /// <see cref="StockConfirmedDescription"/>.
    /// Order Status Stock Confirmed carries the order id, the buyer id, the buyer name, and the order total.
    /// The order total is the sum of unit price times units.
    /// </remarks>
    public void Handle(MarkOrderStockConfirmedCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(RequestIdRequiredMessage, RequestIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (Version == 0)
            throw new DomainException(OrderNotFoundMessage, OrderNotFoundTitle);

        if (State.OrderStatus != AwaitingValidationStatus)
            return;

        decimal total = 0m;
        foreach (var line in State.OrderItems)
            total += line.UnitPrice * line.Units;

        Apply(new OrderStatusStockConfirmed(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.OrderId,
            State.BuyerId,
            total,
            State.Name));
    }
}

public sealed partial class OrderState
{
    internal void Apply(OrderStatusStockConfirmed confirmed)
    {
        OrderId = confirmed.OrderId;
        BuyerId = confirmed.BuyerId;
        Name = confirmed.BuyerName;
        OrderStatus = Order.StockConfirmedStatus;
        Description = Order.StockConfirmedDescription;
    }
}
