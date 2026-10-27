using LawnDart;
using LawnDart.EventSourcing;
using LawnDart.EventSourcing.Outbox;
using LawnDart.EventSourcing.Postgres.EventStore;
using LawnDart.EventSourcing.Postgres.Outbox;
using LawnDart.EventSourcing.SqlServer.EventStore;
using LawnDart.EventSourcing.SqlServer.Outbox;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Messaging.Postgres;
using LawnDart.Messaging.SqlServer;
using LawnDart.Outbox;
using LawnDart.Projections.Checkpoints;
using LawnDart.Projections.Lightweight.Postgres;
using LawnDart.Projections.Lightweight.SqlServer;
using LawnDart.Projections.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopLawnDartHostTests
{
    private const string SqlConnectionString =
        "Server=localhost;Database=eshop-wave1;User Id=sa;Password=placeholder;TrustServerCertificate=True";

    private const string PostgresConnectionString =
        "Host=localhost;Database=eshop-wave1;Username=postgres;Password=placeholder";

    [Fact]
    public void AddEShopLawnDart_without_connection_string_throws()
    {
        var builder = Host.CreateApplicationBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddEShopLawnDart(EShopDatabases.Catalog, context => context.AddCatalogContext()));

        Assert.Contains(EShopDatabases.Catalog, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEShopLawnDart_with_unknown_database_throws()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [EShopDatabaseSetting.ConfigurationKey] = "Oracle",
            [$"ConnectionStrings:{EShopDatabases.Catalog}"] = SqlConnectionString
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddEShopLawnDart(EShopDatabases.Catalog, context => context.AddCatalogContext()));

        Assert.Contains("Oracle", ex.Message, StringComparison.Ordinal);
        Assert.Contains("SqlServer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Postgres", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_setting_registers_sql_server_stores()
    {
        using var host = BuildHost(database: null, context => context.AddCatalogContext());

        AssertRegistrations(host.Services, EShopDatabase.SqlServer);
    }

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Empty_hook_registers_database_stores_and_stand_in_catalog(EShopDatabase database)
    {
        using var host = BuildHost(database, context => context.AddCatalogContext());

        AssertRegistrations(host.Services, database);

        var catalog = host.Services.GetRequiredKeyedService<IEventTypeCatalog>(EShopLawnDartHost.ContextName);
        Assert.True(catalog.TryResolveType(EShopHostCatalog.ReadyToken, out _));
    }

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Hook_with_event_types_omits_the_stand_in_catalog(EShopDatabase database)
    {
        using var host = BuildHost(database, context => context.WithEventTypes(typeof(SampleCatalogItemAdded)));

        AssertRegistrations(host.Services, database);

        var catalog = host.Services.GetRequiredKeyedService<IEventTypeCatalog>(EShopLawnDartHost.ContextName);
        Assert.True(catalog.TryResolveType("sample-catalog-item-added", out var eventType));
        Assert.Equal(typeof(SampleCatalogItemAdded), eventType);
        Assert.False(catalog.TryResolveType(EShopHostCatalog.ReadyToken, out _));
    }

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Outbox_processor_is_off_when_the_host_opts_out(EShopDatabase database)
    {
        using var polling = BuildHost(database, context => context.AddCatalogContext(), enableOutbox: true);
        using var quiet = BuildHost(database, context => context.AddCatalogContext(), enableOutbox: false);

        Assert.Contains(polling.Services.GetServices<IHostedService>(), service => service is OutboxProcessor);
        Assert.DoesNotContain(quiet.Services.GetServices<IHostedService>(), service => service is OutboxProcessor);
    }

    [Fact]
    public async Task Schema_setup_rejects_a_host_without_a_database_event_store()
    {
        var services = new ServiceCollection();
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddBoundedContext(EShopLawnDartHost.ContextName)
            .WithEventTypes(typeof(SampleCatalogItemAdded))
            .UseInMemory();
        await using var provider = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.InitializeEShopLawnDartSchemaAsync());

        Assert.Contains("SqlServerEventStore or PostgresEventStore", ex.Message, StringComparison.Ordinal);
    }

    private static IHost BuildHost(
        EShopDatabase? database,
        Action<BoundedContextBuilder> configureContext,
        bool enableOutbox = true)
    {
        var builder = Host.CreateApplicationBuilder();
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopDatabases.Catalog}"] = database == EShopDatabase.Postgres
                ? PostgresConnectionString
                : SqlConnectionString
        };
        if (database is not null)
            values[EShopDatabaseSetting.ConfigurationKey] = database.Value.ToString();
        builder.Configuration.AddInMemoryCollection(values);
        builder.AddEShopLawnDart(
            EShopDatabases.Catalog,
            configureContext,
            subscriptionClient: null,
            enableOutbox: enableOutbox);
        return builder.Build();
    }

    private static void AssertRegistrations(IServiceProvider services, EShopDatabase database)
    {
        var store = services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);
        var outbox = services.GetRequiredKeyedService<IOutboxWriter>(EShopLawnDartHost.ContextName);
        var views = services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName);
        var checkpoints = services.GetRequiredKeyedService<ICheckpointStore>(EShopLawnDartHost.ContextName);
        var inbox = services.GetRequiredService<IInboxStore>();

        switch (database)
        {
            case EShopDatabase.SqlServer:
                Assert.IsType<SqlServerEventStore>(store);
                Assert.IsType<SqlServerOutboxWriter>(outbox);
                Assert.IsType<SqlViewStore>(views);
                Assert.IsType<SqlCheckpointStore>(checkpoints);
                Assert.IsType<SqlServerInboxStore>(inbox);
                break;
            case EShopDatabase.Postgres:
                Assert.IsType<PostgresEventStore>(store);
                Assert.IsType<PostgresOutboxWriter>(outbox);
                Assert.IsType<PostgresProjectionStore>(views);
                Assert.IsType<PostgresProjectionStore>(checkpoints);
                Assert.IsType<PostgresInboxStore>(inbox);
                Assert.Equal(
                    EShopLawnDartHost.PostgresSchemaName,
                    services.GetRequiredKeyedService<PostgresProjectionStoreOptions>(EShopLawnDartHost.ContextName).SchemaName);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(database), database, null);
        }

        Assert.IsType<InMemoryMessageTransport>(services.GetRequiredService<IMessageTransport>());
        Assert.IsType<MessageTransportOutboxPublisher>(services.GetRequiredService<IOutboxPublisher>());
    }
}

[EventTypeName("sample-catalog-item-added")]
public sealed record SampleCatalogItemAdded(Guid Id, DateTime Timestamp) : IEvent;
