namespace LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;

/// <summary>
/// Lists every catalog brand and type with its display name, for the storefront filter lists and for API clients that add or update catalog items.
/// </summary>
public sealed class CatalogBrandsAndTypesView
{
    /// <summary>
    /// Brand id + brand name pairs.
    /// </summary>
    public List<CatalogBrandName> Brands { get; set; } = [];

    /// <summary>
    /// Type id + type name pairs.
    /// </summary>
    public List<CatalogTypeName> Types { get; set; } = [];
}

/// <summary>
/// Brand id + brand name pair.
/// </summary>
public sealed class CatalogBrandName
{
    /// <summary>
    /// Brand identifier.
    /// </summary>
    public Guid BrandId { get; set; }

    /// <summary>
    /// Brand display name.
    /// </summary>
    public string Name { get; set; } = "";
}

/// <summary>
/// Type id + type name pair.
/// </summary>
public sealed class CatalogTypeName
{
    /// <summary>
    /// Type identifier.
    /// </summary>
    public Guid TypeId { get; set; }

    /// <summary>
    /// Type display name.
    /// </summary>
    public string Name { get; set; } = "";
}
