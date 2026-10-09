using LawnDart;

namespace LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;

/// <summary>
/// Decision state for a reserved email.
/// </summary>
public sealed class ShopperEmailState : IState
{
    /// <summary>
    /// Email address in lower case.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Identifier of the account that reserved the email.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// True after the email is reserved.
    /// </summary>
    public bool Exists { get; set; }
}
