using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Shared.Events;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;

/// <summary>
/// One stock check for an order. Stream key: OrderStockValidation:{orderId}.
/// </summary>
public sealed class OrderStockValidation : AggregateRoot<OrderStockValidationState>
{
    /// <summary>
    /// An empty command id is rejected.
    /// </summary>
    public const string CommandIdRequiredMessage = "Command identifier is required.";

    /// <summary>
    /// Error code <c>ORDER_STOCK_COMMAND_ID_REQUIRED</c>.
    /// </summary>
    public const string CommandIdRequiredTitle = "ORDER_STOCK_COMMAND_ID_REQUIRED";

    /// <summary>
    /// An empty order id is rejected.
    /// </summary>
    public const string OrderIdRequiredMessage = "Order id is required.";

    /// <summary>
    /// Error code <c>ORDER_STOCK_ORDER_ID_REQUIRED</c>.
    /// </summary>
    public const string OrderIdRequiredTitle = "ORDER_STOCK_ORDER_ID_REQUIRED";

    /// <summary>
    /// A missing or empty line list is rejected.
    /// </summary>
    public const string ItemsRequiredMessage = "Order stock items are required.";

    /// <summary>
    /// Error code <c>ORDER_STOCK_ITEMS_REQUIRED</c>.
    /// </summary>
    public const string ItemsRequiredTitle = "ORDER_STOCK_ITEMS_REQUIRED";

    /// <summary>
    /// An empty product id is rejected.
    /// </summary>
    public const string ProductIdRequiredMessage = "Product id is required.";

    /// <summary>
    /// Error code <c>ORDER_STOCK_PRODUCT_ID_REQUIRED</c>.
    /// </summary>
    public const string ProductIdRequiredTitle = "ORDER_STOCK_PRODUCT_ID_REQUIRED";

    /// <summary>
    /// A line quantity of zero or less is rejected.
    /// </summary>
    public const string UnitsRequiredMessage = "Item units requested should be greater than zero.";

    /// <summary>
    /// Error code <c>ORDER_STOCK_UNITS_INVALID</c>.
    /// </summary>
    public const string UnitsInvalidTitle = "ORDER_STOCK_UNITS_INVALID";

    /// <summary>
    /// Stream id for <paramref name="orderId"/>.
    /// </summary>
    /// <param name="orderId">Order being validated.</param>
    /// <returns>The stream id <c>OrderStockValidation:{orderId}</c>.</returns>
    public static string StreamName(Guid orderId) => $"OrderStockValidation:{orderId}";

    /// <summary>
    /// Checks AvailableStock for each order line; confirms or rejects the order stock check.
    /// </summary>
    /// <param name="command">Validate Order Stock command.</param>
    /// <remarks>
    /// A checked order writes no event. The first result stays.
    /// <see cref="ValidateOrderStockCommand.ConfirmedOrderStockItems"/> is the summed stock check.
    /// Every flag true appends Order Stock Confirmed.
    /// Any flag false appends Order Stock Rejected with those flags.
    /// This command does not change available stock.
    /// An empty command id is rejected with <c>ORDER_STOCK_COMMAND_ID_REQUIRED</c>.
    /// An empty order id is rejected with <c>ORDER_STOCK_ORDER_ID_REQUIRED</c>.
    /// A missing or empty line list is rejected with <c>ORDER_STOCK_ITEMS_REQUIRED</c>.
    /// An empty product id is rejected with <c>ORDER_STOCK_PRODUCT_ID_REQUIRED</c>.
    /// A quantity of zero or less is rejected with <c>ORDER_STOCK_UNITS_INVALID</c>.
    /// </remarks>
    public void Handle(ValidateOrderStockCommand command)
    {
        Require(command);
        if (State.Checked)
            return;

        if (command.ConfirmedOrderStockItems is not { Count: > 0 } lines)
        {
            throw new InvalidOperationException(
                "Confirmed order stock items must be set before the result is stored.");
        }

        var anyShort = false;
        foreach (var line in lines)
        {
            if (!line.HasStock)
                anyShort = true;
        }

        var now = DateTime.UtcNow;
        if (anyShort)
        {
            Apply(new OrderStockRejected(
                Guid.NewGuid(),
                now,
                command.OrderId,
                lines));
            return;
        }

        Apply(new OrderStockConfirmed(
            Guid.NewGuid(),
            now,
            command.OrderId));
    }

    /// <summary>
    /// Rejects an empty command id, an empty order id, an empty line list, an empty product id, or a non-positive quantity.
    /// </summary>
    /// <param name="command">Validate Order Stock command.</param>
    public static void Require(ValidateOrderStockCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(CommandIdRequiredMessage, CommandIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (command.OrderStockItems is null || command.OrderStockItems.Count == 0)
            throw new DomainException(ItemsRequiredMessage, ItemsRequiredTitle);

        foreach (var line in command.OrderStockItems)
        {
            if (line is null)
                throw new DomainException(ItemsRequiredMessage, ItemsRequiredTitle);

            if (line.ProductId == Guid.Empty)
                throw new DomainException(ProductIdRequiredMessage, ProductIdRequiredTitle);

            if (line.Units <= 0)
                throw new DomainException(UnitsRequiredMessage, UnitsInvalidTitle);
        }
    }

    /// <inheritdoc />
    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case OrderStockConfirmed confirmed:
                State.OrderId = confirmed.OrderId;
                State.Checked = true;
                State.Confirmed = true;
                break;
            case OrderStockRejected rejected:
                State.OrderId = rejected.OrderId;
                State.Checked = true;
                State.Confirmed = false;
                break;
        }
    }
}
