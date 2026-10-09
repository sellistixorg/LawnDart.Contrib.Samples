using LawnDart;
using LawnDart.Samples.EShop.Basket.API.Slices.ChangeBasketItemQuantity;
using LawnDart.Samples.EShop.Basket.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

public sealed partial class Basket
{
    /// <summary>
    /// Sets quantity for a product already in the basket.
    /// </summary>
    /// <param name="command">Change Basket Item Quantity.</param>
    /// <remarks>
    /// Quantity must be at least 1 (eShop BasketItem validation).
    /// A quantity below 1, including 0, is rejected with <c>BASKET_QUANTITY_INVALID</c>.
    /// A quantity of 0 does not remove the line.
    /// An unknown basket is rejected with <c>BASKET_NOT_FOUND</c>.
    /// A product that is not on the basket is rejected with <c>BASKET_ITEM_NOT_FOUND</c>.
    /// The new quantity replaces the line quantity. Name, price, and picture stay as they were.
    /// When the new quantity equals the current quantity, the command writes no event.
    /// </remarks>
    public void Handle(ChangeBasketItemQuantityCommand command)
    {
        if (command.BuyerId == Guid.Empty)
            throw new DomainException("Buyer id is required.");

        if (command.ProductId == Guid.Empty)
            throw new DomainException("Product id is required.");

        if (Version == 0)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        var existing = Find(command.ProductId);
        if (existing is null)
            throw new DomainException(ItemNotFoundMessage, ItemNotFoundTitle);

        if (command.Quantity < 1)
            throw new DomainException(QuantityInvalidMessage, QuantityInvalidTitle);

        if (command.Quantity == existing.Quantity)
            return;

        Apply(new BasketItemQuantityChanged(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.BuyerId,
            command.ProductId,
            existing.Quantity,
            command.Quantity));
    }
}

public sealed partial class BasketState
{
    internal void Apply(BasketItemQuantityChanged changed)
    {
        BuyerId = changed.BuyerId;
        LastUpdated = changed.Timestamp;
        var changedIndex = IndexOf(changed.ProductId);
        if (changedIndex >= 0)
        {
            Items[changedIndex] = Items[changedIndex] with
            {
                Quantity = changed.NewQuantity
            };
        }
    }
}
