using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.AppHost.Tests;

/// <summary>
/// Builds the AppHost resource list without starting it, and checks which database container it adds.
/// No container is started and Docker is not needed.
/// </summary>
public class AppHostDatabaseTests
{
    private static readonly string[] DatabaseNames =
    [
        EShopDatabases.Identity,
        EShopDatabases.IdentityLogin,
        EShopDatabases.Catalog,
        EShopDatabases.Basket,
        EShopDatabases.Ordering,
        EShopDatabases.Payment,
        EShopDatabases.Webhooks
    ];

    private static readonly string[] Projects =
    [
        "identity-api",
        "basket-api",
        "catalog-api",
        "ordering-api",
        "order-processor",
        "payment-processor",
        "webhooks-api",
        "webhooksclient",
        "webapp"
    ];

    [Fact]
    public async Task No_setting_starts_sql_server()
    {
        await using var builder = await CreateAsync([]);

        AssertSqlServer(builder);
        await AssertEveryProjectGetsAsync(builder, EShopDatabase.SqlServer);
    }

    [Theory]
    [InlineData("--database", "postgres")]
    [InlineData("--database=Postgres")]
    [InlineData("--EShop:Database", "Postgres")]
    public async Task Postgres_from_the_command_line_starts_postgres(params string[] args)
    {
        await using var builder = await CreateAsync(args);

        AssertPostgres(builder);
        await AssertEveryProjectGetsAsync(builder, EShopDatabase.Postgres);
    }

    [Theory]
    [InlineData("--database", "sqlserver")]
    [InlineData("--EShop:Database", "SqlServer")]
    public async Task Sql_server_from_the_command_line_starts_sql_server(params string[] args)
    {
        await using var builder = await CreateAsync(args);

        AssertSqlServer(builder);
        await AssertEveryProjectGetsAsync(builder, EShopDatabase.SqlServer);
    }

    [Fact]
    public async Task Launch_option_wins_over_the_setting()
    {
        await using var builder = await CreateAsync(["--EShop:Database", "SqlServer", "--database", "postgres"]);

        AssertPostgres(builder);
        await AssertEveryProjectGetsAsync(builder, EShopDatabase.Postgres);
    }

    [Fact]
    public async Task Unknown_database_stops_with_the_allowed_values()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => CreateAsync(["--database", "oracle"]));

        var message = Flatten(ex);
        Assert.Contains("SqlServer", message, StringComparison.Ordinal);
        Assert.Contains("Postgres", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Postgres_container_uses_the_pinned_image_tag()
    {
        await using var builder = await CreateAsync(["--database", "postgres"]);

        var server = Assert.Single(builder.Resources.OfType<PostgresServerResource>());
        Assert.True(server.TryGetLastAnnotation<ContainerImageAnnotation>(out var image));
        Assert.Equal(EShopDatabaseEnvironment.PostgresImageTag, image.Tag);
    }

    private static Task<IDistributedApplicationTestingBuilder> CreateAsync(string[] args) =>
        DistributedApplicationTestingBuilder.CreateAsync<Projects.LawnDart_Samples_EShop_AppHost>(
            args,
            (options, _) => options.DisableDashboard = true);

    private static void AssertSqlServer(IDistributedApplicationTestingBuilder builder)
    {
        var server = Assert.Single(builder.Resources.OfType<SqlServerServerResource>());
        Assert.Equal(EShopDatabaseEnvironment.SqlServerResourceName, server.Name);
        Assert.Empty(builder.Resources.OfType<PostgresServerResource>());
        Assert.Empty(builder.Resources.OfType<PostgresDatabaseResource>());

        var databases = builder.Resources.OfType<SqlServerDatabaseResource>().ToArray();
        Assert.Equal(DatabaseNames.Order(StringComparer.Ordinal), databases.Select(d => d.Name).Order(StringComparer.Ordinal));
        Assert.All(databases, d => Assert.Same(server, d.Parent));
    }

    private static void AssertPostgres(IDistributedApplicationTestingBuilder builder)
    {
        var server = Assert.Single(builder.Resources.OfType<PostgresServerResource>());
        Assert.Equal(EShopDatabaseEnvironment.PostgresResourceName, server.Name);
        Assert.Empty(builder.Resources.OfType<SqlServerServerResource>());
        Assert.Empty(builder.Resources.OfType<SqlServerDatabaseResource>());

        var databases = builder.Resources.OfType<PostgresDatabaseResource>().ToArray();
        Assert.Equal(DatabaseNames.Order(StringComparer.Ordinal), databases.Select(d => d.Name).Order(StringComparer.Ordinal));
        Assert.All(databases, d => Assert.Same(server, d.Parent));
    }

    private static async Task AssertEveryProjectGetsAsync(
        IDistributedApplicationTestingBuilder builder,
        EShopDatabase database)
    {
        var projects = builder.Resources.OfType<ProjectResource>().ToArray();
        Assert.Equal(Projects.Order(StringComparer.Ordinal), projects.Select(p => p.Name).Order(StringComparer.Ordinal));

        foreach (var project in projects)
        {
            var values = await EnvironmentAsync(project);
            Assert.True(
                values.TryGetValue(EShopDatabaseEnvironment.VariableName, out var value),
                project.Name + " should get " + EShopDatabaseEnvironment.VariableName + ".");
            Assert.Equal(database.ToString(), value);
        }
    }

    private static async Task<Dictionary<string, string>> EnvironmentAsync(ProjectResource project)
    {
        var values = await project.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);
        return new Dictionary<string, string>(values);
    }

    private static string Flatten(Exception ex)
    {
        var messages = new List<string>();
        for (Exception? current = ex; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" ", messages);
    }
}
