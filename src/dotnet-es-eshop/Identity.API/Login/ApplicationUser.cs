using Microsoft.AspNetCore.Identity;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Login user for a shopper account.
/// The row stores the user name, email, password hash, and the demo payment card.
/// <see cref="IdentityUser.Id"/> is the shopper account id.
/// </summary>
/// <remarks>
/// CardHolderName, CardNumber, CardType, Expiration, and SecurityNumber are demo values copied from eShop.
/// They live only on this login record. They are never written to events.
/// </remarks>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Demo card number. Stored only on the login record.
    /// </summary>
    public string? CardNumber { get; set; }

    /// <summary>
    /// Demo card security number. Stored only on the login record.
    /// </summary>
    public string? SecurityNumber { get; set; }

    /// <summary>
    /// Demo card expiration, MM/YY. Stored only on the login record.
    /// </summary>
    public string? Expiration { get; set; }

    /// <summary>
    /// Demo name on the card. Stored only on the login record.
    /// </summary>
    public string? CardHolderName { get; set; }

    /// <summary>
    /// Demo card type. Stored only on the login record.
    /// </summary>
    public int? CardType { get; set; }
}
