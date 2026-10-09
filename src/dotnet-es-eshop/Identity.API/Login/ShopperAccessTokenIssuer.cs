using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Signs shopper access tokens with the key in the login database.
/// </summary>
public sealed class ShopperAccessTokenIssuer : IShopperAccessTokenIssuer
{
    private readonly ShopperSigningKeyRing _keys;

    /// <summary>
    /// Creates the issuer.
    /// </summary>
    /// <param name="keys">Signing key loaded from <c>identitylogindb</c>.</param>
    public ShopperAccessTokenIssuer(ShopperSigningKeyRing keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys = keys;
    }

    /// <inheritdoc />
    public async Task<ShopperAccessToken> IssueAsync(
        Guid accountId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var keys = await _keys.GetAsync(cancellationToken).ConfigureAwait(false);
        var key = keys.FirstOrDefault(item => item.KeyId == ShopperSigningKeyRing.SlotId) ?? keys[0];
        return ShopperAccessTokens.Issue(key, accountId, displayName);
    }
}
