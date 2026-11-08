using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace LawnDart.Samples.EShop.Database.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class IdentityLoginDatabaseTests
{
    private readonly DatabaseFixture _database;

    public IdentityLoginDatabaseTests(DatabaseFixture database) => _database = database;

    [DatabaseFact]
    public async Task Login_schema_setup_is_idempotent_and_keeps_one_signing_key()
    {
        var connectionString = await _database.CreateDatabaseAsync("identity_login");
        using var host = DatabaseHosts.IdentityLogin(_database.Database, connectionString);

        await host.InitializeShopperLoginStoreAsync();
        var first = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);
        Assert.Equal(1, await CountSigningKeysAsync(connectionString));
        AssertCardColumns(first);

        await host.InitializeShopperLoginStoreAsync();
        var second = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);

        Assert.Equal(first, second);
        Assert.Equal(1, await CountSigningKeysAsync(connectionString));
    }

    private void AssertCardColumns(string snapshot)
    {
        foreach (var column in ShopperLoginCardColumns.ColumnNames)
        {
            var line = _database.Database == EShopDatabase.Postgres
                ? "column public.AspNetUsers." + column
                : "column dbo.AspNetUsers." + column;
            Assert.Contains(line, snapshot, StringComparison.Ordinal);
        }

        var signing = _database.Database == EShopDatabase.Postgres
            ? "column public.ShopperSigningKeys.Id"
            : "column dbo.ShopperSigningKeys.Id";
        Assert.Contains(signing, snapshot, StringComparison.Ordinal);
    }

    private async Task<long> CountSigningKeysAsync(string connectionString)
    {
        if (_database.Database == EShopDatabase.Postgres)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"ShopperSigningKeys\"";
            return (long)(await command.ExecuteScalarAsync() ?? 0L);
        }

        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var sqlCommand = sql.CreateCommand();
        sqlCommand.CommandText = "SELECT COUNT(*) FROM dbo.ShopperSigningKeys";
        return Convert.ToInt64(await sqlCommand.ExecuteScalarAsync());
    }
}
