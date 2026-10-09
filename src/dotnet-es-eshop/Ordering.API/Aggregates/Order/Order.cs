using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

/// <summary>
/// Customer order aggregate (eShop Ordering.Domain Order). Stream key: Order:{orderId}.
/// </summary>
public sealed partial class Order : AggregateRoot<OrderState>
{
    /// <summary>
    /// Submitted. eShop's initial order status.
    /// </summary>
    public const string SubmittedStatus = "Submitted";

    /// <summary>
    /// AwaitingValidation. Catalog checks stock while the order is in this status.
    /// </summary>
    public const string AwaitingValidationStatus = "AwaitingValidation";

    /// <summary>
    /// StockConfirmed. Payment charges while the order is in this status.
    /// </summary>
    public const string StockConfirmedStatus = "StockConfirmed";

    /// <summary>
    /// Paid.
    /// </summary>
    public const string PaidStatus = "Paid";

    /// <summary>
    /// Shipped.
    /// </summary>
    public const string ShippedStatus = "Shipped";

    /// <summary>
    /// Cancelled.
    /// </summary>
    public const string CancelledStatus = "Cancelled";

    /// <summary>
    /// Request id is required.
    /// </summary>
    public const string RequestIdRequiredMessage = "Request id is required.";

    /// <summary>
    /// Error code <c>ORDER_REQUEST_ID_REQUIRED</c>.
    /// </summary>
    public const string RequestIdRequiredTitle = "ORDER_REQUEST_ID_REQUIRED";

    /// <summary>
    /// Order id is required.
    /// </summary>
    public const string OrderIdRequiredMessage = "Order id is required.";

    /// <summary>
    /// Error code <c>ORDER_ID_REQUIRED</c>.
    /// </summary>
    public const string OrderIdRequiredTitle = "ORDER_ID_REQUIRED";

    /// <summary>
    /// Order was not found.
    /// </summary>
    public const string OrderNotFoundMessage = "Order was not found.";

    /// <summary>
    /// Error code <c>ORDER_NOT_FOUND</c>.
    /// </summary>
    public const string OrderNotFoundTitle = "ORDER_NOT_FOUND";

    /// <inheritdoc />
    protected override void ApplyEventToState(IEvent @event) => EShopStateApplier.Apply(State, @event);
}
