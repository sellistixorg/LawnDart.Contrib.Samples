using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;

public sealed class CatalogTypeState : IState
{
    public Guid TypeId { get; set; }

    public string Name { get; set; } = "";

    public bool Exists { get; set; }
}
