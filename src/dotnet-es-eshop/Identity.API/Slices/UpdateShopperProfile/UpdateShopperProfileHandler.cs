using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;

namespace LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;

/// <summary>
/// Updates shopper profile fields used as checkout defaults.
/// </summary>
public sealed class UpdateShopperProfileHandler : ICommandHandler<UpdateShopperProfileCommand>
{
    private readonly IAggregateRepository _accounts;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="accounts">Aggregate repository for the shopper account stream.</param>
    public UpdateShopperProfileHandler(IAggregateRepository accounts) => _accounts = accounts;

    /// <summary>
    /// Appends Shopper Profile Updated on the shopper account stream.
    /// </summary>
    /// <param name="command">Update Shopper Profile.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the account stream is written.</returns>
    public async Task HandleAsync(
        UpdateShopperProfileCommand command,
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
