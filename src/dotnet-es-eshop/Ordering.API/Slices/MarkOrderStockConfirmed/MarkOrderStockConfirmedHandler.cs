using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Ordering.Commands;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderStockConfirmed;

/// <summary>
/// Sets order status to StockConfirmed.
/// </summary>
public sealed class MarkOrderStockConfirmedHandler : ICommandHandler<MarkOrderStockConfirmedCommand>
{
    private readonly IAggregateRepository _orders;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="orders">Aggregate repository for the order stream.</param>
    public MarkOrderStockConfirmedHandler(IAggregateRepository orders) => _orders = orders;

    /// <summary>
    /// Sets order status to StockConfirmed.
    /// </summary>
    /// <param name="command">Mark Order Stock Confirmed.</param>
    /// <param name="cancellationToken">Token used to cancel the load and the write.</param>
    /// <returns>A task that completes when the order events are written.</returns>
    public async Task HandleAsync(
        MarkOrderStockConfirmedCommand command,
        CancellationToken cancellationToken = default)
    {
        var order = await _orders
            .GetOrCreateAsync<OrderAggregate>(command.OrderId, cancellationToken)
            .ConfigureAwait(false);
        await _orders
            .HandleCommandAsync(order, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
