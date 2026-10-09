using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Identity.API.Slices.SignOutShopper;

/// <summary>
/// Shopper session ended.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When sign-out completed.</param>
/// <param name="AccountId">Account that signed out.</param>
[EventTypeName("shopper-signed-out")]
public sealed record ShopperSignedOut(
    Guid Id,
    DateTime Timestamp,
    Guid AccountId) : IEvent;
