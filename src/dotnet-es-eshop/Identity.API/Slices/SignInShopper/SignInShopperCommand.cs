using LawnDart;

namespace LawnDart.Samples.EShop.Identity.Commands;

/// <summary>
/// Authenticates the shopper and establishes a session / auth cookie.
/// </summary>
/// <remarks>
/// POST /api/identity/sign-in-shopper.
/// The handler checks the user name and password, then appends Shopper Signed In.
/// It does not issue a cookie or a token. <see cref="ReturnUrl"/> and <see cref="RememberLogin"/> are not applied.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="UserName">User name. Sign-in is by user name only; an email address is not accepted.</param>
/// <param name="Password">Password (never emit on events).</param>
/// <param name="ReturnUrl">Post-login return URL.</param>
/// <param name="RememberLogin">Persist login cookie.</param>
public sealed record SignInShopperCommand(
    Guid Id,
    string UserName,
    string Password,
    string? ReturnUrl = null,
    bool RememberLogin = false) : ICommand
{
    /// <summary>
    /// Formats the command without the password.
    /// </summary>
    /// <returns>The command fields. The password is <c>[redacted]</c>.</returns>
    public override string ToString() =>
        $"SignInShopperCommand {{ Id = {Id}, UserName = {UserName}, Password = [redacted], ReturnUrl = {ReturnUrl}, RememberLogin = {RememberLogin} }}";
}
