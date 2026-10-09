using LawnDart;
using LawnDart.Samples.EShop.Basket.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

public sealed partial class Basket
{
    /// <summary>
    /// Adds a product line to the shopper basket with quantity and price snapshot.
    /// </summary>
    /// <param name="command">Add Item To Basket.</param>
    /// <remarks>
    /// A product already in the basket increases that line's quantity.
    /// The name, price, and picture stay as they were on the first add.
    /// The event quantity is the quantity after the add.
    /// Quantity must be at least 1 (eShop BasketItem validation).
    /// A quantity below 1, or a summed quantity that overflows <c>int</c>, is rejected with <c>BASKET_QUANTITY_INVALID</c>.
    /// A negative unit price is rejected.
    /// </remarks>
    public void Handle(AddItemToBasketCommand command)
    {
        if (command.BuyerId == Guid.Empty)
            throw new DomainException("Buyer id is required.");

        if (command.ProductId == Guid.Empty)
            throw new DomainException("Product id is required.");

        var name = command.ProductName?.Trim() ?? "";
        if (name.Length == 0)
            throw new DomainException("Product name is required.");

        if (command.UnitPrice < 0)
            throw new DomainException("Unit price cannot be negative.");

        if (command.Quantity < 1)
            throw new DomainException(QuantityInvalidMessage, QuantityInvalidTitle);

        var existing = Find(command.ProductId);
        var productName = existing?.ProductName ?? name;
        var unitPrice = existing?.UnitPrice ?? command.UnitPrice;
        var pictureUrl = existing is null ? AddItemChecks.BlankToNull(command.PictureUrl) : existing.PictureUrl;
        var quantity = existing is null
            ? command.Quantity
            : AddItemChecks.SumQuantity(existing.Quantity, command.Quantity);

        Apply(new BasketItemAdded(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.BuyerId,
            command.ProductId,
            productName,
            unitPrice,
            quantity,
            pictureUrl));
    }
}

public sealed partial class BasketState
{
    internal void Apply(BasketItemAdded added)
    {
        BuyerId = added.BuyerId;
        LastUpdated = added.Timestamp;
        var line = new BasketItem(
            added.ProductId,
            added.ProductName,
            added.UnitPrice,
            added.Quantity,
            added.PictureUrl);
        var index = IndexOf(added.ProductId);
        if (index < 0)
            Items.Add(line);
        else
            Items[index] = line;
    }
}

file static class AddItemChecks
{
    public static int SumQuantity(int current, int added)
    {
        try
        {
            return checked(current + added);
        }
        catch (OverflowException)
        {
            throw new DomainException(Basket.QuantityInvalidMessage, Basket.QuantityInvalidTitle);
        }
    }

    public static string? BlankToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
