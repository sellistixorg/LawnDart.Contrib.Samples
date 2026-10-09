using LawnDart;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;

namespace LawnDart.Samples.EShop.Ordering.Commands;

/// <summary>
/// Creates order from basket lines, shipping address, and payment method (card masked in logs/events).
/// </summary>
/// <remarks>
/// POST /api/ordering/place-order.
/// The order id is the client request id. <see cref="OrderId"/> must equal <see cref="CommandId"/>.
/// </remarks>
/// <param name="CommandId">Command / request id (idempotency).</param>
/// <param name="OrderId">New order id. Equals <paramref name="CommandId"/>.</param>
/// <param name="BuyerId">Shopper account id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="UserName">Buyer user name.</param>
/// <param name="Street">Ship-to street.</param>
/// <param name="City">Ship-to city.</param>
/// <param name="State">Ship-to state.</param>
/// <param name="Country">Ship-to country.</param>
/// <param name="ZipCode">Ship-to postal code.</param>
/// <param name="CardTypeId">Card type id.</param>
/// <param name="CardHolderName">Name on card.</param>
/// <param name="MaskedCardNumber">Masked card number only. The last four digits.</param>
/// <param name="CardExpiration">Card expiration.</param>
/// <param name="OrderItems">Basket lines (product, name, price, units).</param>
public sealed record PlaceOrderCommand(
    Guid CommandId,
    Guid OrderId,
    Guid BuyerId,
    string UserName,
    string Street,
    string City,
    string? State,
    string Country,
    string ZipCode,
    int CardTypeId,
    string CardHolderName,
    string MaskedCardNumber,
    DateTime CardExpiration,
    IReadOnlyList<OrderLine> OrderItems) : ICommand
{
    /// <summary>
    /// Command / request id (idempotency).
    /// </summary>
    public Guid Id => CommandId;
}
