using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;

namespace LawnDart.Samples.EShop.Identity.API.Slices.SignOutShopper;

/// <summary>
/// Appends Shopper Signed Out for an existing active shopper account.
/// </summary>
/// <remarks>
/// A missing or inactive account throws <see cref="ShopperAccount.NotFoundTitle"/>.
/// The handler does not clear a cookie or a token. LogoutId is not applied.
/// </remarks>
public sealed class SignOutShopperHandler : ICommandHandler<SignOutShopperCommand>
{
    private readonly IAggregateRepository _accounts;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="accounts">Aggregate repository for the shopper account stream.</param>
    public SignOutShopperHandler(IAggregateRepository accounts) => _accounts = accounts;

    /// <summary>
    /// Appends Shopper Signed Out on the shopper account stream.
    /// </summary>
    /// <param name="command">Sign Out Shopper.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the sign-out event is written.</returns>
    /// <remarks>
    /// Shopper account does not exist. Code SHOPPER_ACCOUNT_NOT_FOUND.
    /// A missing or inactive account is rejected and no event is written.
    /// </remarks>
    public async Task HandleAsync(
        SignOutShopperCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts
            .GetOrCreateAsync<ShopperAccount>(command.AccountId, cancellationToken)
            .ConfigureAwait(false);
        await _accounts
            .HandleCommandAsync(account, command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
