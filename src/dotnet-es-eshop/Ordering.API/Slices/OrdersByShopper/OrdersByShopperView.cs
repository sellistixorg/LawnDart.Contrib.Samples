namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;

/// <summary>
/// List of order summaries for the signed-in shopper.
/// </summary>
public sealed class OrdersByShopperView
{
    /// <summary>
    /// Shopper id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).
    /// </summary>
    public Guid BuyerId { get; set; }

    /// <summary>
    /// Order summary rows (id, date, status, total).
    /// </summary>
    public List<OrdersByShopperItem> Orders { get; set; } = [];

    /// <summary>
    /// An empty list for <paramref name="buyerId"/>.
    /// </summary>
    /// <param name="buyerId">Shopper id.</param>
    /// <returns>A list with that buyer id and no rows.</returns>
    public static OrdersByShopperView Empty(Guid buyerId) => new()
    {
        BuyerId = buyerId
    };
}

/// <summary>
/// Order summary rows (id, date, status, total).
/// </summary>
public sealed class OrdersByShopperItem
{
    /// <summary>
    /// Order id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// When placed.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Submitted|AwaitingValidation|StockConfirmed|Paid|Shipped|Cancelled.
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    /// Sum of unit price times units on the place event.
    /// </summary>
    public decimal Total { get; set; }
}
