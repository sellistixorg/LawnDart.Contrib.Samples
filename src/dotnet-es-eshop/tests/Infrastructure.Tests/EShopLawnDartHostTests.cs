using LawnDart;
using LawnDart.EventSourcing.Outbox;
using LawnDart.EventSourcing.SqlServer.EventStore;
using LawnDart.EventSourcing.SqlServer.Outbox;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Messaging.SqlServer;
using LawnDart.Outbox;
using LawnDart.Projections.Checkpoints;
using LawnDart.Projections.Lightweight.SqlServer;
using LawnDart.Projections.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopLawnDartHostTests
{
    private const string ConnectionString =
        "Server=localhost;Database=eshop-wave1;User Id=sa;Password=placeholder;TrustServerCertificate=True";

    [Fact]
    public void AddEShopLawnDart_without_connection_string_throws()
    {
        var builder = Host.CreateApplicationBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddEShopLawnDart(EShopSqlConnections.Catalog, context => context.AddCatalogContext()));

        Assert.Contains(EShopSqlConnections.Catalog, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_hook_registers_sql_stores_and_stand_in_catalog()
    {
        using var host = BuildHost(context => context.AddCatalogContext());

        AssertSqlRegistrations(host.Services);

        var catalog = host.Services.GetRequiredKeyedService<IEventTypeCatalog>(EShopLawnDartHost.ContextName);
        Assert.True(catalog.TryResolveType(EShopHostCatalog.ReadyToken, out _));
    }

    [Fact]
    public void Hook_with_event_types_omits_the_stand_in_catalog()
    {
        using var host = BuildHost(context => context.WithEventTypes(typeof(SampleCatalogItemAdded)));

        AssertSqlRegistrations(host.Services);

        var catalog = host.Services.GetRequiredKeyedService<IEventTypeCatalog>(EShopLawnDartHost.ContextName);
        Assert.True(catalog.TryResolveType("sample-catalog-item-added", out var eventType));
        Assert.Equal(typeof(SampleCatalogItemAdded), eventType);
        Assert.False(catalog.TryResolveType(EShopHostCatalog.ReadyToken, out _));
    }

    [Fact]
    public void Outbox_processor_is_off_when_the_host_opts_out()
    {
        using var polling = BuildHost(context => context.AddCatalogContext(), enableOutbox: true);
        using var quiet = BuildHost(context => context.AddCatalogContext(), enableOutbox: false);

        Assert.Contains(polling.Services.GetServices<IHostedService>(), service => service is OutboxProcessor);
        Assert.DoesNotContain(quiet.Services.GetServices<IHostedService>(), service => service is OutboxProcessor);
    }

    private static IHost BuildHost(Action<BoundedContextBuilder> configureContext, bool enableOutbox = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopSqlConnections.Catalog}"] = ConnectionString
        });
        builder.AddEShopLawnDart(
            EShopSqlConnections.Catalog,
            configureContext,
            subscriptionClient: null,
            enableOutbox: enableOutbox);
        return builder.Build();
    }

    private static void AssertSqlRegistrations(IServiceProvider services)
    {
        Assert.IsType<SqlServerEventStore>(
            services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName));
        Assert.IsType<SqlServerOutboxWriter>(
            services.GetRequiredKeyedService<IOutboxWriter>(EShopLawnDartHost.ContextName));
        Assert.IsType<SqlViewStore>(
            services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName));
        Assert.IsType<SqlCheckpointStore>(
            services.GetRequiredKeyedService<ICheckpointStore>(EShopLawnDartHost.ContextName));
        Assert.IsType<SqlServerInboxStore>(services.GetRequiredService<IInboxStore>());
        Assert.IsType<InMemoryMessageTransport>(services.GetRequiredService<IMessageTransport>());
        Assert.IsType<MessageTransportOutboxPublisher>(services.GetRequiredService<IOutboxPublisher>());
    }
}

[EventTypeName("sample-catalog-item-added")]
public sealed record SampleCatalogItemAdded(Guid Id, DateTime Timestamp) : IEvent;
