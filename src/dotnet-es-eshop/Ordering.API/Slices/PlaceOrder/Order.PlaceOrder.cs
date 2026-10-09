using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

public sealed partial class Order
{
    /// <summary>
    /// Order id must match the request id.
    /// </summary>
    public const string RequestIdMismatchMessage = "Order id must match the request id.";

    /// <summary>
    /// Error code <c>ORDER_REQUEST_ID_MISMATCH</c>.
    /// </summary>
    public const string RequestIdMismatchTitle = "ORDER_REQUEST_ID_MISMATCH";

    /// <summary>
    /// Buyer id is required.
    /// </summary>
    public const string BuyerIdRequiredMessage = "Buyer id is required.";

    /// <summary>
    /// Error code <c>ORDER_BUYER_ID_REQUIRED</c>.
    /// </summary>
    public const string BuyerIdRequiredTitle = "ORDER_BUYER_ID_REQUIRED";

    /// <summary>
    /// Buyer user name is required.
    /// </summary>
    public const string UserNameRequiredMessage = "Buyer user name is required.";

    /// <summary>
    /// Error code <c>ORDER_USER_NAME_REQUIRED</c>.
    /// </summary>
    public const string UserNameRequiredTitle = "ORDER_USER_NAME_REQUIRED";

    /// <summary>
    /// Ship-to street, city, country, and postal code are required.
    /// </summary>
    public const string AddressRequiredMessage =
        "Ship-to street, city, country, and postal code are required.";

    /// <summary>
    /// Error code <c>ORDER_ADDRESS_REQUIRED</c>.
    /// </summary>
    public const string AddressRequiredTitle = "ORDER_ADDRESS_REQUIRED";

    /// <summary>
    /// Order items are required.
    /// </summary>
    public const string ItemsEmptyMessage = "Order items are required.";

    /// <summary>
    /// Error code <c>ORDER_ITEMS_EMPTY</c>.
    /// </summary>
    public const string ItemsEmptyTitle = "ORDER_ITEMS_EMPTY";

    /// <summary>
    /// Card type id is required.
    /// </summary>
    public const string CardTypeRequiredMessage = "Card type id is required.";

    /// <summary>
    /// Error code <c>ORDER_CARD_TYPE_REQUIRED</c>.
    /// </summary>
    public const string CardTypeRequiredTitle = "ORDER_CARD_TYPE_REQUIRED";

    /// <summary>
    /// Name on card is required.
    /// </summary>
    public const string CardHolderRequiredMessage = "Name on card is required.";

    /// <summary>
    /// Error code <c>ORDER_CARD_HOLDER_REQUIRED</c>.
    /// </summary>
    public const string CardHolderRequiredTitle = "ORDER_CARD_HOLDER_REQUIRED";

    /// <summary>
    /// Last four digits of the card are required.
    /// </summary>
    public const string CardLastFourRequiredMessage = "Last four digits of the card are required.";

    /// <summary>
    /// Error code <c>ORDER_CARD_LAST_FOUR_REQUIRED</c>.
    /// </summary>
    public const string CardLastFourRequiredTitle = "ORDER_CARD_LAST_FOUR_REQUIRED";

    /// <summary>
    /// Card number must be the last four digits.
    /// </summary>
    public const string CardNumberRejectedMessage = "Card number must be the last four digits.";

    /// <summary>
    /// Error code <c>ORDER_CARD_NUMBER_REJECTED</c>.
    /// </summary>
    public const string CardNumberRejectedTitle = "ORDER_CARD_NUMBER_REJECTED";

    /// <summary>
    /// Card expiration is required.
    /// </summary>
    public const string CardExpirationRequiredMessage = "Card expiration is required.";

    /// <summary>
    /// Error code <c>ORDER_CARD_EXPIRATION_REQUIRED</c>.
    /// </summary>
    public const string CardExpirationRequiredTitle = "ORDER_CARD_EXPIRATION_REQUIRED";

    /// <summary>
    /// Product id is required.
    /// </summary>
    public const string ItemProductRequiredMessage = "Product id is required.";

    /// <summary>
    /// Error code <c>ORDER_ITEM_PRODUCT_REQUIRED</c>.
    /// </summary>
    public const string ItemProductRequiredTitle = "ORDER_ITEM_PRODUCT_REQUIRED";

