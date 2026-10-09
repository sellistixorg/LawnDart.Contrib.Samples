using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Ordering.Commands;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderPaid;

/// <summary>
/// Sets order status to Paid and signals Catalog to deduct stock.
/// </summary>
public sealed class MarkOrderPaidHandler : ICommandHandler<MarkOrderPaidCommand>
{
    private readonly IAggregateRepository _orders;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="orders">Aggregate repository for the order stream.</param>
    public MarkOrderPaidHandler(IAggregateRepository orders) => _orders = orders;

    /// <summary>
    /// Sets order status to Paid and signals Catalog to deduct stock.
    /// </summary>
    /// <param name="command">Mark Order Paid.</param>
    /// <param name="cancellationToken">Token used to cancel the load and the write.</param>
    /// <returns>A task that completes when the order events are written.</returns>
    public async Task HandleAsync(
        MarkOrderPaidCommand command,
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
