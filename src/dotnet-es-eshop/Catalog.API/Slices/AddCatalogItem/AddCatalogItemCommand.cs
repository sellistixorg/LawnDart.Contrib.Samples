using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Adds a catalog item. Namespace ends in <c>Commands</c> so HTTP maps
/// <c>POST /api/catalog/add-catalog-item</c>.
/// </summary>
public sealed record AddCatalogItemCommand(
    Guid Id,
    Guid ItemId,
    string Name,
    decimal Price,
    Guid BrandId,
    Guid TypeId,
    int AvailableStock,
    string? Description = null,
    string? PictureFileName = null,
    int? RestockThreshold = null,
    int? MaxStockThreshold = null) : ICommand;
