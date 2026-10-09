using LawnDart;

namespace LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;

/// <summary>
/// Decision state for a shopper basket.
/// </summary>
public sealed partial class BasketState : IState
{
    /// <summary>
    /// Shopper account id owning the basket. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).
    /// </summary>
    public Guid BuyerId { get; set; }

    /// <summary>
    /// Basket line items (product, qty, price snapshot).
    /// </summary>
    public List<BasketItem> Items { get; } = [];

    /// <summary>
    /// Last modification time.
    /// </summary>
    public DateTime? LastUpdated { get; set; }

    /// <summary>
    /// Index of the line for <paramref name="productId"/>, or -1 when the basket has no such line.
    /// </summary>
    /// <param name="productId">Catalog item id.</param>
    /// <returns>The line index, or -1.</returns>
    internal int IndexOf(Guid productId)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].ProductId == productId)
                return i;
        }

        return -1;
    }
}
