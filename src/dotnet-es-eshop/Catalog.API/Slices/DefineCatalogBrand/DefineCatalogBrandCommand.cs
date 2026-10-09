using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Defines a catalog brand. Namespace ends in <c>Commands</c> so HTTP maps
/// <c>POST /api/catalog/define-catalog-brand</c>.
/// </summary>
public sealed record DefineCatalogBrandCommand(Guid Id, Guid BrandId, string Name) : ICommand;
