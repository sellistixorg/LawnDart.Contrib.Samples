using LawnDart;

namespace LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

/// <summary>
/// Decision state for a customer order.
/// </summary>
public sealed partial class OrderState : IState
{
    /// <summary>
    /// Order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Shopper account id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).
    /// </summary>
    public Guid BuyerId { get; set; }

    /// <summary>
    /// Identity account id. Equals Identity AccountId (subject "sub" claim). Upstream keeps a separate int Buyer id and stores the subject here.
    /// </summary>
    public Guid IdentityGuid { get; set; }

    /// <summary>
    /// Buyer display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Submitted|AwaitingValidation|StockConfirmed|Paid|Shipped|Cancelled.
    /// </summary>
    public string OrderStatus { get; set; } = "";

    /// <summary>
    /// When placed.
    /// </summary>
    public DateTime? OrderDate { get; set; }

    /// <summary>
    /// Ship-to street.
    /// </summary>
    public string Street { get; set; } = "";

    /// <summary>
    /// Ship-to city.
    /// </summary>
    public string City { get; set; } = "";

    /// <summary>
    /// Ship-to state.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Ship-to country.
    /// </summary>
    public string Country { get; set; } = "";

    /// <summary>
    /// Ship-to postal code.
    /// </summary>
    public string ZipCode { get; set; } = "";

    /// <summary>
    /// Product lines.
    /// </summary>
    public List<OrderLine> OrderItems { get; } = [];

    /// <summary>
    /// Status description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Payment method id.
    /// </summary>
    public Guid PaymentMethodId { get; set; }

    /// <summary>
    /// Card type.
    /// </summary>
    public int CardTypeId { get; set; }

    /// <summary>
    /// Name on card.
    /// </summary>
    public string CardHolderName { get; set; } = "";

    /// <summary>
    /// Masked card number only. This slice stores the last four digits.
    /// </summary>
    public string MaskedCardNumber { get; set; } = "";

    /// <summary>
    /// Card expiration.
    /// </summary>
    public DateTime? CardExpiration { get; set; }

    /// <summary>
    /// Command / request id (idempotency). The order id is this request id.
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// True after Order Started is applied.
    /// </summary>
    public bool Started { get; set; }
}
