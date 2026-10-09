using LawnDart;
using LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;
using LawnDart.Samples.EShop.Identity.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;

public sealed partial class ShopperAccount
{
    /// <summary>
    /// Updates shopper profile fields used as checkout defaults.
    /// </summary>
    /// <param name="command">Update Shopper Profile.</param>
    /// <remarks>
    /// The shopper account must already exist. A missing account is rejected with
    /// "Shopper account does not exist." Name and last name are required.
    /// Email and user name stay as they were when the account was registered.
    /// </remarks>
    public void Handle(UpdateShopperProfileCommand command)
    {
        if (command.AccountId == Guid.Empty)
            throw new DomainException("Account id is required.");

        var name = Required(command.Name, "Name is required.");
        var lastName = Required(command.LastName, "Last name is required.");

        if (!State.Exists)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        Apply(new ShopperProfileUpdated(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.AccountId,
            name,
            lastName,
            UpdateProfileText.Optional(command.PhoneNumber),
            UpdateProfileText.Optional(command.Street),
            UpdateProfileText.Optional(command.City),
            UpdateProfileText.Optional(command.State),
            UpdateProfileText.Optional(command.Country),
            UpdateProfileText.Optional(command.ZipCode)));
    }
}

public sealed partial class ShopperAccountState
{
    internal void Apply(ShopperProfileUpdated updated)
    {
        Name = updated.Name;
        LastName = updated.LastName;
        PhoneNumber = updated.PhoneNumber;
        Street = updated.Street;
        City = updated.City;
        State = updated.State;
        Country = updated.Country;
        ZipCode = updated.ZipCode;
    }
}

file static class UpdateProfileText
{
    public static string? Optional(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? null : trimmed;
    }
}
