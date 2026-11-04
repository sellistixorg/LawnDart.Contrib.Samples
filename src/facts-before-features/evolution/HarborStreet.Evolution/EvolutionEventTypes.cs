using HarborStreet.Evolution.Events;

namespace HarborStreet.Evolution;

public static class EvolutionEventTypes
{
    public static readonly Type[] All =
    [
        typeof(TitleAddedToCatalog),
        typeof(OrderPlaced),
        typeof(OrderPlacedV2)
    ];
}
