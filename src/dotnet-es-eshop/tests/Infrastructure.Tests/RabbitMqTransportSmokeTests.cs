using System.Text.Json;
using LawnDart;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Outbox;
using LawnDart.Patterns.Reaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

/// <summary>
/// Outbox publisher to RabbitMQ to a second service queue.
/// Runs only when a broker is reachable. Set ESHOP_REQUIRE_RABBITMQ=1 to fail when it is not.
/// </summary>
public class RabbitMqTransportSmokeTests
{
    [Fact]
    public async Task Outbox_publisher_delivers_to_in_memory_subscribers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryMessaging();
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IMessageTransport>();
        var seen = new TaskCompletionSource<SampleCatalogItemAdded>(TaskCreationOptions.RunContinuationsAsynchronously);
        await transport.SubscribeAsync<SampleCatalogItemAdded>((message, _, _) =>
        {
            seen.TrySetResult(message);
            return Task.CompletedTask;
        });

        var id = Guid.NewGuid();
        await PublishOutboxAsync(transport, id, DateTime.UtcNow);

        var delivered = await seen.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(id, delivered.Id);
    }

    [Fact]
    public async Task Outbox_publish_reaches_two_service_queues()
    {
        await using var broker = await RabbitBroker.TryConnectAsync();
        if (broker is null)
        {
            RequireBrokerOrReturn();
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exchange = "eshop.smoke." + suffix;
        await using var ordering = await broker.OpenTransportAsync("ordering-" + suffix, exchange);
        await using var processor = await broker.OpenTransportAsync("processor-" + suffix, exchange);

        var orderingHit = new TaskCompletionSource<SampleCatalogItemAdded>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processorHit = new TaskCompletionSource<SampleCatalogItemAdded>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var orderingStop = new CancellationTokenSource();
        using var processorStop = new CancellationTokenSource();
        var orderingSubscribe = ordering.Transport.SubscribeAsync<SampleCatalogItemAdded>(
            (message, _, _) =>
            {
                orderingHit.TrySetResult(message);
                return Task.CompletedTask;
            },
            orderingStop.Token);
        var processorSubscribe = processor.Transport.SubscribeAsync<SampleCatalogItemAdded>(
            (message, _, _) =>
            {
                processorHit.TrySetResult(message);
                return Task.CompletedTask;
            },
            processorStop.Token);

        await WaitForHandlersAsync(ordering.Transport, processor.Transport);

        var id = Guid.NewGuid();
        var occurred = DateTime.UtcNow;
        await PublishOutboxAsync(ordering.Transport, id, occurred);

        var fromOrdering = await orderingHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var fromProcessor = await processorHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(id, fromOrdering.Id);
        Assert.Equal(id, fromProcessor.Id);

        orderingStop.Cancel();
        processorStop.Cancel();
        await Task.WhenAll(SwallowCancel(orderingSubscribe), SwallowCancel(processorSubscribe));
        await broker.DeleteExchangeAsync(exchange);
    }

    [Fact]
    public async Task Competing_consumers_on_one_queue_deliver_once()
    {
        await using var broker = await RabbitBroker.TryConnectAsync();
        if (broker is null)
        {
            RequireBrokerOrReturn();
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exchange = "eshop.smoke." + suffix;
        var client = "shared-" + suffix;
        await using var first = await broker.OpenTransportAsync(client, exchange);
        await using var second = await broker.OpenTransportAsync(client, exchange);
        var hits = 0;
        var firstHit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();

        Task Count(SampleCatalogItemAdded _, MessageContext __, CancellationToken ___)
        {
            if (Interlocked.Increment(ref hits) == 1)
                firstHit.TrySetResult();
            return Task.CompletedTask;
        }

        var firstSubscribe = first.Transport.SubscribeAsync<SampleCatalogItemAdded>(Count, stop.Token);
        var secondSubscribe = second.Transport.SubscribeAsync<SampleCatalogItemAdded>(Count, stop.Token);
        await WaitForHandlersAsync(first.Transport, second.Transport);

        await PublishOutboxAsync(first.Transport, Guid.NewGuid(), DateTime.UtcNow);
        await firstHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(400);
        Assert.Equal(1, Volatile.Read(ref hits));

        stop.Cancel();
        await Task.WhenAll(SwallowCancel(firstSubscribe), SwallowCancel(secondSubscribe));
        await broker.DeleteExchangeAsync(exchange);
    }

    [Fact]
    public async Task Reactor_inbox_drops_a_replayed_outbox_message()
    {
        await using var broker = await RabbitBroker.TryConnectAsync();
        if (broker is null)
        {
            RequireBrokerOrReturn();
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exchange = "eshop.smoke." + suffix;
        await using var publisher = await broker.OpenTransportAsync("pub-" + suffix, exchange);
        await using var consumer = await broker.OpenTransportAsync("sub-" + suffix, exchange);
        var reactor = new RecordingReactor();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(consumer.Transport);
        builder.Services.AddSingleton<IMessageTransport>(consumer.Transport);
        builder.Services.AddInMemoryMessaging();
        builder.Services.AddSingleton(reactor);
        builder.Services.AddReactor<RecordingReactor, SampleCatalogItemAdded>();
        using var host = builder.Build();
        await host.StartAsync();
        await WaitUntilAsync(() => consumer.Transport.HandlerCount<SampleCatalogItemAdded>() > 0);

        var outboxId = Guid.NewGuid();
        await PublishOutboxAsync(publisher.Transport, Guid.NewGuid(), DateTime.UtcNow, outboxId);
        await WaitUntilAsync(() => reactor.Count == 1);
        await PublishOutboxAsync(publisher.Transport, Guid.NewGuid(), DateTime.UtcNow, outboxId);
        await Task.Delay(500);

        Assert.Equal(1, reactor.Count);
        await host.StopAsync();
        await broker.DeleteExchangeAsync(exchange);
    }

    private static void RequireBrokerOrReturn()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("ESHOP_REQUIRE_RABBITMQ"), "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ESHOP_REQUIRE_RABBITMQ=1 but RabbitMQ is not reachable. " +
                "Set ESHOP_RABBITMQ or start a broker at amqp://guest:guest@127.0.0.1:5672.");
        }
    }

    private static async Task PublishOutboxAsync(
        IMessageTransport transport,
        Guid eventId,
        DateTime occurred,
        Guid? outboxId = null)
    {
        var publisher = new MessageTransportOutboxPublisher(transport, [typeof(SampleCatalogItemAdded)]);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SampleCatalogItemAdded(eventId, occurred));
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = outboxId ?? Guid.NewGuid(),
            EventType = "sample-catalog-item-added",
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = payload,
            Metadata = "{}",
            CreatedAt = occurred,
            StreamId = "smoke:" + eventId.ToString("N"),
            SequencePosition = 1
        });
    }

    private static async Task WaitForHandlersAsync(params RabbitMqMessageTransport[] transports)
    {
        await WaitUntilAsync(() => transports.All(transport => transport.HandlerCount<SampleCatalogItemAdded>() > 0));
    }

    private static async Task WaitUntilAsync(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Timed out waiting for the RabbitMQ subscription.");
            await Task.Delay(50);
        }
    }

    private static async Task SwallowCancel(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class RecordingReactor : IReactor<SampleCatalogItemAdded>
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public Task<IEnumerable<ICommand>> ReactAsync(
            SampleCatalogItemAdded @event,
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult<IEnumerable<ICommand>>([]);
        }
    }

    private sealed class RabbitBroker : IAsyncDisposable
    {
        private readonly string _uri;
        private readonly IConnection _probe;

        private RabbitBroker(string uri, IConnection probe)
        {
            _uri = uri;
            _probe = probe;
        }

        public static async Task<RabbitBroker?> TryConnectAsync()
        {
            var uri = Environment.GetEnvironmentVariable("ESHOP_RABBITMQ")
                ?? "amqp://guest:guest@127.0.0.1:5672";
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var connection = await CreateFactory(uri, "eshop-smoke-probe").CreateConnectionAsync(cts.Token);
                return new RabbitBroker(uri, connection);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<OpenTransport> OpenTransportAsync(string subscriptionClient, string exchange)
        {
            var connection = await CreateFactory(_uri, subscriptionClient).CreateConnectionAsync();
            var transport = new RabbitMqMessageTransport(
                connection,
                new RabbitMqTransportOptions(subscriptionClient, exchange),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RabbitMqMessageTransport>.Instance);
            return new OpenTransport(connection, transport);
        }

        public async Task DeleteExchangeAsync(string exchange)
        {
            try
            {
                await using var channel = await _probe.CreateChannelAsync();
                await channel.ExchangeDeleteAsync(exchange);
            }
            catch (Exception)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _probe.DisposeAsync();
        }

        private static ConnectionFactory CreateFactory(string uri, string clientName) => new()
        {
            Uri = new Uri(uri),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(2),
            ClientProvidedName = clientName
        };
    }

    private sealed class OpenTransport(IConnection connection, RabbitMqMessageTransport transport) : IAsyncDisposable
    {
        public RabbitMqMessageTransport Transport { get; } = transport;

        public async ValueTask DisposeAsync()
        {
            var queueName = Transport.QueueName;
            await Transport.DisposeAsync();
            try
            {
                await using var channel = await connection.CreateChannelAsync();
                await channel.QueueDeleteAsync(queueName);
            }
            catch (Exception)
            {
            }

            await connection.DisposeAsync();
        }
    }
}
