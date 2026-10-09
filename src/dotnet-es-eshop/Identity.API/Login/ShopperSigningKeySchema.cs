using Microsoft.EntityFrameworkCore;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Creates <c>ShopperSigningKeys</c> when an older login database does not have it.
/// </summary>
public static class ShopperSigningKeySchema
{
    /// <summary>
    /// Creates the table when it is missing. A second call changes nothing.
    /// </summary>
    /// <param name="db">Login database.</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    /// <returns>A task that completes when the table is present.</returns>
    /// <remarks>
    /// <c>EnsureCreated</c> skips schema creation when the database already has tables.
    /// SQL Server checks <c>OBJECT_ID</c> before <c>CREATE TABLE</c>.
    /// SQLite uses <c>CREATE TABLE IF NOT EXISTS</c>.
    /// </remarks>
    public static async Task AddMissingAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            const string sql =
                """
                IF OBJECT_ID(N'dbo.ShopperSigningKeys', N'U') IS NULL
                CREATE TABLE dbo.ShopperSigningKeys (
                    Id nvarchar(64) NOT NULL CONSTRAINT PK_ShopperSigningKeys PRIMARY KEY,
                    Pkcs8PrivateKey varbinary(max) NOT NULL,
                    CreatedAtUtc datetime2 NOT NULL
                );
                """;
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            const string sql =
                """
                CREATE TABLE IF NOT EXISTS ShopperSigningKeys (
                    Id TEXT NOT NULL CONSTRAINT PK_ShopperSigningKeys PRIMARY KEY,
                    Pkcs8PrivateKey BLOB NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                """;
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException(
            "Shopper signing keys are not set up for provider '" + provider + "'.");
    }
}
