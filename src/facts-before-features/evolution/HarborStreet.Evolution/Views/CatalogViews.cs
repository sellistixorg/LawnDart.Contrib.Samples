namespace HarborStreet.Evolution.Views;

/// <summary>Catalog projection family v1 — name and price only.</summary>
public sealed class CatalogTitleViewV1
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>Catalog projection family v2 — adds scarcity label for staff UX.</summary>
public sealed class CatalogTitleViewV2
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsScarce { get; set; }
    public string ScarcityLabel { get; set; } = "Ordinary";
}
