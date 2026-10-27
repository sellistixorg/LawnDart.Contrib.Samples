using Microsoft.EntityFrameworkCore;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Creates <c>ShopperSigningKeys</c> when an older login database does not have it.
/// </summary>
public static class ShopperSigningKeySchema
{
    private const string SqlServerSql =
        """
        IF OBJECT_ID(N'dbo.ShopperSigningKeys', N'U') IS NULL
        CREATE TABLE dbo.ShopperSigningKeys (
            Id nvarchar(64) NOT NULL CONSTRAINT PK_ShopperSigningKeys PRIMARY KEY,
            Pkcs8PrivateKey varbinary(max) NOT NULL,
            CreatedAtUtc datetime2 NOT NULL
        );
        """;

    // Npgsql creates tables with quoted names, so the names here are quoted too.
    // CreatedAtUtc is a timestamp with time zone, which is how Npgsql stores a UTC DateTime.
    private const string PostgresSql =
        """
        CREATE TABLE IF NOT EXISTS "ShopperSigningKeys" (
            "Id" character varying(64) NOT NULL CONSTRAINT "PK_ShopperSigningKeys" PRIMARY KEY,
            "Pkcs8PrivateKey" bytea NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        """;

    private const string SqliteSql =
        """
        CREATE TABLE IF NOT EXISTS ShopperSigningKeys (
            Id TEXT NOT NULL CONSTRAINT PK_ShopperSigningKeys PRIMARY KEY,
            Pkcs8PrivateKey BLOB NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """;

    /// <summary>
    /// Creates the table when it is missing. A second call changes nothing.
    /// </summary>
    /// <param name="db">Login database.</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    /// <returns>A task that completes when the table is present.</returns>
    /// <remarks>
    /// <c>EnsureCreated</c> skips schema creation when the database already has tables.
    /// SQL Server checks <c>OBJECT_ID</c> before <c>CREATE TABLE</c>.
    /// Postgres and SQLite use <c>CREATE TABLE IF NOT EXISTS</c>.
    /// </remarks>
    public static async Task AddMissingAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var provider = db.Database.ProviderName ?? "";
        var sql = SqlFor(provider)
            ?? throw new InvalidOperationException(
                "Shopper signing keys are not set up for provider '" + provider + "'.");
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the statement that creates the table for <paramref name="providerName"/>,
    /// or <see langword="null"/> when the provider is not SQL Server, Postgres, or SQLite.
    /// </summary>
    /// <param name="providerName">The Entity Framework Core provider name, from <c>Database.ProviderName</c>.</param>
    /// <returns>The statement, or <see langword="null"/>.</returns>
    public static string? SqlFor(string providerName)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            return SqlServerSql;
        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return PostgresSql;
        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return SqliteSql;
        return null;
    }
}
