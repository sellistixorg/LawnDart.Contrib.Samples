using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;
using LawnDart.Samples.EShop.Shared.Events;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrderSummary;

/// <summary>
/// Shopper read model for a single order detail as status progresses.
/// </summary>
[SingleStreamProjection(Name, streamType: "Order", tenantScope: TenantScope.TenantScoped)]
public sealed class OrderSummaryProjection : ProjectionBase<OrderSummaryView>
{
    /// <summary>
    /// Logical projection name. The view store key is this name plus <c>:v1</c>.
    /// </summary>
    public const string Name = "OrderSummary";

    /// <summary>
    /// View store key for version 1 of <see cref="Name"/>.
    /// </summary>
    public const string StorageKey = Name + ":v1";

    /// <summary>
    /// View instance id for <paramref name="orderId"/>.
    /// </summary>
    /// <param name="orderId">Order id.</param>
    /// <returns>The stream id <c>Order:{orderId}</c>.</returns>
    public static string InstanceId(Guid orderId) => $"Order:{orderId:D}";

    /// <summary>
    /// Order created in Submitted status with lines and ship-to address.
    /// </summary>
    /// <param name="placed">Order Placed.</param>
    /// <remarks>
    /// Sets the order id, the buyer id, the status, the placed time, the lines, and the ship-to address.
    /// The order total is the sum of unit price times units.
    /// Status text stays empty.
    /// </remarks>
    public void Handle(OrderPlaced placed)
    {
        State.OrderId = placed.OrderId;
        State.BuyerId = placed.BuyerId;
        State.OrderStatus = placed.OrderStatus;
        State.OrderDate = placed.Timestamp;
        State.OrderItems = CopyItems(placed.OrderItems);
        State.ShipTo = new OrderSummaryShipTo
        {
            Street = placed.Street,
            City = placed.City,
            State = placed.State,
            Country = placed.Country,
            ZipCode = placed.ZipCode
        };
        State.Total = Sum(State.OrderItems);
        State.Description = null;
    }

    /// <summary>
    /// Order awaiting stock validation. Catalog automation subscribes to this event.
    /// </summary>
    /// <param name="awaiting">Order Awaiting Validation.</param>
    /// <remarks>
    /// Sets the status to <see cref="Order.AwaitingValidationStatus"/>.
    /// Lines, total, ship-to, and status text stay as they were.
    /// </remarks>
    public void Handle(OrderAwaitingValidation awaiting)
    {
        State.OrderId = awaiting.OrderId;
        State.BuyerId = awaiting.BuyerId;
        State.OrderStatus = Order.AwaitingValidationStatus;
    }

    /// <summary>
    /// Stock is confirmed. The view status becomes StockConfirmed.
    /// </summary>
    /// <param name="confirmed">Order Status Stock Confirmed.</param>
    /// <remarks>
    /// Sets the status to <see cref="Order.StockConfirmedStatus"/>.
    /// Status text becomes <see cref="Order.StockConfirmedDescription"/>.
    /// Lines, total, and ship-to stay as they were.
    /// </remarks>
    public void Handle(OrderStatusStockConfirmed confirmed)
    {
        State.OrderId = confirmed.OrderId;
        State.BuyerId = confirmed.BuyerId;
        State.OrderStatus = Order.StockConfirmedStatus;
        State.Description = Order.StockConfirmedDescription;
    }

    /// <summary>
    /// Order paid. Catalog deduct automation subscribes to this event.
    /// </summary>
    /// <param name="paid">Order Paid.</param>
    /// <remarks>
    /// Sets the status to <see cref="Order.PaidStatus"/>.
    /// Status text becomes <see cref="Order.PaidDescription"/>.
    /// Lines, total, and ship-to stay as they were.
    /// </remarks>
    public void Handle(OrderPaid paid)
    {
        State.OrderId = paid.OrderId;
        State.BuyerId = paid.BuyerId;
        State.OrderStatus = Order.PaidStatus;
        State.Description = Order.PaidDescription;
    }

    /// <summary>
    /// Order was shipped.
    /// </summary>
    /// <param name="shipped">Order Shipped.</param>
    /// <remarks>
    /// Sets the status from the event.
    /// Status text becomes the event description.
    /// Lines, total, and ship-to stay as they were.
    /// </remarks>
    public void Handle(OrderShipped shipped)
    {
        State.OrderId = shipped.OrderId;
        State.OrderStatus = shipped.OrderStatus;
        State.Description = shipped.Description;
    }

    /// <summary>
    /// Order cancelled by shopper or operator.
    /// </summary>
    /// <param name="cancelled">Order Cancelled.</param>
    /// <remarks>
    /// Sets the status from the event.
    /// Status text becomes <see cref="Order.CancelledDescription"/>.
    /// Lines, total, and ship-to stay as they were.
    /// </remarks>
    public void Handle(OrderCancelled cancelled)
    {
        State.OrderId = cancelled.OrderId;
        State.OrderStatus = cancelled.OrderStatus;
        State.Description = Order.CancelledDescription;
    }

    /// <summary>
    /// Order cancelled due to insufficient stock.
    /// </summary>
    /// <param name="cancelled">Order Cancelled For Stock.</param>
    /// <remarks>
    /// Sets the status from the event.
    /// Status text lists the products that had no stock:
    /// <c>The product items don't have stock: (names).</c>
    /// Product names come from the order lines when the order has that product.
    /// A product id with no line is listed as the id.
    /// Lines, total, and ship-to stay as they were.
    /// </remarks>
    public void Handle(OrderCancelledForStock cancelled)
    {
        State.OrderId = cancelled.OrderId;
        State.OrderStatus = cancelled.OrderStatus;
        State.Description = StockRejectedDescription(State.OrderItems, cancelled.RejectedProductIds);
    }

    private static List<OrderSummaryItem> CopyItems(IReadOnlyList<OrderLine>? lines)
    {
        var items = new List<OrderSummaryItem>();
        if (lines is null)
            return items;

        foreach (var line in lines)
        {
            if (line is null)
                continue;

            items.Add(new OrderSummaryItem
            {
                ProductId = line.ProductId,
                ProductName = line.ProductName ?? "",
                UnitPrice = line.UnitPrice,
                Units = line.Units
            });
        }

        return items;
    }

    private static decimal Sum(IReadOnlyList<OrderSummaryItem> items)
    {
        decimal total = 0m;
        foreach (var item in items)
            total += item.UnitPrice * item.Units;

        return total;
    }

    private static string StockRejectedDescription(
        IReadOnlyList<OrderSummaryItem>? lines,
        IReadOnlyList<Guid>? rejectedProductIds)
    {
        var labels = new List<string>();
        if (rejectedProductIds is not null)
        {
            foreach (var productId in rejectedProductIds)
                labels.Add(ProductLabel(lines, productId));
        }

        return $"The product items don't have stock: ({string.Join(", ", labels)}).";
    }

    private static string ProductLabel(IReadOnlyList<OrderSummaryItem>? lines, Guid productId)
    {
        if (lines is not null)
        {
            foreach (var line in lines)
            {
                if (line.ProductId != productId)
                    continue;

                var name = line.ProductName?.Trim();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
        }

        return productId.ToString("D");
    }
}
