using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Sdk;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.API.Slices.ChangeBasketItemQuantity;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;
using LawnDart.Samples.EShop.Basket.API.Slices.RemoveItemFromBasket;

namespace LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;

/// <summary>
/// Projection of basket lines, quantities, price snapshots, and subtotal. Stream key: basket:{buyerId}
/// </summary>
/// <remarks>
/// Prices stay at the values captured on add. A quantity change does not replace the unit price or the picture.
/// Clear removes every line. An add after clear, or after the line was removed, starts a fresh line from that add.
/// Item count is the total units. Subtotal is the sum of unit price times quantity.
/// </remarks>
[SingleStreamProjection(Name, streamType: "Basket", tenantScope: TenantScope.TenantScoped)]
public sealed class BasketContentsProjection : ProjectionBase<BasketContentsView>
{
    /// <summary>
    /// Logical projection name. The view store key is this name plus <c>:v1</c>.
    /// </summary>
    public const string Name = "BasketContents";

    /// <summary>
    /// View store key for version 1 of <see cref="Name"/>.
    /// </summary>
    public const string StorageKey = Name + ":v1";

    /// <summary>
    /// View instance id for <paramref name="buyerId"/>. Stream key: basket:{buyerId}
    /// </summary>
    /// <param name="buyerId">Basket owner.</param>
    /// <returns>The stream id <c>Basket:{buyerId}</c>.</returns>
    public static string InstanceId(Guid buyerId) => $"Basket:{buyerId:D}";

    /// <summary>
    /// A line was added (or qty increased) on the shopper basket.
    /// </summary>
    /// <param name="added">Basket Item Added.</param>
    /// <remarks>
    /// The event quantity replaces the line quantity. The event carries the name, unit price, and picture to keep.
    /// </remarks>
    public void Handle(BasketItemAdded added)
    {
        EnsureItems();
        State.BuyerId = added.BuyerId;
        var line = new BasketContentsItem
        {
            ProductId = added.ProductId,
            ProductName = added.ProductName ?? "",
            UnitPrice = added.UnitPrice,
            Quantity = added.Quantity,
            PictureUrl = added.PictureUrl
        };
        var index = IndexOf(added.ProductId);
        if (index < 0)
            State.Items.Add(line);
        else
            State.Items[index] = line;

        ApplyTotals(State);
    }

    /// <summary>
    /// Basket line quantity was changed.
    /// </summary>
    /// <param name="changed">Basket Item Quantity Changed.</param>
    /// <remarks>
    /// The new quantity replaces the line quantity. Name, unit price, and picture stay as they were.
    /// </remarks>
    public void Handle(BasketItemQuantityChanged changed)
    {
        EnsureItems();
        State.BuyerId = changed.BuyerId;
        var index = IndexOf(changed.ProductId);
        if (index >= 0)
            State.Items[index].Quantity = changed.NewQuantity;

        ApplyTotals(State);
    }

    /// <summary>
    /// A product line was removed from the basket.
    /// </summary>
    /// <param name="removed">Basket Item Removed.</param>
    /// <remarks>
    /// Removing the last line leaves an empty basket.
    /// </remarks>
    public void Handle(BasketItemRemoved removed)
    {
        EnsureItems();
        State.BuyerId = removed.BuyerId;
        var index = IndexOf(removed.ProductId);
        if (index >= 0)
            State.Items.RemoveAt(index);

        ApplyTotals(State);
    }

    /// <summary>
    /// Shopper basket was emptied.
    /// </summary>
    /// <param name="cleared">Basket Cleared.</param>
    /// <remarks>
    /// Clearing removes every line. A later add starts a fresh line.
    /// </remarks>
    public void Handle(BasketCleared cleared)
    {
        EnsureItems();
        State.BuyerId = cleared.BuyerId;
        State.Items.Clear();
        ApplyTotals(State);
    }

    /// <summary>
    /// Writes item count, subtotal, and whether the basket has no lines.
    /// </summary>
    /// <param name="view">Basket contents.</param>
    /// <remarks>
    /// Item count is the total units across lines. Subtotal is the sum of unit price times quantity.
    /// A line total is that line's unit price times its quantity.
    /// </remarks>
    public static void ApplyTotals(BasketContentsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.Items ??= [];

        var count = 0;
        decimal subtotal = 0;
        foreach (var item in view.Items)
        {
            count += item.Quantity;
            subtotal += item.UnitPrice * item.Quantity;
        }

        view.ItemCount = count;
        view.Subtotal = subtotal;
        view.IsEmpty = view.Items.Count == 0;
    }

    private void EnsureItems() => State.Items ??= [];

    private int IndexOf(Guid productId)
    {
        for (var i = 0; i < State.Items.Count; i++)
        {
            if (State.Items[i].ProductId == productId)
                return i;
        }

        return -1;
    }
}
