using LawnDart;

namespace LawnDart.Samples.EShop.Identity.Commands;

/// <summary>
/// Creates a new shopper account with credentials and basic profile. Produces Shopper Account Registered.
/// </summary>
/// <remarks>
/// POST /api/identity/register-shopper-account.
/// </remarks>
/// <param name="Id">Command identifier.</param>
/// <param name="AccountId">Identifier to assign to the new account.</param>
/// <param name="UserName">Desired login name.</param>
/// <param name="Email">Email address.</param>
/// <param name="Password">Chosen password (never store on events).</param>
/// <param name="Name">Given name.</param>
/// <param name="LastName">Family name.</param>
public sealed record RegisterShopperAccountCommand(
    Guid Id,
    Guid AccountId,
    string UserName,
    string Email,
    string Password,
    string Name,
    string LastName) : ICommand
{
    /// <summary>
    /// Formats the command without the password.
    /// </summary>
    /// <returns>The command fields. The password is <c>[redacted]</c>.</returns>
    public override string ToString() =>
        $"RegisterShopperAccountCommand {{ Id = {Id}, AccountId = {AccountId}, UserName = {UserName}, Email = {Email}, Password = [redacted], Name = {Name}, LastName = {LastName} }}";
}
