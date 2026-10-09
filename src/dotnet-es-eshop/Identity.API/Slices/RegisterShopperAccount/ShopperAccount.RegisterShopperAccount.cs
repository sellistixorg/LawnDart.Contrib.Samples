using LawnDart;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;

public sealed partial class ShopperAccount
{
    /// <summary>
    /// An account with this email (or user name) already exists.
    /// </summary>
    public const string EmailAlreadyRegisteredMessage =
        "An account with this email (or user name) already exists.";

    /// <summary>
    /// Email Already Registered. Code EMAIL_ALREADY_REGISTERED.
    /// </summary>
    public const string EmailAlreadyRegisteredTitle = "EMAIL_ALREADY_REGISTERED";

    /// <summary>
    /// Creates a new shopper account with credentials and basic profile. Produces Shopper Account Registered.
    /// </summary>
    /// <param name="command">Register Shopper Account.</param>
    /// <remarks>
    /// An account with this email (or user name) already exists.
    /// </remarks>
    public void Handle(RegisterShopperAccountCommand command)
    {
        var profile = Prepare(command);

        if (State.Exists)
            throw new DomainException(EmailAlreadyRegisteredMessage, EmailAlreadyRegisteredTitle);

        Apply(new ShopperAccountRegistered(
            Guid.NewGuid(),
            DateTime.UtcNow,
            command.AccountId,
            profile.UserName,
            profile.Email,
            profile.Name,
            profile.LastName));
    }
}

public sealed partial class ShopperAccountState
{
    internal void Apply(ShopperAccountRegistered registered)
    {
        AccountId = registered.AccountId;
        UserName = registered.UserName;
        Email = registered.Email;
        Name = registered.Name;
        LastName = registered.LastName;
        IsActive = true;
        Exists = true;
    }
}
