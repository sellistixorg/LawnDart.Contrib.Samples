using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;

public sealed class CatalogBrandState : IState
{
    public Guid BrandId { get; set; }

    public string Name { get; set; } = "";

    public bool Exists { get; set; }
}
