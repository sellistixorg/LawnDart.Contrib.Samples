using HarborStreet.Evolution.Events;
using LawnDart.EventStore;

namespace HarborStreet.Evolution.Seeding;

public sealed class EvolutionSeeder
{
    private readonly IEventStore _store;

    public EvolutionSeeder(IEventStore store) => _store = store;

    public async Task SeedMixedHistoryAsync(
        Guid titleId,
        Guid legacyOrderId,
        Guid modernOrderId,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        var streamId = $"harbor-street:evolution:{titleId:N}";
        var now = DateTime.UtcNow;

        await _store.AppendAsync(
            streamId,
            [
                new TitleAddedToCatalog(
                    Guid.NewGuid(), now, titleId, "The Lighthouse Keeper", 52.00m, IsScarce: true),
                // Historical write — v1 shape still on disk
                new OrderPlaced(
                    Guid.NewGuid(), now.AddMinutes(1), legacyOrderId,
                    Guid.Parse("a0000000-0000-0000-0000-000000000001"), titleId, 1),
                // New write — v2 shape with warehouse + display name
                new OrderPlacedV2(
                    Guid.NewGuid(), now.AddMinutes(2), modernOrderId,
                    Guid.Parse("b0000000-0000-0000-0000-000000000002"), titleId, 1,
                    warehouseId, CustomerDisplayName: "Casey Shore")
            ],
            cancellationToken: cancellationToken);
    }
}
