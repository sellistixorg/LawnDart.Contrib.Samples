using LawnDart;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Startup seed switch. The configuration section is <c>EShop:Seed</c>.
/// </summary>
public sealed class EShopSeedOptions
{
    /// <summary>
    /// Configuration section path.
    /// </summary>
    public const string SectionName = "EShop:Seed";

    /// <summary>
    /// When true, the host fills catalog or identity data that is not stored yet.
    /// The value is true when the section is missing.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Decides which command rejections mean the seeded row is already stored.
/// </summary>
public static class EShopSeedRejections
{
    /// <summary>
    /// True when the command lost because that brand, type, item, email, or user name is already stored.
    /// </summary>
    /// <param name="exception">Rejection from a command handler.</param>
    /// <returns>True when startup should keep going.</returns>
    public static bool IsAlreadyPresent(DomainException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception.Title is not null
            && exception.Title.Contains("ALREADY", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return exception.Message.Contains("already", StringComparison.OrdinalIgnoreCase);
    }
}
