using Microsoft.AspNetCore.Identity;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// ASP.NET Core Identity login rows for shopper accounts.
/// The password hash is stored here. It is not written to the event store.
/// </summary>
public interface IShopperLoginStore
{
    /// <summary>
    /// Runs the Identity user validators, then the password validators.
    /// Does not insert a row.
    /// </summary>
    /// <param name="userName">Desired login name.</param>
    /// <param name="email">Email address.</param>
    /// <param name="password">Chosen password (never store on events).</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    /// <returns>A task that completes when the login can be created.</returns>
    Task ValidateNewLoginAsync(
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the login row and stores the password hash.
    /// The user id is <paramref name="accountId"/>.
    /// </summary>
    /// <param name="accountId">Shopper account id. Stored as the Identity user id.</param>
    /// <param name="userName">Desired login name.</param>
    /// <param name="email">Email address.</param>
    /// <param name="password">Chosen password (never store on events).</param>
    /// <param name="cancellationToken">Token used to cancel the insert.</param>
    /// <returns>A task that completes when the row is stored.</returns>
    /// <remarks>
    /// <see cref="IdentityUser.EmailConfirmed"/> stays false. Call <see cref="ConfirmEmailAsync"/> when the login should be marked confirmed.
    /// </remarks>
    Task CreateAsync(
        Guid accountId,
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the Identity user's email as confirmed.
    /// A user that is already confirmed is left as it is.
    /// </summary>
    /// <param name="accountId">Shopper account id. Stored as the Identity user id.</param>
    /// <param name="cancellationToken">Token used to cancel the update.</param>
    /// <returns>A task that completes when the flag is stored.</returns>
    Task ConfirmEmailAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the demo payment card on the login record.
    /// </summary>
    /// <param name="accountId">Shopper account id. Stored as the Identity user id.</param>
    /// <param name="card">Demo card copied from eShop.</param>
    /// <param name="cancellationToken">Token used to cancel the update.</param>
    /// <returns>A task that completes when the login row is updated.</returns>
    /// <remarks>
    /// These are demo values copied from eShop. They live only on the login record, never in events.
    /// </remarks>
    Task SetPaymentCardAsync(
        Guid accountId,
        ShopperPaymentCard card,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the user name and password with <c>SignInManager</c>.
    /// An unknown user name, a wrong password, and a lockout all return null.
    /// </summary>
    /// <param name="userName">User name. Sign-in is by user name only; an email address is not accepted.</param>
    /// <param name="password">Password (never store on events, never log).</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    /// <returns>The shopper account id when the password matches and the user can sign in. Null otherwise.</returns>
    /// <remarks>
    /// Lookup is by user name, matching eShop <c>AccountController.Login</c>.
    /// The check calls <c>CheckPasswordSignInAsync</c> with lockout on failure.
    /// That is the password check inside <c>PasswordSignInAsync</c>.
    /// This method does not issue a cookie or a token.
    /// </remarks>
    Task<Guid?> CheckUserNamePasswordAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Demo payment card copied from eShop.
/// These values live only on the login record. They are never written to events.
/// </summary>
/// <param name="CardHolderName">Name on the card.</param>
/// <param name="CardNumber">Card number.</param>
/// <param name="CardType">Card type.</param>
/// <param name="Expiration">Card expiration, MM/YY.</param>
/// <param name="SecurityNumber">Card security number.</param>
public sealed record ShopperPaymentCard(
    string CardHolderName,
    string CardNumber,
    int CardType,
    string Expiration,
    string SecurityNumber);
