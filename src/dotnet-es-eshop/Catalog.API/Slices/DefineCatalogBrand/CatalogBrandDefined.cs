using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;

[EventTypeName("catalog-brand-defined")]
public sealed record CatalogBrandDefined(
    Guid Id,
    DateTime Timestamp,
    Guid BrandId,
    string Name) : IEvent;
