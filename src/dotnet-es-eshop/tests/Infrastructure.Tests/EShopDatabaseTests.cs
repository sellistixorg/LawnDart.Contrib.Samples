using LawnDart.EventSourcing.Postgres.EventStore;
using LawnDart.EventStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopDatabaseTests
{
    [Fact]
    public void Missing_setting_is_sql_server()
    {
        var configuration = Build();

        Assert.Equal(EShopDatabase.SqlServer, configuration.ReadEShopDatabase());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_setting_is_sql_server(string value)
    {
        var configuration = Build(new() { [EShopDatabaseSetting.ConfigurationKey] = value });

        Assert.Equal(EShopDatabase.SqlServer, configuration.ReadEShopDatabase());
    }

    [Theory]
    [InlineData("SqlServer", EShopDatabase.SqlServer)]
    [InlineData("sqlserver", EShopDatabase.SqlServer)]
    [InlineData("SQLSERVER", EShopDatabase.SqlServer)]
    [InlineData("Postgres", EShopDatabase.Postgres)]
    [InlineData("postgres", EShopDatabase.Postgres)]
    [InlineData(" POSTGRES ", EShopDatabase.Postgres)]
    public void Setting_matches_both_names_with_case_ignored(string value, EShopDatabase expected)
    {
        var configuration = Build(new() { [EShopDatabaseSetting.ConfigurationKey] = value });

        Assert.Equal(expected, configuration.ReadEShopDatabase());
    }

    [Theory]
    [InlineData("Oracle")]
    [InlineData("PostgreSQL")]
    [InlineData("1")]
    [InlineData("0")]
    public void Unknown_setting_throws_with_the_allowed_values(string value)
    {
        var configuration = Build(new() { [EShopDatabaseSetting.ConfigurationKey] = value });

        var ex = Assert.Throws<InvalidOperationException>(() => configuration.ReadEShopDatabase());

        Assert.Contains(EShopDatabaseSetting.ConfigurationKey, ex.Message, StringComparison.Ordinal);
        Assert.Contains($"'{value}'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'SqlServer'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'Postgres'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { "--database", "postgres" }, EShopDatabase.Postgres)]
    [InlineData(new[] { "--database", "Postgres" }, EShopDatabase.Postgres)]
    [InlineData(new[] { "--DATABASE", "POSTGRES" }, EShopDatabase.Postgres)]
    [InlineData(new[] { "--database=postgres" }, EShopDatabase.Postgres)]
    [InlineData(new[] { "--database", "sqlserver" }, EShopDatabase.SqlServer)]
    [InlineData(new[] { "--urls", "http://localhost:5000", "--database", "postgres" }, EShopDatabase.Postgres)]
    public void Launch_option_sets_the_database(string[] args, EShopDatabase expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddEShopDatabaseLaunchOption(args)
            .Build();

        Assert.Equal(expected, configuration.ReadEShopDatabase());
        Assert.Equal(expected.ToString(), configuration[EShopDatabaseSetting.ConfigurationKey]);
    }

    [Fact]
    public void Launch_option_wins_over_the_setting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [EShopDatabaseSetting.ConfigurationKey] = "SqlServer"
            })
            .AddEShopDatabaseLaunchOption(["--database", "postgres"])
            .Build();

        Assert.Equal(EShopDatabase.Postgres, configuration.ReadEShopDatabase());
    }

    [Fact]
    public void No_launch_option_leaves_the_setting_alone()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [EShopDatabaseSetting.ConfigurationKey] = "Postgres"
            })
            .AddEShopDatabaseLaunchOption(["--urls", "http://localhost:5000"])
            .Build();

        Assert.Equal(EShopDatabase.Postgres, configuration.ReadEShopDatabase());
    }

    [Fact]
    public void No_args_keeps_the_default()
    {
        var configuration = new ConfigurationBuilder()
            .AddEShopDatabaseLaunchOption([])
            .Build();

        Assert.Equal(EShopDatabase.SqlServer, configuration.ReadEShopDatabase());
    }

    [Theory]
    [InlineData(new[] { "--database", "oracle" }, "'oracle'")]
    [InlineData(new[] { "--database=1" }, "'1'")]
    public void Unknown_launch_option_value_throws_with_the_allowed_values(string[] args, string quoted)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ConfigurationBuilder().AddEShopDatabaseLaunchOption(args));

        Assert.Contains(EShopDatabaseSetting.LaunchOption, ex.Message, StringComparison.Ordinal);
        Assert.Contains(quoted, ex.Message, StringComparison.Ordinal);
        Assert.Contains("'SqlServer'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'Postgres'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--database")]
    [InlineData("--database=")]
    [InlineData("--database --urls http://localhost:5000")]
    public void Launch_option_without_a_value_throws(string commandLine)
    {
        var args = commandLine.Split(' ');
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ConfigurationBuilder().AddEShopDatabaseLaunchOption(args));

        Assert.Contains("needs a value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Launch_option_given_twice_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ConfigurationBuilder().AddEShopDatabaseLaunchOption(["--database", "postgres", "--database=sqlserver"]));

        Assert.Contains("more than once", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_built_with_the_launch_option_registers_postgres()
    {
        string[] args = ["--database", "postgres"];
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddEShopDatabaseLaunchOption(args);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopDatabases.Catalog}"] =
                "Host=localhost;Database=eshop-wave1;Username=postgres;Password=placeholder"
        });

        builder.AddEShopLawnDart(EShopDatabases.Catalog, context => context.AddCatalogContext());
        using var host = builder.Build();

        Assert.IsType<PostgresEventStore>(
            host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName));
    }

    private static IConfiguration Build(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? [])
            .Build();
}
