using LawnDart;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

public sealed partial class Order
{
    /// <summary>
    /// The payment was performed at a simulated "American Bank checking bank account ending on XX35071".
    /// </summary>
    public const string PaidDescription =
        "The payment was performed at a simulated \"American Bank checking bank account ending on XX35071\"";

    /// <summary>
    /// Sets order status to Paid and signals Catalog to deduct stock.
    /// </summary>
    /// <param name="command">Mark Order Paid.</param>
    /// <remarks>
    /// Only a <see cref="StockConfirmedStatus"/> order moves to <see cref="PaidStatus"/>.
    /// Any other status writes no event. A repeat while the order is already Paid writes no event.
    /// An unknown order is rejected with <c>ORDER_NOT_FOUND</c>.
    /// An empty order id is rejected with <c>ORDER_ID_REQUIRED</c>.
    /// An empty command id is rejected with <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// The command appends Order Paid.
    /// Applying it sets the status to <see cref="PaidStatus"/> and the status description to
    /// <see cref="PaidDescription"/>.
    /// Order Paid carries the order id, the buyer id, and each line's product id and units.
    /// </remarks>
    public void Handle(MarkOrderPaidCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(RequestIdRequiredMessage, RequestIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (Version == 0)
            throw new DomainException(OrderNotFoundMessage, OrderNotFoundTitle);

        if (State.OrderStatus != StockConfirmedStatus)
            return;

        var stockItems = new List<OrderStockItem>(State.OrderItems.Count);
        foreach (var line in State.OrderItems)
            stockItems.Add(new OrderStockItem(line.ProductId, line.Units));

        Apply(new OrderPaid(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.OrderId,
            stockItems,
            State.BuyerId));
    }
}

public sealed partial class OrderState
{
    internal void Apply(OrderPaid paid)
    {
        OrderId = paid.OrderId;
        BuyerId = paid.BuyerId;
        OrderStatus = Order.PaidStatus;
        Description = Order.PaidDescription;
    }
}
