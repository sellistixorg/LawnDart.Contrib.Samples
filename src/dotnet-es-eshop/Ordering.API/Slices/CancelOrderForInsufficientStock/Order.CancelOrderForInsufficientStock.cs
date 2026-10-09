using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

public sealed partial class Order
{
    /// <summary>
    /// Cancels order because stock validation failed.
    /// </summary>
    /// <param name="command">Cancel Order For Insufficient Stock.</param>
    /// <remarks>
    /// Only an <see cref="AwaitingValidationStatus"/> order moves to <see cref="CancelledStatus"/>.
    /// Any other status writes no event.
    /// An unknown order is rejected with <c>ORDER_NOT_FOUND</c>.
    /// An empty order id is rejected with <c>ORDER_ID_REQUIRED</c>.
    /// An empty command id is rejected with <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// The command appends Order Cancelled For Stock.
    /// Applying it sets the status to <see cref="CancelledStatus"/>.
    /// The status description lists the products that had no stock:
    /// <c>The product items don't have stock: (names).</c>
    /// Product names come from the order lines when the order has that product.
    /// A product id with no line is listed as the id.
    /// Order Cancelled For Stock carries the order id, the rejected product ids, and the status Cancelled.
    /// </remarks>
    public void Handle(CancelOrderForInsufficientStockCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(RequestIdRequiredMessage, RequestIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (Version == 0)
            throw new DomainException(OrderNotFoundMessage, OrderNotFoundTitle);

        if (State.OrderStatus != AwaitingValidationStatus)
            return;

        var rejected = CancelOrderStock.CopyProductIds(command.RejectedProductIds);
        Apply(new OrderCancelledForStock(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.OrderId,
            rejected,
            CancelledStatus));
    }
}

public sealed partial class OrderState
{
    internal void Apply(OrderCancelledForStock cancelled)
    {
        OrderId = cancelled.OrderId;
        OrderStatus = cancelled.OrderStatus;
        Description = CancelOrderStock.StockRejectedDescription(OrderItems, cancelled.RejectedProductIds);
    }
}

file static class CancelOrderStock
{
    public static string StockRejectedDescription(
        IReadOnlyList<OrderLine> lines,
        IReadOnlyList<Guid> rejectedProductIds)
    {
        var labels = new List<string>(rejectedProductIds.Count);
        foreach (var productId in rejectedProductIds)
            labels.Add(ProductLabel(lines, productId));

        return $"The product items don't have stock: ({string.Join(", ", labels)}).";
    }

    public static IReadOnlyList<Guid> CopyProductIds(IReadOnlyList<Guid>? rejectedProductIds)
    {
        if (rejectedProductIds is null || rejectedProductIds.Count == 0)
            return [];

        var copy = new Guid[rejectedProductIds.Count];
        for (var i = 0; i < rejectedProductIds.Count; i++)
            copy[i] = rejectedProductIds[i];

        return copy;
    }

    private static string ProductLabel(IReadOnlyList<OrderLine> lines, Guid productId)
    {
        foreach (var line in lines)
        {
            if (line.ProductId != productId)
                continue;

            var name = line.ProductName?.Trim();
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return productId.ToString("D");
    }
}
