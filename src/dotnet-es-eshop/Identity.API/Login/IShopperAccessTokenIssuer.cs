using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Signs a shopper access token after a successful sign-in.
/// </summary>
public interface IShopperAccessTokenIssuer
{
    /// <summary>
    /// Signs a token for <paramref name="accountId"/>.
    /// </summary>
    /// <param name="accountId">Shopper account id. Written as <c>sub</c>.</param>
    /// <param name="displayName">Display name. Written as <c>name</c>. The password is not an input.</param>
    /// <param name="cancellationToken">Token used to cancel key loading.</param>
    /// <returns>The compact token and its expiry. Do not log the token.</returns>
    Task<ShopperAccessToken> IssueAsync(
        Guid accountId,
        string displayName,
        CancellationToken cancellationToken = default);
}
