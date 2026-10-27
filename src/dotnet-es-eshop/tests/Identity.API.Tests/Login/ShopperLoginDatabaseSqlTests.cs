using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Login;

/// <summary>
/// Checks the hand-written login store statements for SQL Server and Postgres without opening a database.
/// </summary>
public class ShopperLoginDatabaseSqlTests
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string PostgresProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private const string SqlConnectionString =
        "Server=localhost;Database=identitylogindb;User Id=sa;Password=placeholder;TrustServerCertificate=True";

    private const string PostgresConnectionString =
        "Host=localhost;Database=identitylogindb;Username=postgres;Password=placeholder";

    [Fact]
    public void Postgres_card_columns_use_add_column_if_not_exists()
    {
        var statements = ShopperLoginCardColumns.StatementsFor(PostgresProvider);

        Assert.NotNull(statements);
        Assert.Equal(ShopperLoginCardColumns.ColumnNames.Count, statements.Count);
        foreach (var (name, sql) in ShopperLoginCardColumns.ColumnNames.Zip(statements))
        {
            Assert.StartsWith("ALTER TABLE \"AspNetUsers\" ADD COLUMN IF NOT EXISTS \"" + name + "\" ", sql, StringComparison.Ordinal);
            Assert.EndsWith(" NULL;", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("dbo.", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Sql_server_card_columns_keep_the_col_length_check()
    {
        var statements = ShopperLoginCardColumns.StatementsFor(SqlServerProvider);

        Assert.NotNull(statements);
        Assert.Equal(ShopperLoginCardColumns.ColumnNames.Count, statements.Count);
        foreach (var (name, sql) in ShopperLoginCardColumns.ColumnNames.Zip(statements))
        {
            Assert.StartsWith("IF COL_LENGTH('dbo.AspNetUsers', '" + name + "') IS NULL ", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("IF NOT EXISTS", sql, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("Some.Other.Provider")]
    public void Card_columns_have_no_fixed_statements_for_other_providers(string provider) =>
        Assert.Null(ShopperLoginCardColumns.StatementsFor(provider));

    [Fact]
    public void Postgres_signing_key_table_uses_create_table_if_not_exists()
    {
        var sql = ShopperSigningKeySchema.SqlFor(PostgresProvider);

        Assert.NotNull(sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"ShopperSigningKeys\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"Pkcs8PrivateKey\" bytea NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("\"CreatedAtUtc\" timestamp with time zone NOT NULL", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("OBJECT_ID", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Signing_key_table_picks_the_statement_for_each_provider()
    {
        Assert.Contains("OBJECT_ID(N'dbo.ShopperSigningKeys'", ShopperSigningKeySchema.SqlFor(SqlServerProvider), StringComparison.Ordinal);
        Assert.Contains("BLOB", ShopperSigningKeySchema.SqlFor("Microsoft.EntityFrameworkCore.Sqlite"), StringComparison.Ordinal);
        Assert.Null(ShopperSigningKeySchema.SqlFor("Some.Other.Provider"));
    }

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Card_column_types_match_the_types_entity_framework_creates(EShopDatabase database)
    {
        using var db = CreateContext(database);
        var table = FindTable(db, "AspNetUsers");
        var statements = ShopperLoginCardColumns.StatementsFor(db.Database.ProviderName!);
        Assert.NotNull(statements);

        foreach (var (name, sql) in ShopperLoginCardColumns.ColumnNames.Zip(statements))
        {
            var column = table.FindColumn(name);
            Assert.NotNull(column);
            Assert.True(column.IsNullable, name + " should be nullable.");
            var expected = database == EShopDatabase.Postgres
                ? "\"" + name + "\" " + column.StoreType + " NULL;"
                : name + " " + column.StoreType + " NULL;";
            Assert.EndsWith(expected, sql, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Signing_key_column_types_match_the_types_entity_framework_creates(EShopDatabase database)
    {
        using var db = CreateContext(database);
        var table = FindTable(db, "ShopperSigningKeys");
        var sql = ShopperSigningKeySchema.SqlFor(db.Database.ProviderName!);
        Assert.NotNull(sql);

        foreach (var name in new[] { "Id", "Pkcs8PrivateKey", "CreatedAtUtc" })
        {
            var column = table.FindColumn(name);
            Assert.NotNull(column);
            var expected = database == EShopDatabase.Postgres
                ? "\"" + name + "\" " + column.StoreType + " NOT NULL"
                : name + " " + column.StoreType + " NOT NULL";
            Assert.Contains(expected, sql, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(EShopDatabase.SqlServer, SqlServerProvider)]
    [InlineData(EShopDatabase.Postgres, PostgresProvider)]
    public void Login_store_registration_follows_the_database_setting(EShopDatabase database, string provider)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [EShopDatabaseSetting.ConfigurationKey] = database.ToString(),
            [$"ConnectionStrings:{EShopDatabases.IdentityLogin}"] = ConnectionString(database)
        });

        builder.AddShopperLoginStore();
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(provider, db.Database.ProviderName);
    }

    [Fact]
    public void Login_store_registration_follows_the_launch_option()
    {
        string[] args = ["--database", "postgres"];
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopDatabases.IdentityLogin}"] = PostgresConnectionString
        });
        builder.Configuration.AddEShopDatabaseLaunchOption(args);

        builder.AddShopperLoginStore();
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Assert.Equal(PostgresProvider, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ProviderName);
    }

    [Fact]
    public void Login_store_registration_without_a_connection_string_names_the_database()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [EShopDatabaseSetting.ConfigurationKey] = nameof(EShopDatabase.Postgres)
        });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddShopperLoginStore());

        Assert.Contains(EShopDatabases.IdentityLogin, ex.Message, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext(EShopDatabase database)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        if (database == EShopDatabase.Postgres)
            options.UseNpgsql(PostgresConnectionString);
        else
            options.UseSqlServer(SqlConnectionString);
        return new ApplicationDbContext(options.Options);
    }

    private static ITable FindTable(ApplicationDbContext db, string name)
    {
        var table = db.Model.GetRelationalModel().Tables.SingleOrDefault(t => t.Name == name);
        Assert.NotNull(table);
        return table;
    }

    private static string ConnectionString(EShopDatabase database) =>
        database == EShopDatabase.Postgres ? PostgresConnectionString : SqlConnectionString;
}
