using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Removes a catalog item from sale.
/// Namespace ends in <c>Commands</c> so HTTP maps
/// <c>POST /api/catalog/remove-catalog-item</c>.
/// </summary>
/// <param name="Id">Command identifier</param>
/// <param name="ItemId">Item to remove</param>
public sealed record RemoveCatalogItemCommand(
    Guid Id,
    Guid ItemId) : ICommand;
