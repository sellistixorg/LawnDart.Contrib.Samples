using LawnDart;

namespace LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;

/// <summary>
/// Reserves a trimmed email for one new shopper account.
/// </summary>
/// <param name="Id">Command identifier.</param>
/// <param name="Email">Email address.</param>
/// <param name="AccountId">Identifier to assign to the new account.</param>
public sealed record ReserveShopperEmailCommand(Guid Id, string Email, Guid AccountId) : ICommand;
