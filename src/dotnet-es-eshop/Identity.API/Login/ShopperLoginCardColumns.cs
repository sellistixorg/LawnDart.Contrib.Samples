using System.Data;
using Microsoft.EntityFrameworkCore;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Adds the nullable demo card columns when an older login database has <c>AspNetUsers</c> without them.
/// </summary>
public static class ShopperLoginCardColumns
{
    private static readonly CardColumn[] Columns =
    [
        new("CardHolderName", "nvarchar(max)", "text", "TEXT"),
        new("CardNumber", "nvarchar(max)", "text", "TEXT"),
        new("CardType", "int", "integer", "INTEGER"),
        new("Expiration", "nvarchar(max)", "text", "TEXT"),
        new("SecurityNumber", "nvarchar(max)", "text", "TEXT")
    ];

    /// <summary>
    /// Names of the card columns, in the order they are added.
    /// </summary>
    public static IReadOnlyList<string> ColumnNames { get; } = Columns.Select(column => column.Name).ToArray();

    /// <summary>
    /// Adds each card column that is not already on <c>AspNetUsers</c>.
    /// A second call changes nothing.
    /// </summary>
    /// <param name="db">Login database.</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    /// <returns>A task that completes when every card column is present.</returns>
    /// <remarks>
    /// SQL Server uses <c>COL_LENGTH</c> before <c>ALTER TABLE ADD</c>.
    /// Postgres uses <c>ALTER TABLE ADD COLUMN IF NOT EXISTS</c>.
    /// SQLite checks <c>pragma_table_info</c> and then adds the column.
    /// The columns stay nullable. They are not written to events.
    /// </remarks>
    public static async Task AddMissingAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var provider = db.Database.ProviderName ?? "";
        var statements = StatementsFor(provider);
        if (statements is not null)
        {
            foreach (var sql in statements)
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);

            return;
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var column in Columns)
            {
                if (await SqliteHasColumnAsync(db, column.Name, cancellationToken).ConfigureAwait(false))
                    continue;

                var sql = "ALTER TABLE AspNetUsers ADD COLUMN " + column.Name + " " + column.SqliteType + " NULL;";
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        throw new InvalidOperationException(
            "Login card columns are not set up for provider '" + provider + "'.");
    }

    /// <summary>
    /// Returns the statements that add the card columns for <paramref name="providerName"/>,
    /// or <see langword="null"/> when the provider has no fixed statements.
    /// </summary>
    /// <param name="providerName">The Entity Framework Core provider name, from <c>Database.ProviderName</c>.</param>
    /// <returns>One statement for each card column on SQL Server or Postgres. <see langword="null"/> for SQLite and other providers.</returns>
    /// <remarks>
    /// Each statement checks for the column first, so running all of them again changes nothing.
    /// SQLite needs a lookup before each column, so it is handled in <see cref="AddMissingAsync"/>.
    /// </remarks>
    public static IReadOnlyList<string>? StatementsFor(string providerName)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return Columns
                .Select(column =>
                    "IF COL_LENGTH('dbo.AspNetUsers', '" + column.Name + "') IS NULL " +
                    "ALTER TABLE dbo.AspNetUsers ADD " + column.Name + " " + column.SqlServerType + " NULL;")
                .ToArray();
        }

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            // Npgsql creates the Identity tables with quoted names, so the names here are quoted too.
            return Columns
                .Select(column =>
                    "ALTER TABLE \"AspNetUsers\" ADD COLUMN IF NOT EXISTS \"" + column.Name + "\" " +
                    column.PostgresType + " NULL;")
                .ToArray();
        }

        return null;
    }

    private static async Task<bool> SqliteHasColumnAsync(
        ApplicationDbContext db,
        string columnName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM pragma_table_info('AspNetUsers') WHERE name = $name LIMIT 1";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = columnName;
            command.Parameters.Add(parameter);
            var found = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return found is not null and not DBNull;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private readonly record struct CardColumn(string Name, string SqlServerType, string PostgresType, string SqliteType);
}
