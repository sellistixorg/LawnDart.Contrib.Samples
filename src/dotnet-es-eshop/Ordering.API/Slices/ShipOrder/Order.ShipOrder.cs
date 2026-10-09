using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;
using LawnDart.Samples.EShop.Ordering.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

public sealed partial class Order
{
    /// <summary>
    /// The order was shipped.
    /// </summary>
    public const string ShippedDescription = "The order was shipped.";

    /// <summary>
    /// Order is not in Paid status.
    /// </summary>
    public const string CannotShipOrderMessage = "Order is not in Paid status.";

    /// <summary>
    /// Error code <c>CANNOT_SHIP_ORDER</c>.
    /// </summary>
    public const string CannotShipOrderTitle = "CANNOT_SHIP_ORDER";

    /// <summary>
    /// Marks a paid order as shipped.
    /// </summary>
    /// <param name="command">Ship Order.</param>
    /// <remarks>
    /// Only a <see cref="PaidStatus"/> order moves to <see cref="ShippedStatus"/>.
    /// Any other status is rejected with <c>CANNOT_SHIP_ORDER</c>.
    /// An unknown order is rejected with <c>ORDER_NOT_FOUND</c>.
    /// An empty order id is rejected with <c>ORDER_ID_REQUIRED</c>.
    /// An empty command id is rejected with <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// The command appends Order Shipped.
    /// Applying it sets the status to <see cref="ShippedStatus"/> and the status description to
    /// <see cref="ShippedDescription"/>.
    /// Order Shipped carries the order id, the status Shipped, and the description.
    /// </remarks>
    public void Handle(ShipOrderCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(RequestIdRequiredMessage, RequestIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (Version == 0)
            throw new DomainException(OrderNotFoundMessage, OrderNotFoundTitle);

        if (State.OrderStatus != PaidStatus)
            throw new DomainException(CannotShipOrderMessage, CannotShipOrderTitle);

        Apply(new OrderShipped(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.OrderId,
            ShippedStatus,
            ShippedDescription));
    }
}

public sealed partial class OrderState
{
    internal void Apply(OrderShipped shipped)
    {
        OrderId = shipped.OrderId;
        OrderStatus = shipped.OrderStatus;
        Description = shipped.Description;
    }
}
