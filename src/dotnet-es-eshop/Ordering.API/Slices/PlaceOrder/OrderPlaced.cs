using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;

/// <summary>
/// Order created in Submitted status with lines and ship-to address.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When placed.</param>
/// <param name="OrderId">Order id.</param>
/// <param name="BuyerId">Buyer id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="UserName">Buyer user name.</param>
/// <param name="OrderStatus">Submitted.</param>
/// <param name="OrderItems">Order lines.</param>
/// <param name="Street">Ship-to street.</param>
/// <param name="City">Ship-to city.</param>
/// <param name="State">Ship-to state.</param>
/// <param name="Country">Ship-to country.</param>
/// <param name="ZipCode">Ship-to postal code.</param>
/// <param name="CardTypeId">Card type id.</param>
/// <param name="CardHolderName">Name on card.</param>
/// <param name="MaskedCardNumber">Masked card number only. The last four digits.</param>
/// <param name="CardExpiration">Card expiration.</param>
/// <param name="PaymentMethodId">Payment method id.</param>
[EventTypeName("order-placed")]
public sealed record OrderPlaced(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    Guid BuyerId,
    string UserName,
    string OrderStatus,
    IReadOnlyList<OrderLine> OrderItems,
    string Street,
    string City,
    string? State,
    string Country,
    string ZipCode,
    int CardTypeId,
    string CardHolderName,
    string MaskedCardNumber,
    DateTime CardExpiration,
    Guid PaymentMethodId) : IEvent;
