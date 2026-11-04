using HarborStreet.Catalog.Slice.Domain.Events;

namespace HarborStreet.Catalog.Slice.Domain;

/// <summary>
/// Every event type this slice can append or replay. Pass to WithEventTypes / CreateInMemory.
/// AI must update this list when (and only when) the Event Model adds a catalog event.
/// </summary>
// start snippet event-types
public static class CatalogEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(TitlePriceUpdated),
        typeof(TitleRestocked),
        typeof(TitleMarkedScarce)
    ];
// end snippet event-types
}
