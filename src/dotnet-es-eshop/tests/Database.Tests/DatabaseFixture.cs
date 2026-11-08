using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Data.SqlClient;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace LawnDart.Samples.EShop.Database.Tests;

/// <summary>
/// One database server for the test run. Each test asks for its own empty database on that server.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private MsSqlContainer? _sql;
    private PostgreSqlContainer? _postgres;
    private string? _adminConnectionString;

    public EShopDatabase Database { get; private set; }

    public async Task InitializeAsync()
    {
        if (!DatabaseSession.IsSelected)
        {
            if (DatabaseSession.Require)
            {
                throw new InvalidOperationException(
                    DatabaseSession.RequireVariable + "=1 requires " + DatabaseSession.DatabaseVariable +
                    " set to SqlServer or Postgres.");
            }

            return;
        }

        Database = DatabaseSession.Database;
        if (Database == EShopDatabase.Postgres)
        {
            _postgres = new PostgreSqlBuilder("postgres:" + EShopDatabaseSetting.PostgresImageTag)
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
            await _postgres.StartAsync().ConfigureAwait(false);
            _adminConnectionString = _postgres.GetConnectionString();
            return;
        }

        _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sql.StartAsync().ConfigureAwait(false);
        _adminConnectionString = _sql.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_sql is not null)
            await _sql.DisposeAsync().ConfigureAwait(false);
        if (_postgres is not null)
            await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new empty database and returns a connection string for it.
    /// </summary>
    /// <param name="name">Short name. Letters, digits, and underscores.</param>
    public async Task<string> CreateDatabaseAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '_'))
            throw new ArgumentException("Database name must be letters, digits, and underscores.", nameof(name));

        var databaseName = "eshop_" + name;
        if (Database == EShopDatabase.Postgres)
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE DATABASE " + databaseName;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);

            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = databaseName
            };
            return builder.ConnectionString;
        }

        await using (var connection = new SqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE DATABASE " + databaseName;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var sqlBuilder = new SqlConnectionStringBuilder(_adminConnectionString)
        {
            InitialCatalog = databaseName
        };
        return sqlBuilder.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "eshop-database";
}