    /// <summary>
    /// Product name is required.
    /// </summary>
    public const string ItemNameRequiredMessage = "Product name is required.";

    /// <summary>
    /// Error code <c>ORDER_ITEM_NAME_REQUIRED</c>.
    /// </summary>
    public const string ItemNameRequiredTitle = "ORDER_ITEM_NAME_REQUIRED";

    /// <summary>
    /// Unit price cannot be negative.
    /// </summary>
    public const string ItemPriceInvalidMessage = "Unit price cannot be negative.";

    /// <summary>
    /// Error code <c>ORDER_ITEM_PRICE_INVALID</c>.
    /// </summary>
    public const string ItemPriceInvalidTitle = "ORDER_ITEM_PRICE_INVALID";

    /// <summary>
    /// Units must be at least 1.
    /// </summary>
    public const string ItemUnitsInvalidMessage = "Units must be at least 1.";

    /// <summary>
    /// Error code <c>ORDER_ITEM_UNITS_INVALID</c>.
    /// </summary>
    public const string ItemUnitsInvalidTitle = "ORDER_ITEM_UNITS_INVALID";

    /// <summary>
    /// Creates order from basket lines, shipping address, and payment method (card masked in logs/events).
    /// </summary>
    /// <param name="command">Place Order.</param>
    /// <remarks>
    /// The order id is the client request id. <see cref="PlaceOrderCommand.OrderId"/> must equal
    /// <see cref="PlaceOrderCommand.CommandId"/>. A second command for an order that already exists
    /// writes no events.
    /// An empty request id is rejected with <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// An empty order id is rejected with <c>ORDER_ID_REQUIRED</c>.
    /// An order id that does not match the request id is rejected with <c>ORDER_REQUEST_ID_MISMATCH</c>.
    /// An empty buyer id is rejected with <c>ORDER_BUYER_ID_REQUIRED</c>.
    /// A blank buyer user name is rejected with <c>ORDER_USER_NAME_REQUIRED</c>.
    /// A missing ship-to street, city, country, or postal code is rejected with <c>ORDER_ADDRESS_REQUIRED</c>.
    /// Ship-to state is optional.
    /// An empty basket is rejected with <c>ORDER_ITEMS_EMPTY</c>.
    /// A line with an empty product id is rejected with <c>ORDER_ITEM_PRODUCT_REQUIRED</c>.
    /// A line with a blank name is rejected with <c>ORDER_ITEM_NAME_REQUIRED</c>.
    /// A negative unit price is rejected with <c>ORDER_ITEM_PRICE_INVALID</c>.
    /// Units below 1 are rejected with <c>ORDER_ITEM_UNITS_INVALID</c>.
    /// A card type below 1 is rejected with <c>ORDER_CARD_TYPE_REQUIRED</c>.
    /// A blank cardholder name is rejected with <c>ORDER_CARD_HOLDER_REQUIRED</c>.
    /// A blank card number is rejected with <c>ORDER_CARD_LAST_FOUR_REQUIRED</c>.
    /// Anything other than four digits is rejected with <c>ORDER_CARD_NUMBER_REJECTED</c>.
    /// A missing card expiration is rejected with <c>ORDER_CARD_EXPIRATION_REQUIRED</c>.
    /// The new order status is <see cref="SubmittedStatus"/>.
    /// The command stores the card type, the cardholder name, the expiry, and the last four digits.
    /// </remarks>
    public void Handle(PlaceOrderCommand command)
    {
        if (command.CommandId == Guid.Empty)
            throw new DomainException(RequestIdRequiredMessage, RequestIdRequiredTitle);

        if (command.OrderId == Guid.Empty)
            throw new DomainException(OrderIdRequiredMessage, OrderIdRequiredTitle);

        if (command.OrderId != command.CommandId)
            throw new DomainException(RequestIdMismatchMessage, RequestIdMismatchTitle);

        if (Version > 0)
            return;

        if (command.BuyerId == Guid.Empty)
            throw new DomainException(BuyerIdRequiredMessage, BuyerIdRequiredTitle);

        var userName = PlaceOrderChecks.RequireText(command.UserName, UserNameRequiredMessage, UserNameRequiredTitle);
        var street = PlaceOrderChecks.RequireText(command.Street, AddressRequiredMessage, AddressRequiredTitle);
        var city = PlaceOrderChecks.RequireText(command.City, AddressRequiredMessage, AddressRequiredTitle);
        var country = PlaceOrderChecks.RequireText(command.Country, AddressRequiredMessage, AddressRequiredTitle);
        var zipCode = PlaceOrderChecks.RequireText(command.ZipCode, AddressRequiredMessage, AddressRequiredTitle);
        var state = PlaceOrderChecks.BlankToNull(command.State);

        if (command.CardTypeId < 1)
            throw new DomainException(CardTypeRequiredMessage, CardTypeRequiredTitle);

        var cardHolder = PlaceOrderChecks.RequireText(command.CardHolderName, CardHolderRequiredMessage, CardHolderRequiredTitle);
        var lastFour = PlaceOrderChecks.RequireLastFour(command.MaskedCardNumber);
        if (command.CardExpiration == default)
            throw new DomainException(CardExpirationRequiredMessage, CardExpirationRequiredTitle);

        var lines = PlaceOrderChecks.RequireLines(command.OrderItems);
        var now = DateTime.UtcNow;
        var paymentMethodId = Guid.NewGuid();

        Apply(new OrderPlaced(
            Guid.NewGuid(),
            now,
            command.OrderId,
            command.BuyerId,
            userName,
            SubmittedStatus,
            lines,
            street,
            city,
            state,
            country,
            zipCode,
            command.CardTypeId,
            cardHolder,
            lastFour,
            command.CardExpiration,
            paymentMethodId));

        Apply(new OrderStarted(
            Guid.NewGuid(),
            now,
            command.OrderId,
            command.BuyerId));
    }
}

