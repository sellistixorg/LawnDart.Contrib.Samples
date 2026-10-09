using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;

/// <summary>
/// A shopper account was created. Tagged: shopper-account:{accountId}.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When registration completed.</param>
/// <param name="AccountId">New account id.</param>
/// <param name="UserName">Login name.</param>
/// <param name="Email">Email address.</param>
/// <param name="Name">Given name.</param>
/// <param name="LastName">Family name.</param>
[EventTypeName("shopper-account-registered")]
public sealed record ShopperAccountRegistered(
    Guid Id,
    DateTime Timestamp,
    Guid AccountId,
    string UserName,
    string Email,
    string Name,
    string LastName) : IEvent;
