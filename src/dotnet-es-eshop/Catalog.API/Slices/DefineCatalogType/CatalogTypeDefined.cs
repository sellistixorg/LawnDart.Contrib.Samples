using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;

[EventTypeName("catalog-type-defined")]
public sealed record CatalogTypeDefined(
    Guid Id,
    DateTime Timestamp,
    Guid TypeId,
    string Name) : IEvent;
