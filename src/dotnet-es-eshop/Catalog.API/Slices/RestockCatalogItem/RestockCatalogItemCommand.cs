using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Adds units to available stock up to MaxStockThreshold.
/// Namespace ends in <c>Commands</c> so HTTP maps
/// <c>POST /api/catalog/restock-catalog-item</c>.
/// </summary>
/// <param name="Id">Command identifier</param>
/// <param name="ItemId">Item to restock</param>
/// <param name="Quantity">Units to add</param>
public sealed record RestockCatalogItemCommand(
    Guid Id,
    Guid ItemId,
    int Quantity) : ICommand;
