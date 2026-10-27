using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Catalog stand-in used when a service hook has not called <c>WithEventTypes</c>.
/// </summary>
public static class EShopHostCatalog
{
    /// <summary>
    /// Family token for <see cref="EShopHostCatalogEvent"/>.
    /// </summary>
    public const string ReadyToken = "eshop-host-catalog-ready";
}

/// <summary>
/// Lets a service start and create its database tables before any slice registers events.
/// LawnDart resolves the event store only after <c>WithEventTypes</c>.
/// A hook that calls <c>WithEventTypes</c> supplies the catalog, and this type is not registered.
/// </summary>
[EventTypeName(EShopHostCatalog.ReadyToken)]
public sealed record EShopHostCatalogEvent(Guid Id, DateTime Timestamp) : IEvent;
