using LawnDart;

namespace LawnDart.Samples.EShop.Basket.Commands;

/// <summary>
/// Deletes all lines for the shopper basket.
/// </summary>
/// <remarks>
/// POST /api/basket/clear-basket.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="BuyerId">Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).</param>
/// <param name="Reason">UserCleared | OrderStarted | Other.</param>
public sealed record ClearBasketCommand(
    Guid Id,
    Guid BuyerId,
    string? Reason = null) : ICommand;
