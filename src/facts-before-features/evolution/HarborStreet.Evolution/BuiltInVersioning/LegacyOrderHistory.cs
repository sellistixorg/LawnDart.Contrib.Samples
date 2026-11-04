using LawnDart.EventSourcing.Serialization;
using LawnDart.EventStore;

namespace HarborStreet.Evolution.BuiltInVersioning;

/// <summary>
/// Stands in for yesterday's build. This process writes only the current version, so the
/// sample puts a version 1 record on the log directly, the way an older deploy would have.
/// </summary>
public static class LegacyOrderHistory
{
    public static async Task WriteVersion1Async(
        IEventLog log,
        string streamId,
        OrderPlacedV1 legacy,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonEventSerializer().Serialize(legacy, typeof(OrderPlacedV1));
        await log.AppendAsync(
            streamId,
            [new AppendEvent("HarborStreet.OrderPlaced", payload, schemaVersion: 1)],
            cancellationToken: cancellationToken);
    }
}
