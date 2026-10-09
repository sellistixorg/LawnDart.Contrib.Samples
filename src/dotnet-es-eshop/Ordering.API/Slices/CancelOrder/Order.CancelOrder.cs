using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

public sealed partial class Order
{
    /// <summary>
    /// The order was cancelled.
    /// </summary>
    public const string CancelledDescription = "The order was cancelled.";

    /// <summary>
    /// Order is Paid or Shipped and cannot be cancelled.
    /// </summary>
    public const string CannotCancelOrderMessage = "Order is Paid or Shipped and cannot be cancelled.";

    /// <summary>
    /// Error code <c>CANNOT_CANCEL_ORDER</c>.
    /// </summary>
    public const string CannotCancelOrderTitle = "CANNOT_CANCEL_ORDER";

    /// <summary>
    /// Cancels an order that is not yet Paid or Shipped.
    /// </summary>
    /// <param name="command">Cancel Order.</param>
    /// <remarks>
    /// A <see cref="PaidStatus"/> or <see cref="ShippedStatus"/> order is rejected with <c>CANNOT_CANCEL_ORDER</c>.
    /// An order that is already <see cref="CancelledStatus"/> writes no event.
    /// Any other status appends Order Cancelled and sets the status to <see cref="CancelledStatus"/>.
    /// The status description becomes <see cref="CancelledDescription"/>.
    /// An unknown order is rejected with <c>ORDER_NOT_FOUND</c>.
    /// An empty order id is rejected with <c>ORDER_ID_REQUIRED</c>.
    /// An empty command id is rejected with <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// Order Cancelled carries the order id, the status Cancelled, and the cancel reason.
    /// A blank reason is stored as no reason.
    /// </remarks>
    public void Handle(CancelOrderCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(RequestIdRequiredMessage, RequestIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (Version == 0)
            throw new DomainException(OrderNotFoundMessage, OrderNotFoundTitle);

        if (State.OrderStatus is PaidStatus or ShippedStatus)
            throw new DomainException(CannotCancelOrderMessage, CannotCancelOrderTitle);

        if (State.OrderStatus == CancelledStatus)
            return;

        Apply(new OrderCancelled(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.OrderId,
            CancelledStatus,
            CancelOrderText.ReasonOrNull(command.Reason)));
    }
}

public sealed partial class OrderState
{
    internal void Apply(OrderCancelled cancelled)
    {
        OrderId = cancelled.OrderId;
        OrderStatus = cancelled.OrderStatus;
        Description = Order.CancelledDescription;
    }
}

file static class CancelOrderText
{
    public static string? ReasonOrNull(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
