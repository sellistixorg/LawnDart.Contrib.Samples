using LawnDart;
using LawnDart.Samples.EShop.Identity.API.Slices.SignOutShopper;
using LawnDart.Samples.EShop.Identity.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;

public sealed partial class ShopperAccount
{
    /// <summary>
    /// Clears the shopper authentication session. Produces Shopper Signed Out.
    /// </summary>
    /// <param name="command">Sign Out Shopper.</param>
    /// <remarks>
    /// The shopper account must already exist and be active.
    /// A missing or inactive account is rejected with "Shopper account does not exist."
    /// Code SHOPPER_ACCOUNT_NOT_FOUND.
    /// LogoutId is accepted and is not applied. No cookie or token is cleared.
    /// </remarks>
    public void Handle(SignOutShopperCommand command)
    {
        _ = command;

        if (!State.Exists || !State.IsActive)
            throw new DomainException(NotFoundMessage, NotFoundTitle);

        Apply(new ShopperSignedOut(
            Guid.NewGuid(),
            DateTime.UtcNow,
            State.AccountId));
    }
}

public sealed partial class ShopperAccountState
{
    internal void Apply(ShopperSignedOut signedOut)
    {
        // Sign-out does not change decision state.
        _ = signedOut;
    }
}
