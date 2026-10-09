using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;
using LawnDart.Samples.EShop.Shared.Events;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;

/// <summary>
/// List of order summaries for the signed-in shopper.
/// </summary>
/// <remarks>
/// One global fold holds every shopper. The orders page reads one shopper from that fold.
/// Rows are newest first. A later place with the same date sorts ahead of an earlier place.
/// The total is the sum of unit price times units on Order Placed. A later status event does not change it.
/// </remarks>
[GlobalProjection(Name)]
public sealed class OrdersByShopperProjection : ProjectionBase<OrdersByShopperState>
{
    /// <summary>
    /// Logical projection name. The view store key is this name plus <c>:v1</c>.
    /// </summary>
    public const string Name = "OrdersByShopper";

    /// <summary>
    /// View store key for version 1 of <see cref="Name"/>.
    /// </summary>
    public const string StorageKey = Name + ":v1";

    /// <summary>
    /// Order created in Submitted status with lines and ship-to address.
    /// </summary>
    /// <param name="placed">Order Placed.</param>
    /// <remarks>
    /// Adds a row for the order, or replaces the date, status, buyer, and total when the order is already listed.
    /// The total is the sum of unit price times units.
    /// </remarks>
    public void Handle(OrderPlaced placed)
    {
        EnsureOrders();
        var row = Find(placed.OrderId);
        if (row is null)
        {
            State.NextSequence++;
            row = new OrdersByShopperRow
            {
                Id = placed.OrderId,
                Sequence = State.NextSequence
            };
            State.Orders.Add(row);
        }

        row.BuyerId = placed.BuyerId;
        row.Date = placed.Timestamp;
        row.Status = placed.OrderStatus;
        row.Total = TotalOf(placed.OrderItems);
    }

    /// <summary>
    /// Order awaiting stock validation. Catalog validates stock on this signal.
    /// </summary>
    /// <param name="signaled">Order Awaiting Validation.</param>
    /// <remarks>
    /// Sets the matching row to <see cref="Order.AwaitingValidationStatus"/>.
    /// An unknown order leaves the list unchanged.
    /// A buyer id that does not match the row leaves the row unchanged.
    /// </remarks>
    public void Handle(OrderAwaitingValidation signaled) =>
        SetStatus(signaled.OrderId, Order.AwaitingValidationStatus, signaled.BuyerId);

    /// <summary>
    /// Stock is confirmed. The shopper order row becomes StockConfirmed.
    /// </summary>
    /// <param name="confirmed">Order Status Stock Confirmed.</param>
    /// <remarks>
    /// Sets the matching row to <see cref="Order.StockConfirmedStatus"/>.
    /// The total stays the sum from Order Placed.
    /// An unknown order leaves the list unchanged.
    /// A buyer id that does not match the row leaves the row unchanged.
    /// </remarks>
    public void Handle(OrderStatusStockConfirmed confirmed) =>
        SetStatus(confirmed.OrderId, Order.StockConfirmedStatus, confirmed.BuyerId);

    /// <summary>
    /// Order paid. Catalog deduct automation subscribes to this event.
    /// </summary>
    /// <param name="paid">Order Paid.</param>
    /// <remarks>
    /// Sets the matching row to <see cref="Order.PaidStatus"/>.
    /// An unknown order leaves the list unchanged.
    /// A buyer id that does not match the row leaves the row unchanged.
    /// </remarks>
    public void Handle(OrderPaid paid) =>
        SetStatus(paid.OrderId, Order.PaidStatus, paid.BuyerId);

    /// <summary>
    /// Order was shipped.
    /// </summary>
    /// <param name="shipped">Order Shipped.</param>
    /// <remarks>
    /// Sets the matching row to the status on the event.
    /// An unknown order leaves the list unchanged.
    /// </remarks>
    public void Handle(OrderShipped shipped) =>
        SetStatus(shipped.OrderId, shipped.OrderStatus, null);

