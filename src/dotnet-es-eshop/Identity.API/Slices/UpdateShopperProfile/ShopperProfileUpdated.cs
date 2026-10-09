using LawnDart;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;

/// <summary>
/// Shopper profile fields changed.
/// </summary>
/// <param name="Id">Event identifier.</param>
/// <param name="Timestamp">When update completed.</param>
/// <param name="AccountId">Account id.</param>
/// <param name="Name">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="PhoneNumber">Phone.</param>
/// <param name="Street">Street.</param>
/// <param name="City">City.</param>
/// <param name="State">State.</param>
/// <param name="Country">Country.</param>
/// <param name="ZipCode">Postal code.</param>
[EventTypeName("shopper-profile-updated")]
public sealed record ShopperProfileUpdated(
    Guid Id,
    DateTime Timestamp,
    Guid AccountId,
    string Name,
    string LastName,
    string? PhoneNumber,
    string? Street,
    string? City,
    string? State,
    string? Country,
    string? ZipCode) : IEvent;
