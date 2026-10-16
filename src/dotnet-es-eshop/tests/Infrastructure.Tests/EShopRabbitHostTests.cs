using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.RabbitMQ;
using LawnDart.Outbox;
using LawnDart.Messaging.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopRabbitHostTests
{
    private const string SqlConnectionString =
        "Server=localhost;Database=eshop-wave2;User Id=sa;Password=placeholder;TrustServerCertificate=True";

    private const string RabbitConnectionString = "amqp://guest:guest@localhost:5672";

    [Fact]
    public void Subscription_queues_are_distinct_per_service()
    {
        var token = MessageTypeNames.Require(typeof(SampleCatalogItemAdded));
        var ordering = RabbitMqNames.Queue(token, EShopServiceNames.Ordering);
        var processor = RabbitMqNames.Queue(token, EShopServiceNames.OrderProcessor);

        Assert.Equal("sample-catalog-item-added.ordering", ordering);
        Assert.Equal("sample-catalog-item-added.order-processor", processor);
        Assert.NotEqual(ordering, processor);
        Assert.Equal(
            EShopLawnDartHost.IntegrationExchangeName,
            new SharedTopicMapper(EShopLawnDartHost.IntegrationExchangeName).TopicFor(token));
    }

    [Fact]
    public void Rabbit_host_keeps_sql_inbox_and_outbox_publisher()
    {
        var builder = CreateBuilder();
        builder.AddEShopLawnDart(
            EShopSqlConnections.Catalog,
            context => context.AddCatalogContext(),
            EShopServiceNames.Catalog);

        Assert.Contains(builder.Services, d => d.ServiceType == typeof(RabbitMqMessageTransport));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IMessageTransport));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IMessageTopologySetup));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(SqlServerInboxStore));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IOutboxPublisher));
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(InMemoryMessageTransport));

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMqTransportOptions>>().Value;
        Assert.Equal(EShopServiceNames.Catalog, options.SubscriptionName);
        Assert.Equal(RabbitConnectionString, options.Uri);
        Assert.True(options.SetupTopology);
        Assert.Equal(5, options.MaxDeliveryAttempts);
        Assert.True(options.DeadLetterFailedMessages);
        Assert.Equal(
            EShopLawnDartHost.IntegrationExchangeName,
            options.TopicMapper.TopicFor("order-paid"));
        Assert.IsType<RabbitMqMessageTransport>(provider.GetRequiredService<IMessageTransport>());
    }

    [Fact]
    public void Rabbit_host_without_eventbus_connection_throws()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopSqlConnections.Catalog}"] = SqlConnectionString
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddEShopLawnDart(
                EShopSqlConnections.Catalog,
                context => context.AddCatalogContext(),
                EShopServiceNames.Catalog));

        Assert.Contains(EShopLawnDartHost.EventBusConnectionName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void In_memory_host_does_not_register_rabbit()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopSqlConnections.Catalog}"] = SqlConnectionString
        });
        builder.AddEShopLawnDart(
            EShopSqlConnections.Catalog,
            context => context.AddCatalogContext());

        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(RabbitMqMessageTransport));
        using var provider = builder.Services.BuildServiceProvider();
        Assert.IsType<InMemoryMessageTransport>(provider.GetRequiredService<IMessageTransport>());
    }

    [Fact]
    public void Envelope_round_trips_message_context()
    {
        var enqueued = new DateTimeOffset(2026, 10, 7, 12, 0, 1, TimeSpan.Zero);
        var context = new MessageContext
        {
            MessageId = "outbox-1",
            CorrelationId = "corr-1",
            CausationId = "cause-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            EnqueuedAt = enqueued,
            Headers = new Dictionary<string, string> { ["traceparent"] = "00-abc-def-01" }
        };

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        MessageEnvelopeHeaders.Write(context, "sample-catalog-item-added", headers);
        var restored = MessageEnvelopeHeaders.Read(headers, RabbitMqMessageTransport.TransportTypeName).Context;

        Assert.Equal("outbox-1", restored.MessageId);
        Assert.Equal("corr-1", restored.CorrelationId);
        Assert.Equal("cause-1", restored.CausationId);
        Assert.Equal("tenant-1", restored.TenantId);
        Assert.Equal("user-1", restored.UserId);
        Assert.Equal(enqueued, restored.EnqueuedAt);
        Assert.Equal(RabbitMqMessageTransport.TransportTypeName, restored.TransportType);
        Assert.Equal("00-abc-def-01", restored.Headers["traceparent"]);
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopSqlConnections.Catalog}"] = SqlConnectionString,
            [$"ConnectionStrings:{EShopLawnDartHost.EventBusConnectionName}"] = RabbitConnectionString
        });
        return builder;
    }
}
