using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Ordering.Commands;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Slices.MarkOrderAwaitingValidation;

/// <summary>
/// Sets order status to AwaitingValidation and notifies Catalog.
/// </summary>
public sealed class MarkOrderAwaitingValidationHandler : ICommandHandler<MarkOrderAwaitingValidationCommand>
{
    private readonly IAggregateRepository _orders;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="orders">Aggregate repository for the order stream.</param>
    public MarkOrderAwaitingValidationHandler(IAggregateRepository orders) => _orders = orders;

    /// <summary>
    /// Sets order status to AwaitingValidation and notifies Catalog.
    /// </summary>
    /// <param name="command">Mark Order Awaiting Validation.</param>
    /// <param name="cancellationToken">Token used to cancel the load and the write.</param>
    /// <returns>A task that completes when the order events are written.</returns>
    public async Task HandleAsync(
        MarkOrderAwaitingValidationCommand command,
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
