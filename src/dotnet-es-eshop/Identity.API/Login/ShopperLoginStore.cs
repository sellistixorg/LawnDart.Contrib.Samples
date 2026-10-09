using LawnDart;
using Microsoft.AspNetCore.Identity;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Creates ASP.NET Core Identity users with <see cref="PasswordHasher{TUser}"/>.
/// The user name is unique. Sign in uses that name.
/// </summary>
public sealed class ShopperLoginStore : IShopperLoginStore
{
    /// <summary>
    /// An account with this user name already exists.
    /// </summary>
    public const string UserNameAlreadyRegisteredMessage =
        "An account with this user name already exists.";

    /// <summary>
    /// User name already registered. Code USER_NAME_ALREADY_REGISTERED.
    /// </summary>
    public const string UserNameAlreadyRegisteredTitle = "USER_NAME_ALREADY_REGISTERED";

    /// <summary>
    /// The user name contains characters the Identity user validator rejects.
    /// </summary>
    public const string UserNameInvalidMessage = "User name is invalid.";

    /// <summary>
    /// User name invalid. Code USER_NAME_INVALID.
    /// </summary>
    public const string UserNameInvalidTitle = "USER_NAME_INVALID";

    /// <summary>
    /// The password does not meet the Identity password rules. Code PASSWORD_REQUIREMENTS_NOT_MET.
    /// </summary>
    public const string PasswordRequirementsNotMetTitle = "PASSWORD_REQUIREMENTS_NOT_MET";

    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <param name="users">Identity user manager. It hashes passwords with <see cref="PasswordHasher{TUser}"/>.</param>
    /// <param name="signIn">Identity sign-in manager. Password checks and lockout go through it.</param>
    public ShopperLoginStore(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    /// <inheritdoc />
    public async Task ValidateNewLoginAsync(
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var taken = await _users.FindByNameAsync(userName).ConfigureAwait(false);
        if (taken is not null)
        {
            throw new DomainException(UserNameAlreadyRegisteredMessage, UserNameAlreadyRegisteredTitle);
        }

        var probe = new ApplicationUser
        {
            UserName = userName,
            Email = email
        };

        foreach (var validator in _users.UserValidators)
        {
            var result = await validator.ValidateAsync(_users, probe).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                ThrowIfUserNameRejected(result);
                throw UnmappedLoginRejection(result);
            }
        }

        foreach (var validator in _users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_users, probe, password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw PasswordRejected(result);
            }
        }
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        Guid accountId,
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = new ApplicationUser
        {
            Id = accountId.ToString(),
            UserName = userName,
            Email = email
        };

        var result = await _users.CreateAsync(user, password).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return;
        }

        ThrowIfUserNameRejected(result);

        if (result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal)))
        {
            throw PasswordRejected(result);
        }

        throw UnmappedLoginRejection(result);
    }

    /// <inheritdoc />
    public async Task ConfirmEmailAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _users.FindByIdAsync(accountId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException("Identity user was not created for this shopper account.");
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        user.EmailConfirmed = true;
        var result = await _users.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity login store could not mark the email confirmed. " +
                string.Join(' ', result.Errors.Select(error => error.Code)));
        }
    }

    /// <inheritdoc />
    public async Task SetPaymentCardAsync(
        Guid accountId,
        ShopperPaymentCard card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _users.FindByIdAsync(accountId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException("Identity user was not created for this shopper account.");
        }

        user.CardHolderName = card.CardHolderName;
        user.CardNumber = card.CardNumber;
        user.CardType = card.CardType;
        user.Expiration = card.Expiration;
        user.SecurityNumber = card.SecurityNumber;
        var result = await _users.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity login store could not store the demo payment card. " +
                string.Join(' ', result.Errors.Select(error => error.Code)));
        }
    }

    /// <inheritdoc />
    public async Task<Guid?> CheckUserNamePasswordAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var name = userName?.Trim() ?? "";
        if (name.Length == 0 || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = await _users.FindByNameAsync(name).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var result = await _signIn
            .CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        if (!Guid.TryParse(user.Id, out var accountId))
        {
            throw new InvalidOperationException("Identity user id is not a shopper account id.");
        }

        return accountId;
    }

    /// <summary>
    /// Maps <c>InvalidUserName</c> to <see cref="UserNameInvalidTitle"/> and
    /// <c>DuplicateUserName</c> to <see cref="UserNameAlreadyRegisteredTitle"/>.
    /// </summary>
    private static void ThrowIfUserNameRejected(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidUserName)))
        {
            throw new DomainException(UserNameInvalidMessage, UserNameInvalidTitle);
        }

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw new DomainException(UserNameAlreadyRegisteredMessage, UserNameAlreadyRegisteredTitle);
        }
    }

    private static InvalidOperationException UnmappedLoginRejection(IdentityResult result) =>
        new(
            "Identity login store rejected the new user. " +
            string.Join(' ', result.Errors.Select(error => error.Code)));

    private static DomainException PasswordRejected(IdentityResult result)
    {
        var message = string.Join(' ', result.Errors.Select(error => error.Description));
        return new DomainException(message, PasswordRequirementsNotMetTitle);
    }
}
