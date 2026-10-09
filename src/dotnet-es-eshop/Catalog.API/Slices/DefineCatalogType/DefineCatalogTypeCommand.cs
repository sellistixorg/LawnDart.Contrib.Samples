using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Defines a catalog type. Namespace ends in <c>Commands</c> so HTTP maps
/// <c>POST /api/catalog/define-catalog-type</c>.
/// </summary>
public sealed record DefineCatalogTypeCommand(Guid Id, Guid TypeId, string Name) : ICommand;
