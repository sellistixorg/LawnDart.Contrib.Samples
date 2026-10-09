namespace LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;

/// <summary>
/// Projection of basket lines, quantities, price snapshots, and subtotal.
/// </summary>
public sealed class BasketContentsView
{
    /// <summary>
    /// Basket owner. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).
    /// </summary>
    public Guid BuyerId { get; set; }

    /// <summary>
    /// Line items with product, qty, unit price, picture.
    /// </summary>
    public List<BasketContentsItem> Items { get; set; } = [];

    /// <summary>
    /// Total units across lines.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Sum of unit price × qty.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Whether the basket has no lines.
    /// </summary>
    public bool IsEmpty { get; set; } = true;

    /// <summary>
    /// An empty basket for <paramref name="buyerId"/>.
    /// </summary>
    /// <param name="buyerId">Basket owner.</param>
    /// <returns>A basket with no lines, a zero subtotal, and <see cref="IsEmpty"/> true.</returns>
    public static BasketContentsView Empty(Guid buyerId) => new()
    {
        BuyerId = buyerId
    };
}

/// <summary>
/// Line items with product, qty, unit price, picture.
/// </summary>
public sealed class BasketContentsItem
{
    /// <summary>
    /// Catalog item id.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Display name snapshot.
    /// </summary>
    public string ProductName { get; set; } = "";

    /// <summary>
    /// Unit price snapshot.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Quantity on the line.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Picture URL snapshot.
    /// </summary>
    public string? PictureUrl { get; set; }
}
