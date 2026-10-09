using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.Commands;

namespace LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;

/// <summary>
/// Creates a new shopper account with credentials and basic profile. Produces Shopper Account Registered.
/// </summary>
/// <remarks>
/// An account with this email (or user name) already exists.
/// A duplicate email is rejected with EMAIL_ALREADY_REGISTERED before either write.
/// A duplicate user name is rejected with USER_NAME_ALREADY_REGISTERED before either write.
/// A user name the Identity user validator rejects is rejected with USER_NAME_INVALID before either write.
/// The email reservation and Shopper Account Registered are written first. The Identity user is inserted second.
/// If that insert fails, the events stay and the password hash is not stored.
/// </remarks>
public sealed class RegisterShopperAccountHandler : ICommandHandler<RegisterShopperAccountCommand>
{
    private readonly IAggregateRepository _accounts;
    private readonly IShopperLoginStore _logins;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="accounts">Aggregate repository for the account stream and the email reservation stream.</param>
    /// <param name="logins">Identity login store. The password hash is written here.</param>
    public RegisterShopperAccountHandler(IAggregateRepository accounts, IShopperLoginStore logins)
    {
        _accounts = accounts;
        _logins = logins;
    }

    /// <summary>
    /// Reserves the email, appends Shopper Account Registered, then stores the password hash.
    /// </summary>
    /// <param name="command">Register Shopper Account.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the events and the login row are written.</returns>
    /// <remarks>
    /// The event write runs first. The Identity insert runs second.
    /// A failed insert leaves the events in place and stores no password hash, so sign in cannot succeed.
    /// This command does not mark the email confirmed.
    /// </remarks>
    public async Task HandleAsync(
        RegisterShopperAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var profile = ShopperAccount.Prepare(command);

        var account = await _accounts
            .GetOrCreateAsync<ShopperAccount>(command.AccountId, cancellationToken)
            .ConfigureAwait(false);
        if (account.State.Exists)
        {
            throw new DomainException(
                ShopperAccount.EmailAlreadyRegisteredMessage,
                ShopperAccount.EmailAlreadyRegisteredTitle);
        }

        var email = await _accounts
            .GetOrCreateAsync<ShopperEmail>(ShopperEmail.EmailStreamId(profile.Email), cancellationToken)
            .ConfigureAwait(false);
        if (email.State.Exists)
        {
            throw new DomainException(
                ShopperAccount.EmailAlreadyRegisteredMessage,
                ShopperAccount.EmailAlreadyRegisteredTitle);
        }

        await _logins
            .ValidateNewLoginAsync(profile.UserName, profile.Email, command.Password, cancellationToken)
            .ConfigureAwait(false);

        await _accounts
            .HandleCommandAsync(
                email,
                new ReserveShopperEmailCommand(Guid.NewGuid(), profile.Email, command.AccountId),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await _accounts
            .HandleCommandAsync(account, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await _logins
            .CreateAsync(command.AccountId, profile.UserName, profile.Email, command.Password, cancellationToken)
            .ConfigureAwait(false);
    }
}
