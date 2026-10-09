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
        new("CardHolderName", "nvarchar(max)", "TEXT"),
        new("CardNumber", "nvarchar(max)", "TEXT"),
        new("CardType", "int", "INTEGER"),
        new("Expiration", "nvarchar(max)", "TEXT"),
        new("SecurityNumber", "nvarchar(max)", "TEXT")
    ];

    /// <summary>
    /// Adds each card column that is not already on <c>AspNetUsers</c>.
    /// A second call changes nothing.
    /// </summary>
    /// <param name="db">Login database.</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    /// <returns>A task that completes when every card column is present.</returns>
    /// <remarks>
    /// SQL Server uses <c>COL_LENGTH</c> before <c>ALTER TABLE ADD</c>.
    /// SQLite checks <c>pragma_table_info</c> and then adds the column.
    /// The columns stay nullable. They are not written to events.
    /// </remarks>
    public static async Task AddMissingAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var column in Columns)
            {
                var sql =
                    "IF COL_LENGTH('dbo.AspNetUsers', '" + column.Name + "') IS NULL " +
                    "ALTER TABLE dbo.AspNetUsers ADD " + column.Name + " " + column.SqlServerType + " NULL;";
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
            }

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

    private readonly record struct CardColumn(string Name, string SqlServerType, string SqliteType);
}
