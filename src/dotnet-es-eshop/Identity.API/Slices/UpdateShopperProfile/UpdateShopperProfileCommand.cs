using LawnDart;

namespace LawnDart.Samples.EShop.Identity.Commands;

/// <summary>
/// Updates shopper profile fields used as checkout defaults.
/// </summary>
/// <remarks>
/// POST /api/identity/update-shopper-profile.
/// Email and user name stay as they were when the account was registered.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="AccountId">Account to update.</param>
/// <param name="Name">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="PhoneNumber">Phone.</param>
/// <param name="Street">Street.</param>
/// <param name="City">City.</param>
/// <param name="State">State.</param>
/// <param name="Country">Country.</param>
/// <param name="ZipCode">Postal code.</param>
public sealed record UpdateShopperProfileCommand(
    Guid Id,
    Guid AccountId,
    string Name,
    string LastName,
    string? PhoneNumber = null,
    string? Street = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? ZipCode = null) : ICommand;