public sealed partial class OrderState
{
    internal void Apply(OrderPlaced placed)
    {
        OrderId = placed.OrderId;
        BuyerId = placed.BuyerId;
        IdentityGuid = placed.BuyerId;
        Name = placed.UserName;
        OrderStatus = placed.OrderStatus;
        OrderDate = placed.Timestamp;
        Street = placed.Street;
        City = placed.City;
        State = placed.State;
        Country = placed.Country;
        ZipCode = placed.ZipCode;
        OrderItems.Clear();
        foreach (var line in placed.OrderItems)
            OrderItems.Add(line);
        PaymentMethodId = placed.PaymentMethodId;
        CardTypeId = placed.CardTypeId;
        CardHolderName = placed.CardHolderName;
        MaskedCardNumber = placed.MaskedCardNumber;
        CardExpiration = placed.CardExpiration;
        RequestId = placed.OrderId;
    }

    internal void Apply(OrderStarted started)
    {
        OrderId = started.OrderId;
        BuyerId = started.BuyerId;
        Started = true;
    }
}

file static class PlaceOrderChecks
{
    public static List<OrderLine> RequireLines(IReadOnlyList<OrderLine>? items)
    {
        if (items is null || items.Count == 0)
            throw new DomainException(Order.ItemsEmptyMessage, Order.ItemsEmptyTitle);

        var lines = new List<OrderLine>(items.Count);
        foreach (var item in items)
        {
            if (item is null)
                throw new DomainException(Order.ItemsEmptyMessage, Order.ItemsEmptyTitle);

            if (item.ProductId == Guid.Empty)
                throw new DomainException(Order.ItemProductRequiredMessage, Order.ItemProductRequiredTitle);

            var name = item.ProductName?.Trim() ?? "";
            if (name.Length == 0)
                throw new DomainException(Order.ItemNameRequiredMessage, Order.ItemNameRequiredTitle);

            if (item.UnitPrice < 0)
                throw new DomainException(Order.ItemPriceInvalidMessage, Order.ItemPriceInvalidTitle);

            if (item.Units < 1)
                throw new DomainException(Order.ItemUnitsInvalidMessage, Order.ItemUnitsInvalidTitle);

            lines.Add(new OrderLine(item.ProductId, name, item.UnitPrice, item.Units));
        }

        return lines;
    }

    public static string RequireLastFour(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new DomainException(Order.CardLastFourRequiredMessage, Order.CardLastFourRequiredTitle);

        if (trimmed.Length == 4 && IsDigits(trimmed))
            return trimmed;

        throw new DomainException(Order.CardNumberRejectedMessage, Order.CardNumberRejectedTitle);
    }

    public static string RequireText(string? value, string message, string title)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new DomainException(message, title);

        return trimmed;
    }

    public static string? BlankToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsDigits(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsDigit(character))
                return false;
        }

        return true;
    }
}
