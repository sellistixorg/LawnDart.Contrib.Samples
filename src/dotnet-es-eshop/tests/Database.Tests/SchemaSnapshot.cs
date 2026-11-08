using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace LawnDart.Samples.EShop.Database.Tests;

/// <summary>
/// Column list and row counts for the user tables in one database.
/// A second schema setup must leave this text unchanged.
/// </summary>
internal static class SchemaSnapshot
{
    public static async Task<string> ReadAsync(EShopDatabase database, string connectionString)
    {
        var lines = database == EShopDatabase.Postgres
            ? await ReadPostgresAsync(connectionString).ConfigureAwait(false)
            : await ReadSqlServerAsync(connectionString).ConfigureAwait(false);
        return string.Join('\n', lines);
    }

    private static async Task<List<string>> ReadSqlServerAsync(string connectionString)
    {
        var lines = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var columns = connection.CreateCommand())
        {
            columns.CommandText =
                """
                SELECT s.name, t.name, c.column_id, c.name
                FROM sys.columns c
                JOIN sys.tables t ON t.object_id = c.object_id
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                ORDER BY s.name, t.name, c.column_id
                """;
            await using var reader = await columns.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                lines.Add("column " + reader.GetString(0) + "." + reader.GetString(1) + "." + reader.GetString(3));
            }
        }

        await using (var rows = connection.CreateCommand())
        {
            rows.CommandText =
                """
                SELECT s.name, t.name, SUM(p.rows)
                FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
                GROUP BY s.name, t.name
                ORDER BY s.name, t.name
                """;
            await using var reader = await rows.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                lines.Add("rows " + reader.GetString(0) + "." + reader.GetString(1) + "=" + reader.GetInt64(2));
            }
        }

        return lines;
    }

    private static async Task<List<string>> ReadPostgresAsync(string connectionString)
    {
        var lines = new List<string>();
        var tables = new List<(string Schema, string Table)>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var columns = connection.CreateCommand())
        {
            columns.CommandText =
                """
                SELECT n.nspname, c.relname, a.attnum, a.attname
                FROM pg_attribute a
                JOIN pg_class c ON c.oid = a.attrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE a.attnum > 0
                  AND NOT a.attisdropped
                  AND c.relkind = 'r'
                  AND n.nspname <> 'pg_catalog'
                  AND n.nspname <> 'information_schema'
                ORDER BY n.nspname, c.relname, a.attnum
                """;
            await using var reader = await columns.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var schema = reader.GetString(0);
                var table = reader.GetString(1);
                lines.Add("column " + schema + "." + table + "." + reader.GetString(3));
                if (tables.Count == 0 || tables[^1].Schema != schema || tables[^1].Table != table)
                    tables.Add((schema, table));
            }
        }

        foreach (var (schema, table) in tables)
        {
            await using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM \"" + schema.Replace("\"", "\"\"", StringComparison.Ordinal)
                + "\".\"" + table.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            var value = (long)(await count.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
            lines.Add("rows " + schema + "." + table + "=" + value);
        }

        return lines;
    }
}