    /// <summary>
    /// Order cancelled by shopper or operator.
    /// </summary>
    /// <param name="cancelled">Order Cancelled.</param>
    /// <remarks>
    /// Sets the matching row to the status on the event.
    /// An unknown order leaves the list unchanged.
    /// </remarks>
    public void Handle(OrderCancelled cancelled) =>
        SetStatus(cancelled.OrderId, cancelled.OrderStatus, null);

    /// <summary>
    /// Order cancelled due to insufficient stock.
    /// </summary>
    /// <param name="cancelled">Order Cancelled For Stock.</param>
    /// <remarks>
    /// Sets the matching row to the status on the event.
    /// An unknown order leaves the list unchanged.
    /// </remarks>
    public void Handle(OrderCancelledForStock cancelled) =>
        SetStatus(cancelled.OrderId, cancelled.OrderStatus, null);

    /// <summary>
    /// Rows for <paramref name="buyerId"/>, newest first.
    /// </summary>
    /// <param name="state">Folded rows for every shopper. Null is an empty list.</param>
    /// <param name="buyerId">Shopper id.</param>
    /// <returns>That shopper's rows. An unknown shopper gets an empty list.</returns>
    public static OrdersByShopperView ForBuyer(OrdersByShopperState? state, Guid buyerId)
    {
        var orders = new List<OrdersByShopperItem>();
        if (state?.Orders is null)
            return new OrdersByShopperView { BuyerId = buyerId, Orders = orders };

        var rows = state.Orders
            .Where(row => row.BuyerId == buyerId)
            .OrderByDescending(row => row.Date)
            .ThenByDescending(row => row.Sequence)
            .ToList();
        foreach (var row in rows)
        {
            orders.Add(new OrdersByShopperItem
            {
                Id = row.Id,
                Date = row.Date,
                Status = row.Status,
                Total = row.Total
            });
        }

        return new OrdersByShopperView { BuyerId = buyerId, Orders = orders };
    }

    private void SetStatus(Guid orderId, string status, Guid? buyerId)
    {
        if (string.IsNullOrWhiteSpace(status))
            return;

        EnsureOrders();
        var row = Find(orderId);
        if (row is null)
            return;

        if (buyerId is Guid buyer && buyer != Guid.Empty && row.BuyerId != buyer)
            return;

        row.Status = status;
    }

    private void EnsureOrders() => State.Orders ??= [];

    private OrdersByShopperRow? Find(Guid orderId)
    {
        for (var i = 0; i < State.Orders.Count; i++)
        {
            if (State.Orders[i].Id == orderId)
                return State.Orders[i];
        }

        return null;
    }

    private static decimal TotalOf(IReadOnlyList<OrderLine>? lines)
    {
        if (lines is null)
            return 0;

        decimal total = 0;
        foreach (var line in lines)
            total += line.UnitPrice * line.Units;

        return total;
    }
}

/// <summary>
/// Folded order rows for every shopper.
/// </summary>
public sealed class OrdersByShopperState
{
    /// <summary>
    /// Order summary rows (id, date, status, total), plus the shopper id on each row.
    /// </summary>
    public List<OrdersByShopperRow> Orders { get; set; } = [];

    /// <summary>
    /// Next placement sequence. A later place sorts ahead of an earlier place with the same date.
    /// </summary>
    public long NextSequence { get; set; }
}

/// <summary>
/// One order row in the shopper list.
/// </summary>
public sealed class OrdersByShopperRow
{
    /// <summary>
    /// Order id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Shopper id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).
    /// </summary>
    public Guid BuyerId { get; set; }

    /// <summary>
    /// When placed.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Submitted|AwaitingValidation|StockConfirmed|Paid|Shipped|Cancelled.
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    /// Sum of unit price times units on the place event.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Placement order. A later place sorts ahead of an earlier place with the same date.
    /// </summary>
    public long Sequence { get; set; }
}
