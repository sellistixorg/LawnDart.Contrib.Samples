using HarborStreet.Coordination.Commands;
using HarborStreet.Coordination.Domain;
using LawnDart.Aggregates;

namespace HarborStreet.Coordination.Seeding;

/// <summary>
/// Seeds post-checkout state: a scarce title already reserved for a placed order.
/// </summary>
public static class CheckoutSeed
{
    public static async Task SeedReservedOrderAsync(
        IAggregateRepository repo,
        Guid titleId,
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        await repo.HandleCommandAsync(
            await repo.GetOrCreateAsync<Title>(titleId, cancellationToken),
            new AddTitleToCatalogCommand(Guid.NewGuid(), titleId, "The Lighthouse Keeper", 52.00m, IsScarce: true),
            cancellationToken: cancellationToken);

        await repo.HandleCommandAsync(
            await repo.GetOrCreateAsync<Title>(titleId, cancellationToken),
            new RestockTitleCommand(Guid.NewGuid(), titleId, 1),
            cancellationToken: cancellationToken);

        await repo.HandleCommandAsync(
            await repo.GetOrCreateAsync<Order>(orderId, cancellationToken),
            new PlaceOrderCommand(Guid.NewGuid(), orderId, customerId, titleId, 1),
            cancellationToken: cancellationToken);

        await repo.HandleCommandAsync(
            await repo.GetOrCreateAsync<Title>(titleId, cancellationToken),
            new ReserveStockCommand(Guid.NewGuid(), titleId, orderId, 1),
            cancellationToken: cancellationToken);
    }
}
