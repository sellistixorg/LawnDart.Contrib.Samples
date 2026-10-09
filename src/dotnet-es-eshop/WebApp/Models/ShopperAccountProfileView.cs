namespace LawnDart.Samples.EShop.WebApp.Models;

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

    /// <summary>
    /// Given name and family name for the header.
    /// </summary>
    /// <param name="profile">Shopper Account Profile. Null when the account is unknown.</param>
    /// <returns>The display name, or an empty string when <paramref name="profile"/> is null.</returns>
    public static string FormatDisplayName(ShopperAccountProfileView? profile)
    {
        if (profile is null)
            return "";

        return string.Join(
            ' ',
            new[] { profile.Name, profile.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
