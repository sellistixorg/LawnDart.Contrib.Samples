using LawnDart;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;
using LawnDart.Samples.EShop.Basket.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

public sealed partial class Basket
{
    /// <summary>
    /// Deletes all lines for the shopper basket.
    /// </summary>
    /// <param name="command">Clear Basket.</param>
    /// <remarks>
    /// Clearing removes every line.
    /// A cleared basket can be used again by Add Item To Basket. Add starts fresh lines.
    /// Clearing a basket that is already empty writes no event.
    /// An unknown basket is rejected with <c>BASKET_NOT_FOUND</c>.
    /// An empty buyer id is rejected.
    /// </remarks>
    public void Handle(ClearBasketCommand command)
    {
        if (command.BuyerId == Guid.Empty)
            throw new DomainException("Buyer id is required.");

        if (Version == 0)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        if (State.Items.Count == 0)
            return;

        Apply(new BasketCleared(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.BuyerId,
            command.Reason));
    }
}

public sealed partial class BasketState
{
    internal void Apply(BasketCleared cleared)
    {
        BuyerId = cleared.BuyerId;
        LastUpdated = cleared.Timestamp;
        Items.Clear();
    }
}
