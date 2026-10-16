using System.Text.Json;
using LawnDart;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Messaging.RabbitMQ;
using LawnDart.Outbox;
using LawnDart.Patterns.Reaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

/// <summary>
/// Outbox publisher to the LawnDart RabbitMQ transport, then a second subscription.
/// Runs only when a broker is reachable. Set ESHOP_REQUIRE_RABBITMQ=1 to fail when it is not.
/// </summary>
public class RabbitMqTransportSmokeTests
{
    private const string EventName = "sample-catalog-item-added";

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
        var broker = await RabbitBroker.TryConnectAsync();
        if (broker is null)
        {
            RequireBrokerOrReturn();
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exchange = "eshop.smoke." + suffix;
        var orderingName = "ordering-" + suffix;
        var processorName = "processor-" + suffix;
        await using var ordering = await broker.OpenAsync(orderingName, exchange);
        await using var processor = await broker.OpenAsync(processorName, exchange);
        var orderingTransport = ordering.GetRequiredService<IMessageTransport>();
        var processorTransport = processor.GetRequiredService<IMessageTransport>();

        var orderingHit = new TaskCompletionSource<SampleCatalogItemAdded>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processorHit = new TaskCompletionSource<SampleCatalogItemAdded>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var orderingSubscribe = orderingTransport.SubscribeAsync<SampleCatalogItemAdded>(
            (message, _, _) =>
            {
                orderingHit.TrySetResult(message);
                return Task.CompletedTask;
            },
            stop.Token);
        var processorSubscribe = processorTransport.SubscribeAsync<SampleCatalogItemAdded>(
            (message, _, _) =>
            {
                processorHit.TrySetResult(message);
                return Task.CompletedTask;
            },
            stop.Token);

        var id = Guid.NewGuid();
        var occurred = DateTime.UtcNow;
        await PublishOutboxAsync(orderingTransport, id, occurred);

        var fromOrdering = await orderingHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var fromProcessor = await processorHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(id, fromOrdering.Id);
        Assert.Equal(id, fromProcessor.Id);

        stop.Cancel();
        await Task.WhenAll(SwallowCancel(orderingSubscribe), SwallowCancel(processorSubscribe));
        await broker.DeleteSubscriptionAsync(exchange, orderingName);
        await broker.DeleteSubscriptionAsync(exchange, processorName);
    }

    [Fact]
    public async Task Competing_consumers_on_one_queue_deliver_once()
    {
        var broker = await RabbitBroker.TryConnectAsync();
        if (broker is null)
        {
            RequireBrokerOrReturn();
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exchange = "eshop.smoke." + suffix;
        var client = "shared-" + suffix;
        await using var first = await broker.OpenAsync(client, exchange);
        await using var second = await broker.OpenAsync(client, exchange);
        var firstTransport = first.GetRequiredService<IMessageTransport>();
        var secondTransport = second.GetRequiredService<IMessageTransport>();
        var hits = 0;
        var firstHit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();

        Task Count(SampleCatalogItemAdded _, MessageContext __, CancellationToken ___)
        {
            if (Interlocked.Increment(ref hits) == 1)
                firstHit.TrySetResult();
            return Task.CompletedTask;
        }

        var firstSubscribe = firstTransport.SubscribeAsync<SampleCatalogItemAdded>(Count, stop.Token);
        var secondSubscribe = secondTransport.SubscribeAsync<SampleCatalogItemAdded>(Count, stop.Token);

        await PublishOutboxAsync(firstTransport, Guid.NewGuid(), DateTime.UtcNow);
        await firstHit.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(400);
        Assert.Equal(1, Volatile.Read(ref hits));

        stop.Cancel();
        await Task.WhenAll(SwallowCancel(firstSubscribe), SwallowCancel(secondSubscribe));
        await broker.DeleteSubscriptionAsync(exchange, client);
    }

    [Fact]
    public async Task Failed_handler_dead_letters_after_five_attempts()
    {
        var broker = await RabbitBroker.TryConnectAsync();
        if (broker is null)
        {
            RequireBrokerOrReturn();
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exchange = "eshop.smoke." + suffix;
        var subscription = "dlq-" + suffix;
        await using var provider = await broker.OpenAsync(subscription, exchange, options =>
        {
            options.MaxDeliveryAttempts = 5;
            options.DeadLetterFailedMessages = true;
            options.RetryDelay = TimeSpan.FromMilliseconds(200);
        });
        var transport = provider.GetRequiredService<IMessageTransport>();
        var attempts = 0;
        using var stop = new CancellationTokenSource();
        var subscribe = transport.SubscribeAsync<SampleCatalogItemAdded>(
            (_, _, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("always fail");
            },
            stop.Token);

        var id = Guid.NewGuid();
        await PublishOutboxAsync(transport, id, DateTime.UtcNow);

        var deadLetterQueue = RabbitMqNames.DeadLetterQueue(subscription);
        var deadLetterExchange = RabbitMqNames.DeadLetterExchange(subscription);
        Assert.Equal(subscription + ".dlq", deadLetterQueue);
        Assert.Equal(subscription + ".dlx", deadLetterExchange);

        var body = await broker.WaitForMessageAsync(deadLetterQueue, TimeSpan.FromSeconds(15));
        var message = JsonSerializer.Deserialize<SampleCatalogItemAdded>(body, MessagingJson.Options);
        Assert.NotNull(message);
        Assert.Equal(id, message.Id);
        Assert.Equal(5, Volatile.Read(ref attempts));

        stop.Cancel();
        await SwallowCancel(subscribe);
        await broker.DeleteSubscriptionAsync(exchange, subscription);
    }

    [Fact]
    public async Task Reactor_inbox_drops_a_replayed_outbox_message()
    {
        var broker = await RabbitBroker.TryConnectAsync();
        if (broker is null)
        {
            RequireBrokerOrReturn();
            return;
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var exchange = "eshop.smoke." + suffix;
        var publisherName = "pub-" + suffix;
        var consumerName = "sub-" + suffix;
        await using var publisher = await broker.OpenAsync(publisherName, exchange);
        await using var consumer = await broker.OpenAsync(consumerName, exchange);
        var consumerTransport = consumer.GetRequiredService<IMessageTransport>();
        var reactor = new RecordingReactor();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(consumerTransport);
        builder.Services.AddSingleton<IMessageTransport>(consumerTransport);
        builder.Services.AddInMemoryMessaging();
        builder.Services.AddSingleton(reactor);
        builder.Services.AddReactor<RecordingReactor, SampleCatalogItemAdded>();
        using var host = builder.Build();
        await host.StartAsync();
        await WaitForQueueAsync(
            broker,
            RabbitMqNames.Queue(consumerName, RabbitMqQueueLayout.Subscription, EventName));

        var outboxId = Guid.NewGuid();
        await PublishOutboxAsync(publisher.GetRequiredService<IMessageTransport>(), Guid.NewGuid(), DateTime.UtcNow, outboxId);
        await WaitUntilAsync(() => reactor.Count == 1);
        await PublishOutboxAsync(publisher.GetRequiredService<IMessageTransport>(), Guid.NewGuid(), DateTime.UtcNow, outboxId);
        await Task.Delay(500);

        Assert.Equal(1, reactor.Count);
        await host.StopAsync();
        await broker.DeleteSubscriptionAsync(exchange, publisherName);
        await broker.DeleteSubscriptionAsync(exchange, consumerName);
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
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new SampleCatalogItemAdded(eventId, occurred),
            MessagingJson.Options);
        await publisher.PublishAsync(new OutboxMessage
        {
            Id = outboxId ?? Guid.NewGuid(),
            EventType = EventName,
            SchemaVersion = 1,
            CodecId = EventCodec.Json,
            Payload = payload,
            Metadata = "{}",
            CreatedAt = occurred,
            StreamId = "smoke:" + eventId.ToString("N"),
            SequencePosition = 1
        });
    }

    private static async Task WaitForQueueAsync(RabbitBroker broker, string queue)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await broker.QueueExistsAsync(queue))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Timed out waiting for queue {queue}.");
            await Task.Delay(50);
        }
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

