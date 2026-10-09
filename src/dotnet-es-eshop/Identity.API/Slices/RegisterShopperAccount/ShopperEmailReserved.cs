using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;

/// <summary>
/// An email was reserved for one shopper account.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When the email was reserved.</param>
/// <param name="Email">Email address in lower case.</param>
/// <param name="AccountId">Account that reserved the email.</param>
[EventTypeName("shopper-email-reserved")]
public sealed record ShopperEmailReserved(
    Guid Id,
    DateTime Timestamp,
    string Email,
    Guid AccountId) : IEvent;
