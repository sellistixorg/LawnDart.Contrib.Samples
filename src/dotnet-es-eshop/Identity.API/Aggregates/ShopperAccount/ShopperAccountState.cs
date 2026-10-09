using LawnDart;

namespace LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;

/// <summary>
/// Decision state for a shopper account.
/// </summary>
public sealed partial class ShopperAccountState : IState
{
    /// <summary>
    /// Unique account identifier.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Login user name.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// Email address.
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// Given name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Family name.
    /// </summary>
    public string LastName { get; set; } = "";

    /// <summary>
    /// Default shipping street.
    /// </summary>
    public string? Street { get; set; }

    /// <summary>
    /// Default shipping city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Default shipping state.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Default shipping country.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Default shipping postal code.
    /// </summary>
    public string? ZipCode { get; set; }

    /// <summary>
    /// Contact phone.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Whether the account can sign in.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// True after Shopper Account Registered is applied.
    /// </summary>
    public bool Exists { get; set; }
}