    private sealed class RabbitBroker
    {
        private readonly string _uri;

        private RabbitBroker(string uri) => _uri = uri;

        public static async Task<RabbitBroker?> TryConnectAsync()
        {
            var uri = Environment.GetEnvironmentVariable("ESHOP_RABBITMQ")
                ?? "amqp://guest:guest@127.0.0.1:5672";
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(uri),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(2)
                };
                await using var connection = await factory.CreateConnectionAsync(cts.Token);
                return new RabbitBroker(uri);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<ServiceProvider> OpenAsync(
            string subscriptionName,
            string exchange,
            Action<RabbitMqTransportOptions>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddRabbitMqTransport(options =>
            {
                options.Uri = _uri;
                options.SubscriptionName = subscriptionName;
                options.TopicMapper = new SharedTopicMapper(exchange);
                options.SetupTopology = true;
                options.UnroutablePublish = RabbitMqUnroutablePublish.Throw;
                configure?.Invoke(options);
            });
            var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<IMessageTransport>();
            return provider;
        }

        public async Task<bool> QueueExistsAsync(string queue)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(_uri) };
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();
                await channel.QueueDeclareAsync(
                    queue: queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    passive: true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<byte[]> WaitForMessageAsync(string queue, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            var factory = new ConnectionFactory { Uri = new Uri(_uri) };
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var delivery = await channel.BasicGetAsync(queue, autoAck: true);
                    if (delivery is not null)
                        return delivery.Body.ToArray();
                }
                catch (Exception)
                {
                    // The dead-letter queue is declared with the subscription. A get before that declare throws.
                }

                await Task.Delay(100);
            }

            throw new TimeoutException($"Timed out waiting for a message on {queue}.");
        }

        public async Task DeleteSubscriptionAsync(string exchange, string subscriptionName)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(_uri) };
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();
                await channel.QueueDeleteAsync(
                    RabbitMqNames.Queue(subscriptionName, RabbitMqQueueLayout.Subscription, EventName));
                await channel.QueueDeleteAsync(RabbitMqNames.RetryQueue(subscriptionName));
                await channel.QueueDeleteAsync(RabbitMqNames.DeadLetterQueue(subscriptionName));
                await channel.ExchangeDeleteAsync(RabbitMqNames.RetryExchange(subscriptionName));
                await channel.ExchangeDeleteAsync(RabbitMqNames.DelayExchange(subscriptionName));
                await channel.ExchangeDeleteAsync(RabbitMqNames.DeadLetterExchange(subscriptionName));
                await channel.ExchangeDeleteAsync(exchange);
            }
            catch (Exception)
            {
            }
        }
    }
}
