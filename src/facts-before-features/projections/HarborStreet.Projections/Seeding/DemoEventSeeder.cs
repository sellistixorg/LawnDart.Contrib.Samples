using HarborStreet.Projections.Events;
using LawnDart.EventStore;

namespace HarborStreet.Projections.Seeding;

/// <summary>
/// Appends Harbor Street facts for projection demos. Domain decisions live in other chapters;
/// here we only need a known event history to project.
/// </summary>
public sealed class DemoEventSeeder
{
    private readonly IEventStore _store;

    public DemoEventSeeder(IEventStore store) => _store = store;

    public async Task SeedLighthouseKeeperStoryAsync(
        Guid titleId,
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var streamId = $"harbor-street:projections:{titleId:N}";
        var now = DateTime.UtcNow;

        await _store.AppendAsync(
            streamId,
            [
                new TitleAddedToCatalog(
                    Guid.NewGuid(), now, titleId, "The Lighthouse Keeper", 52.00m, IsScarce: true),
                new TitleRestocked(Guid.NewGuid(), now.AddSeconds(1), titleId, Quantity: 1),
                new OrderPlaced(Guid.NewGuid(), now.AddSeconds(2), orderId, customerId, titleId, Quantity: 1),
                new StockReserved(Guid.NewGuid(), now.AddSeconds(2), titleId, orderId, Quantity: 1)
            ],
            cancellationToken: cancellationToken);
    }
}
