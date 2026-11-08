using System.Data;
using Microsoft.EntityFrameworkCore;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Reads the login tables. It does not create them.
/// </summary>
public static class ShopperLoginSchemaCheck
{
    /// <summary>
    /// Names that must exist before Identity accepts requests.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindMissingAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var provider = db.Database.ProviderName ?? "";
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var tables = await ReadNamesAsync(connection, TableSql(provider), cancellationToken).ConfigureAwait(false);
        var columns = tables.Contains("AspNetUsers")
            ? await ReadNamesAsync(connection, ColumnSql(provider), cancellationToken).ConfigureAwait(false)
            : new HashSet<string>(StringComparer.Ordinal);

        var missing = new List<string>();
        if (!tables.Contains("AspNetUsers"))
            missing.Add("AspNetUsers");
        foreach (var column in ShopperLoginCardColumns.ColumnNames)
        {
            if (!columns.Contains(column))
                missing.Add("AspNetUsers." + column);
        }

        if (!tables.Contains("ShopperSigningKeys"))
            missing.Add("ShopperSigningKeys");

        return missing;
    }

    private static string TableSql(string provider)
    {
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return """
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME IN ('AspNetUsers', 'ShopperSigningKeys')
                """;
        }

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return """
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name IN ('AspNetUsers', 'ShopperSigningKeys')
                """;
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return """
                SELECT name
                FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ('AspNetUsers', 'ShopperSigningKeys')
                """;
        }

        throw new InvalidOperationException(
            "Login schema check is not set up for provider '" + provider + "'.");
    }

    private static string ColumnSql(string provider)
    {
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return """
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'AspNetUsers'
                """;
        }

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return """
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'AspNetUsers'
                """;
        }

        return "SELECT name FROM pragma_table_info('AspNetUsers')";
    }

    private static async Task<HashSet<string>> ReadNamesAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names.Add(reader.GetString(0));
        return names;
    }
}
