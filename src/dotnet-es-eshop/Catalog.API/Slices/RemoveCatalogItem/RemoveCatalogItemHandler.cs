using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;

/// <summary>
/// Loads the catalog item and handles <see cref="RemoveCatalogItemCommand"/>.
/// </summary>
public sealed class RemoveCatalogItemHandler : ICommandHandler<RemoveCatalogItemCommand>
{
    private readonly IAggregateRepository _items;

    /// <summary>
    /// Creates a handler for the catalog item store.
    /// </summary>
    /// <param name="items">Catalog item aggregate repository.</param>
    public RemoveCatalogItemHandler(IAggregateRepository items) => _items = items;

    /// <summary>
    /// Removes a catalog item from sale.
    /// </summary>
    /// <param name="command">Command that removes the item.</param>
    /// <param name="cancellationToken">Token that cancels the store call.</param>
    /// <returns>A task that completes when the command is stored.</returns>
    public async Task HandleAsync(
        RemoveCatalogItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var item = await _items
            .GetOrCreateAsync<CatalogItem>(command.ItemId, cancellationToken)
            .ConfigureAwait(false);
        await _items
            .HandleCommandAsync(item, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
