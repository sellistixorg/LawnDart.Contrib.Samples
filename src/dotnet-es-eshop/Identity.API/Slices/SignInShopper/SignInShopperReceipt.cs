namespace LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;

/// <summary>
/// Holds the account id, display name, and access token for one sign-in request.
/// The password is not stored here. Do not log the token.
/// </summary>
public sealed class SignInShopperReceipt
{
    /// <summary>
    /// Account id, display name, and access token after a successful sign-in. Null until then.
    /// </summary>
    public ShopperSignInResponse? Result { get; private set; }

    /// <summary>
    /// Records the account id, display name, and access token.
    /// </summary>
    /// <param name="result">Account id, display name, and access token. The password is not part of this value.</param>
    public void Set(ShopperSignInResponse result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }
}
