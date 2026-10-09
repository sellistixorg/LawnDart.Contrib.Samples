using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.Commands;

namespace LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;

/// <summary>
/// Authenticates the shopper by user name and appends Shopper Signed In.
/// </summary>
/// <remarks>
/// The password check uses SignInManager.CheckPasswordSignInAsync with lockout on failure.
/// That matches eShop <c>AccountController.Login</c>, which calls <c>PasswordSignInAsync</c> with <c>lockoutOnFailure: true</c>.
/// A wrong user name, a wrong password, and a lockout all throw INVALID_CREDENTIALS
/// (<see cref="ShopperAccount.InvalidCredentialsTitle"/>).
/// The handler does not log the command or the password.
/// </remarks>
public sealed class SignInShopperHandler : ICommandHandler<SignInShopperCommand>
{
    private readonly IAggregateRepository _accounts;
    private readonly IShopperLoginStore _logins;
    private readonly SignInShopperReceipt _receipt;
    private readonly IShopperAccessTokenIssuer _tokens;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="accounts">Aggregate repository for the shopper account stream.</param>
    /// <param name="logins">Identity login store. The password check runs here.</param>
    /// <param name="receipt">Holds the account id, display name, and access token for the HTTP response. The password is not stored here.</param>
    /// <param name="tokens">Signs the access token. The password is not passed in.</param>
    public SignInShopperHandler(
        IAggregateRepository accounts,
        IShopperLoginStore logins,
        SignInShopperReceipt receipt,
        IShopperAccessTokenIssuer tokens)
    {
        _accounts = accounts;
        _logins = logins;
        _receipt = receipt;
        _tokens = tokens;
    }

    /// <summary>
    /// Checks the user name and password, then appends Shopper Signed In.
    /// </summary>
    /// <param name="command">Sign In Shopper.</param>
    /// <param name="cancellationToken">Token used to cancel the check and the write.</param>
    /// <returns>A task that completes when the sign-in event is written.</returns>
    /// <remarks>
    /// User name or password is incorrect, or account is locked/disabled.
    /// The password is not written on the event.
    /// </remarks>
    public async Task HandleAsync(
        SignInShopperCommand command,
        CancellationToken cancellationToken = default)
    {
        var accountId = await _logins
            .CheckUserNamePasswordAsync(command.UserName, command.Password, cancellationToken)
            .ConfigureAwait(false);
        if (accountId is null)
        {
            throw new DomainException(
                ShopperAccount.InvalidCredentialsMessage,
                ShopperAccount.InvalidCredentialsTitle);
        }

        var account = await _accounts
            .GetOrCreateAsync<ShopperAccount>(accountId.Value, cancellationToken)
            .ConfigureAwait(false);
        await _accounts
            .HandleCommandAsync(account, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var id = account.State.AccountId == Guid.Empty ? accountId.Value : account.State.AccountId;
        var displayName = ShopperSignInResponse.FormatDisplayName(
            account.State.Name,
            account.State.LastName,
            account.State.UserName);
        var token = await _tokens.IssueAsync(id, displayName, cancellationToken).ConfigureAwait(false);
        _receipt.Set(new ShopperSignInResponse(id, displayName, token.Value, token.ExpiresAt));
    }
}
