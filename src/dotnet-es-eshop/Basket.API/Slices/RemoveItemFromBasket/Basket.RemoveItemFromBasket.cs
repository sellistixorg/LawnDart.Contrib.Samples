using LawnDart;
using LawnDart.Samples.EShop.Basket.API.Slices.RemoveItemFromBasket;
using LawnDart.Samples.EShop.Basket.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

public sealed partial class Basket
{
    /// <summary>
    /// Removes a product line from the shopper basket.
    /// </summary>
    /// <param name="command">Remove Item From Basket.</param>
    /// <remarks>
    /// The line is removed entirely.
    /// Removing the last line leaves an empty basket. Add Item To Basket can use that basket again.
    /// Re-adding a removed product starts a fresh line with the new name, price, and picture.
    /// An unknown basket is rejected with <c>BASKET_NOT_FOUND</c>.
    /// A product that is not on the basket is rejected with <c>BASKET_ITEM_NOT_FOUND</c>.
    /// </remarks>
    public void Handle(RemoveItemFromBasketCommand command)
    {
        if (command.BuyerId == Guid.Empty)
            throw new DomainException("Buyer id is required.");

        if (command.ProductId == Guid.Empty)
            throw new DomainException("Product id is required.");

        if (Version == 0)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        if (Find(command.ProductId) is null)
            throw new DomainException(ItemNotFoundMessage, ItemNotFoundTitle);

        Apply(new BasketItemRemoved(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.BuyerId,
            command.ProductId));
    }
}

public sealed partial class BasketState
{
    internal void Apply(BasketItemRemoved removed)
    {
        BuyerId = removed.BuyerId;
        LastUpdated = removed.Timestamp;
        var removedIndex = IndexOf(removed.ProductId);
        if (removedIndex >= 0)
            Items.RemoveAt(removedIndex);
    }
}
