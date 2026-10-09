using LawnDart;

namespace LawnDart.Samples.EShop.Identity.Commands;

/// <summary>
/// Clears the shopper authentication session.
/// </summary>
/// <remarks>
/// POST /api/identity/sign-out-shopper.
/// The handler appends Shopper Signed Out for an existing active account.
/// It does not clear a cookie or a token. <see cref="LogoutId"/> is not applied.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="AccountId">Account ending the session.</param>
/// <param name="LogoutId">Duende logout context id if present.</param>
public sealed record SignOutShopperCommand(
    Guid Id,
    Guid AccountId,
    string? LogoutId = null) : ICommand;
