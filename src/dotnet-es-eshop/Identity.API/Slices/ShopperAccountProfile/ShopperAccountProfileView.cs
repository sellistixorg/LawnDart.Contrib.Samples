namespace LawnDart.Samples.EShop.Identity.API.Slices.ShopperAccountProfile;

/// <summary>
/// Projection of shopper account identity and default address fields.
/// </summary>
public sealed class ShopperAccountProfileView
{
    /// <summary>
    /// Account id.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Login name.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// Email.
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
    /// Street.
    /// </summary>
    public string? Street { get; set; }

    /// <summary>
    /// City.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Country.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Postal code.
    /// </summary>
    public string? ZipCode { get; set; }

    /// <summary>
    /// Phone.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Active flag.
    /// </summary>
    public bool IsActive { get; set; }
}
