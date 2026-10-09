using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.Identity.API.Seed;

/// <summary>
/// Sends Register Shopper Account and Update Shopper Profile for Alice and Bob.
/// </summary>
public sealed class IdentitySeeder
{
    private readonly ICommandHandler<RegisterShopperAccountCommand> _register;
    private readonly ICommandHandler<UpdateShopperProfileCommand> _profiles;
    private readonly IAggregateRepository _accounts;
    private readonly IShopperLoginStore _logins;
    private readonly UserManager<ApplicationUser> _users;
    private readonly EShopSeedOptions _options;
    private readonly ILogger<IdentitySeeder> _logger;

    /// <summary>
    /// Creates the seeder.
    /// </summary>
    /// <param name="register">Register Shopper Account handler.</param>
    /// <param name="profiles">Update Shopper Profile handler.</param>
    /// <param name="accounts">Aggregate repository for the shopper account streams.</param>
    /// <param name="logins">Identity login store. Email confirmation and the demo card are written here, not by the register command.</param>
    /// <param name="users">Identity user manager. The user name check uses it.</param>
    /// <param name="options">Seed switch.</param>
    /// <param name="logger">Logger.</param>
    public IdentitySeeder(
        ICommandHandler<RegisterShopperAccountCommand> register,
        ICommandHandler<UpdateShopperProfileCommand> profiles,
        IAggregateRepository accounts,
        IShopperLoginStore logins,
        UserManager<ApplicationUser> users,
        IOptions<EShopSeedOptions> options,
        ILogger<IdentitySeeder> logger)
    {
        _register = register;
        _profiles = profiles;
        _accounts = accounts;
        _logins = logins;
        _users = users;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Seeds Alice and Bob when they are missing.
    /// A second call writes nothing when both accounts, profiles, and confirmed emails are already stored.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the seed.</param>
    /// <returns>A task that completes when the seed has finished or been skipped.</returns>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Identity seed is disabled.");
            return;
        }

        if (await AlreadySeededAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Identity seed is already present ({UserCount} users).", IdentitySeedData.Users.Count);
            return;
        }

        foreach (var user in IdentitySeedData.Users)
            await SeedUserAsync(user, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Identity seed stored {UserCount} users.", IdentitySeedData.Users.Count);
    }

    private async Task<bool> AlreadySeededAsync(CancellationToken cancellationToken)
    {
        foreach (var user in IdentitySeedData.Users)
        {
            if (!await IsCompleteAsync(user, cancellationToken).ConfigureAwait(false))
                return false;
        }

        return true;
    }

    private async Task<bool> IsCompleteAsync(IdentitySeedUser user, CancellationToken cancellationToken)
    {
        var login = await _users.FindByNameAsync(user.UserName).ConfigureAwait(false);
        if (login is null || BelongsToAnotherAccount(login, user) || !login.EmailConfirmed)
            return false;

        if (string.IsNullOrEmpty(login.CardNumber))
            return false;

        var account = await _accounts
            .GetOrCreateAsync<ShopperAccount>(user.AccountId, cancellationToken)
            .ConfigureAwait(false);
        return account.State.Exists && ProfileMatches(account.State, user);
    }

    private async Task SeedUserAsync(IdentitySeedUser user, CancellationToken cancellationToken)
    {
        var login = await _users.FindByNameAsync(user.UserName).ConfigureAwait(false);
        if (BelongsToAnotherAccount(login, user))
        {
            LogOtherAccount(user);
            return;
        }

        var registeredNow = false;
        if (login is null)
        {
            await SendAsync(
                () => _register.HandleAsync(user.RegisterCommand(), cancellationToken),
                cancellationToken).ConfigureAwait(false);
            registeredNow = true;
            login = await _users.FindByNameAsync(user.UserName).ConfigureAwait(false);
            if (BelongsToAnotherAccount(login, user))
            {
                LogOtherAccount(user);
                return;
            }

            if (login is null)
                return;
        }

        if (registeredNow || !login.EmailConfirmed)
        {
            await _logins.ConfirmEmailAsync(user.AccountId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Identity seed marked {UserName} email confirmed.", user.UserName);
        }

        if (registeredNow || string.IsNullOrEmpty(login.CardNumber))
        {
            await _logins
                .SetPaymentCardAsync(user.AccountId, user.PaymentCard(), cancellationToken)
                .ConfigureAwait(false);
        }

        var account = await _accounts
            .GetOrCreateAsync<ShopperAccount>(user.AccountId, cancellationToken)
            .ConfigureAwait(false);
        if (!account.State.Exists || ProfileMatches(account.State, user))
            return;

        await SendAsync(
            () => _profiles.HandleAsync(user.ProfileCommand(), cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool BelongsToAnotherAccount(ApplicationUser? login, IdentitySeedUser user) =>
        login is not null
        && !string.Equals(login.Id, user.AccountId.ToString(), StringComparison.OrdinalIgnoreCase);

    private void LogOtherAccount(IdentitySeedUser user) =>
        _logger.LogInformation(
            "Identity seed skipped {UserName} because that user name belongs to another account.",
            user.UserName);

    private static bool ProfileMatches(ShopperAccountState state, IdentitySeedUser user) =>
        state.Name == user.Name
        && state.LastName == user.LastName
        && state.PhoneNumber == user.PhoneNumber
        && state.Street == user.Street
        && state.City == user.City
        && state.State == user.State
        && state.Country == user.Country
        && state.ZipCode == user.ZipCode;

    private async Task SendAsync(Func<Task> send, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await send().ConfigureAwait(false);
        }
        catch (DomainException ex) when (EShopSeedRejections.IsAlreadyPresent(ex))
        {
            _logger.LogInformation(
                "Identity seed skipped a command that is already stored: {Message} ({Title}).",
                ex.Message,
                ex.Title);
        }
        catch (ConcurrencyException)
        {
            _logger.LogInformation("Identity seed skipped a command that another writer stored first.");
        }
    }
}
