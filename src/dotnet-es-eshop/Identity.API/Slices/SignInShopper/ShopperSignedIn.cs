using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;

/// <summary>
/// Shopper successfully authenticated.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When sign-in completed.</param>
/// <param name="AccountId">Authenticated account id.</param>
/// <param name="UserName">Login name.</param>
[EventTypeName("shopper-signed-in")]
public sealed record ShopperSignedIn(
    Guid Id,
    DateTime Timestamp,
    Guid AccountId,
    string UserName) : IEvent;
