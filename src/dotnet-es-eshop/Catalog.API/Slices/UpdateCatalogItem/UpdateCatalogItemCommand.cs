using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Replaces catalog item details; emits Price Changed when price differs.
/// Namespace ends in <c>Commands</c> so HTTP maps
/// <c>POST /api/catalog/update-catalog-item</c>.
/// </summary>
/// <param name="Id">Command identifier</param>
/// <param name="ItemId">Item to update</param>
/// <param name="Name">Product name</param>
/// <param name="Price">Unit price</param>
/// <param name="BrandId">Brand id</param>
/// <param name="TypeId">Type id</param>
/// <param name="Description">Description</param>
/// <param name="PictureFileName">Picture file name</param>
public sealed record UpdateCatalogItemCommand(
    Guid Id,
    Guid ItemId,
    string Name,
    decimal Price,
    Guid BrandId,
    Guid TypeId,
    string? Description = null,
    string? PictureFileName = null) : ICommand;
