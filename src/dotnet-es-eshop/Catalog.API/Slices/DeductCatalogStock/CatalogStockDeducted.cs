using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;

/// <summary>
/// Stock units were removed after a paid order.
/// </summary>
/// <param name="Id">Event identifier</param>
/// <param name="Timestamp">When deducted</param>
/// <param name="OrderId">Order id</param>
/// <param name="Deductions">Item id + units removed</param>
[EventTypeName("catalog-stock-deducted")]
public sealed record CatalogStockDeducted(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId,
    IReadOnlyList<CatalogStockDeduction> Deductions) : IEvent;

/// <summary>
/// Item id + units removed.
/// </summary>
/// <param name="ItemId">Item id</param>
/// <param name="UnitsRemoved">Units removed</param>
public sealed record CatalogStockDeduction(Guid ItemId, int UnitsRemoved);
