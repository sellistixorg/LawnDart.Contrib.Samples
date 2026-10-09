using LawnDart;
using LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;
using LawnDart.Samples.EShop.Identity.Commands;

// Keep this namespace. This file lives in the slice folder.
// Changing the namespace splits the partial class.
namespace LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;

public sealed partial class ShopperAccount
{
    /// <summary>
    /// User name or password is incorrect, or account is locked/disabled.
    /// </summary>
    public const string InvalidCredentialsMessage =
        "User name or password is incorrect, or account is locked/disabled.";

    /// <summary>
    /// Invalid Credentials. Code INVALID_CREDENTIALS.
    /// </summary>
    public const string InvalidCredentialsTitle = "INVALID_CREDENTIALS";

    /// <summary>
    /// Records a successful shopper sign-in. Produces Shopper Signed In.
    /// </summary>
    /// <param name="command">Sign In Shopper.</param>
    /// <remarks>
    /// The shopper account must already exist and be active.
    /// A missing or inactive account is rejected with INVALID_CREDENTIALS.
    /// User name or password is incorrect, or account is locked/disabled.
    /// The password is not written on the event.
    /// </remarks>
    public void Handle(SignInShopperCommand command)
    {
        _ = command;

        if (!State.Exists || !State.IsActive)
            throw new DomainException(InvalidCredentialsMessage, InvalidCredentialsTitle);

        Apply(new ShopperSignedIn(
            Guid.NewGuid(),
            DateTime.UtcNow,
            State.AccountId,
            State.UserName));
    }
}

public sealed partial class ShopperAccountState
{
    internal void Apply(ShopperSignedIn signedIn)
    {
        // Sign-in does not change decision state.
        _ = signedIn;
    }
}
