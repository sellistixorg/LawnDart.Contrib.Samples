using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Outbox;
using LawnDart.Messaging.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopRabbitHostTests
{
    private const string SqlConnectionString =
        "Server=localhost;Database=eshop-wave2;User Id=sa;Password=placeholder;TrustServerCertificate=True";

    private const string RabbitConnectionString = "amqp://guest:guest@localhost:5672";

    [Fact]
    public void Subscription_queues_are_distinct_per_service()
    {
        var ordering = EShopRabbitMq.QueueName(EShopServiceNames.Ordering);
        var processor = EShopRabbitMq.QueueName(EShopServiceNames.OrderProcessor);

        Assert.Equal("eshop.ordering", ordering);
        Assert.Equal("eshop.order-processor", processor);
        Assert.NotEqual(ordering, processor);
        Assert.Equal("sample-catalog-item-added", EShopRabbitMq.RoutingKey(typeof(SampleCatalogItemAdded)));
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
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(SqlServerInboxStore));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IOutboxPublisher));
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(InMemoryMessageTransport));
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

        Assert.Contains(EShopRabbitMq.ConnectionName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rabbit_topology_init_returns_when_the_transport_is_absent()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();
        await provider.InitializeEShopRabbitMqAsync();
    }

    [Fact]
    public void Codec_round_trips_event_body_and_message_context()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var occurred = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var enqueued = new DateTimeOffset(2026, 10, 7, 12, 0, 1, TimeSpan.Zero);
        var source = new SampleCatalogItemAdded(id, occurred);
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

        var properties = RabbitMqMessageCodec.ToProperties(context, "sample-catalog-item-added", "catalog");
        var restored = RabbitMqMessageCodec.DeserializeBody<SampleCatalogItemAdded>(
            RabbitMqMessageCodec.SerializeBody(source));
        var restoredContext = RabbitMqMessageCodec.ToContext(properties);

        Assert.Equal(id, restored.Id);
        Assert.Equal(occurred, restored.Timestamp);
        Assert.Equal("outbox-1", restoredContext.MessageId);
        Assert.Equal("corr-1", restoredContext.CorrelationId);
        Assert.Equal("cause-1", restoredContext.CausationId);
        Assert.Equal("tenant-1", restoredContext.TenantId);
        Assert.Equal("user-1", restoredContext.UserId);
        Assert.Equal(enqueued, restoredContext.EnqueuedAt);
        Assert.Equal(RabbitMqMessageCodec.TransportType, restoredContext.TransportType);
        Assert.Equal("00-abc-def-01", restoredContext.Headers["traceparent"]);
        Assert.Equal("catalog", properties.AppId);
        Assert.Equal(DeliveryModes.Persistent, properties.DeliveryMode);
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{EShopSqlConnections.Catalog}"] = SqlConnectionString,
            [$"ConnectionStrings:{EShopRabbitMq.ConnectionName}"] = RabbitConnectionString
        });
        return builder;
    }
}
