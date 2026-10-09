using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.Commands;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;

/// <summary>
/// Loads the catalog item and handles <see cref="RestockCatalogItemCommand"/>.
/// </summary>
public sealed class RestockCatalogItemHandler : ICommandHandler<RestockCatalogItemCommand>
{
    private readonly IAggregateRepository _items;

    /// <summary>
    /// Creates a handler for the catalog item store.
    /// </summary>
    /// <param name="items">Catalog item aggregate repository.</param>
    public RestockCatalogItemHandler(IAggregateRepository items) => _items = items;

    /// <summary>
    /// Adds units to available stock up to MaxStockThreshold.
    /// </summary>
    /// <param name="command">Command that restocks the item.</param>
    /// <param name="cancellationToken">Token that cancels the store call.</param>
    /// <returns>A task that completes when the command is stored.</returns>
    public async Task HandleAsync(
        RestockCatalogItemCommand command,
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
