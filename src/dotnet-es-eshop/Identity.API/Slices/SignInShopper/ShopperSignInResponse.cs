namespace LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;

/// <summary>
/// Account id, display name, and access token returned after a successful sign-in.
/// </summary>
/// <param name="AccountId">Authenticated account id.</param>
/// <param name="DisplayName">Given name and family name. The login name when those are blank.</param>
/// <param name="AccessToken">Signed JWT. <c>sub</c> is <paramref name="AccountId"/>. Do not log this value.</param>
/// <param name="AccessTokenExpiresAt">UTC expiry of <paramref name="AccessToken"/>.</param>
public sealed record ShopperSignInResponse(
    Guid AccountId,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt)
{
    /// <summary>
    /// Given name and family name. The login name when those are blank.
    /// </summary>
    /// <param name="name">Given name.</param>
    /// <param name="lastName">Family name.</param>
    /// <param name="userName">Login name.</param>
    /// <returns>The display name. Empty when every part is blank.</returns>
    public static string FormatDisplayName(string? name, string? lastName, string? userName)
    {
        var display = string.Join(
            ' ',
            new[] { name, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (display.Length > 0)
            return display;

        return userName?.Trim() ?? "";
    }
}
