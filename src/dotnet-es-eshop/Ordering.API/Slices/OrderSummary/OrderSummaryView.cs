namespace LawnDart.Samples.EShop.Ordering.API.Slices.OrderSummary;

/// <summary>
/// Order detail projection for shopper and operator.
/// </summary>
public sealed class OrderSummaryView
{
    /// <summary>
    /// Order id.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Buyer id. BuyerId = Identity AccountId (the signed-in shopper's subject "sub" claim).
    /// </summary>
    public Guid BuyerId { get; set; }

    /// <summary>
    /// Current status.
    /// </summary>
    public string OrderStatus { get; set; } = "";

    /// <summary>
    /// Placed at.
    /// </summary>
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// Lines.
    /// </summary>
    public List<OrderSummaryItem> OrderItems { get; set; } = [];

    /// <summary>
    /// Address fields.
    /// </summary>
    public OrderSummaryShipTo? ShipTo { get; set; }

    /// <summary>
    /// Status text.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Order total.
    /// </summary>
    public decimal Total { get; set; }
}

/// <summary>
/// Lines.
/// </summary>
public sealed class OrderSummaryItem
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
    /// Unit price snapshot captured in the basket.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Units on the line.
    /// </summary>
    public int Units { get; set; }
}

/// <summary>
/// Address fields.
/// </summary>
public sealed class OrderSummaryShipTo
{
    /// <summary>
    /// Ship-to street.
    /// </summary>
    public string Street { get; set; } = "";

    /// <summary>
    /// Ship-to city.
    /// </summary>
    public string City { get; set; } = "";

    /// <summary>
    /// Ship-to state.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Ship-to country.
    /// </summary>
    public string Country { get; set; } = "";

    /// <summary>
    /// Ship-to postal code.
    /// </summary>
    public string ZipCode { get; set; } = "";
}
