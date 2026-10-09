using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Ordering.Commands;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;

/// <summary>
/// Marks a paid order as shipped.
/// </summary>
public sealed class ShipOrderHandler : ICommandHandler<ShipOrderCommand>
{
    private readonly IAggregateRepository _orders;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="orders">Aggregate repository for the order stream.</param>
    public ShipOrderHandler(IAggregateRepository orders) => _orders = orders;

    /// <summary>
    /// Marks a paid order as shipped.
    /// </summary>
    /// <param name="command">Ship Order.</param>
    /// <param name="cancellationToken">Token used to cancel the load and the write.</param>
    /// <returns>A task that completes when the order events are written.</returns>
    public async Task HandleAsync(
        ShipOrderCommand command,
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
