namespace LawnDart.Samples.EShop.WebApp.Models;

public class BasketItem
{
    public required string Id { get; set; }

    /// <summary>
    /// Item id.
    /// </summary>
    public Guid ProductId { get; set; }

    public required string ProductName { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal OldUnitPrice { get; set; }

    public int Quantity { get; set; }

    /// <summary>
    /// Picture URL snapshot.
    /// </summary>
    public string? PictureUrl { get; set; }
}
