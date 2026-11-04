namespace HarborStreet.Projections.Views;

/// <summary>Product catalog row — what shoppers and staff browse.</summary>
public sealed class CatalogTitleView
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsScarce { get; set; }
}
