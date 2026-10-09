using LawnDart.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Sample <see cref="IMessageTransport"/> over RabbitMQ.
/// One durable queue per subscription client. The SQL outbox publishes. The SQL inbox dedupes on consume.
/// </summary>
public sealed class RabbitMqMessageTransport : IMessageTransport, IAsyncDisposable, IDisposable
{
    private readonly IConnection _connection;
    private readonly RabbitMqTransportOptions _options;
    private readonly ILogger<RabbitMqMessageTransport> _logger;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private readonly SemaphoreSlim _consumeGate = new(1, 1);
    private readonly object _handlerGate = new();
    private readonly Dictionary<string, List<RegisteredHandler>> _handlers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _boundKeys = new(StringComparer.Ordinal);

    private IChannel? _publishChannel;
    private IChannel? _consumeChannel;
    private string? _consumerTag;
    private int _disposeState;

    public RabbitMqMessageTransport(
        IConnection connection,
        RabbitMqTransportOptions options,
        ILogger<RabbitMqMessageTransport> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string SubscriptionClient => _options.SubscriptionClient;

    public string QueueName => _options.QueueName;

    /// <summary>
    /// Declares the topic exchange and this service's durable queue.
    /// Publish and subscribe still declare the same objects on first use.
    /// </summary>
    public async Task InitializeTopologyAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposeState != 0, this);

        await _consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await GetConsumeChannelAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Declared exchange {ExchangeName} and queue {QueueName}",
                _options.ExchangeName,
                _options.QueueName);
        }
        finally
        {
            _consumeGate.Release();
        }
    }

    public async Task PublishAsync<TMessage>(
        TMessage message,
        MessageContext context,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ObjectDisposedException.ThrowIf(_disposeState != 0, this);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var routingKey = EShopRabbitMq.RoutingKey(typeof(TMessage));
        var body = RabbitMqMessageCodec.SerializeBody(message);
        var properties = RabbitMqMessageCodec.ToProperties(context, routingKey, _options.SubscriptionClient);

        var channel = await GetPublishChannelAsync(cancellationToken).ConfigureAwait(false);
        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Published {RoutingKey} for {SubscriptionClient} (MessageId={MessageId})",
            routingKey,
            _options.SubscriptionClient,
            context.MessageId);
    }

    public async Task SubscribeAsync<TMessage>(
        Func<TMessage, MessageContext, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ObjectDisposedException.ThrowIf(_disposeState != 0, this);
        ArgumentNullException.ThrowIfNull(handler);

        var routingKey = EShopRabbitMq.RoutingKey(typeof(TMessage));
        var registration = new RegisteredHandler(
            routingKey,
            async (body, properties, token) =>
            {
                var message = RabbitMqMessageCodec.DeserializeBody<TMessage>(body);
                var context = RabbitMqMessageCodec.ToContext(properties);
                await handler(message, context, token).ConfigureAwait(false);
            });

        await DeclareAndBindAsync(routingKey, cancellationToken).ConfigureAwait(false);
        AddHandler(registration);
        try
        {
            await StartConsumerAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            RemoveHandler(registration);
        }
    }

    /// <summary>
    /// Active local handlers for <typeparamref name="TMessage"/>. Tests wait on this before publishing.
    /// </summary>
    public int HandlerCount<TMessage>()
        where TMessage : class
        => HandlerCount(typeof(TMessage));

    public int HandlerCount(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        var routingKey = EShopRabbitMq.RoutingKey(messageType);
        lock (_handlerGate)
        {
            return _handlers.TryGetValue(routingKey, out var handlers) ? handlers.Count : 0;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        await CloseQuietlyAsync(_consumeChannel).ConfigureAwait(false);
        await CloseQuietlyAsync(_publishChannel).ConfigureAwait(false);
        _consumeGate.Dispose();
        _publishGate.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task<IChannel> GetPublishChannelAsync(CancellationToken cancellationToken)
    {
        if (_publishChannel is { IsOpen: true })
            return _publishChannel;

        await _publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_publishChannel is { IsOpen: true })
                return _publishChannel;

            _publishChannel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken).ConfigureAwait(false);
            await _publishChannel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return _publishChannel;
        }
        finally
        {
            _publishGate.Release();
        }
    }

    private async Task DeclareAndBindAsync(string routingKey, CancellationToken cancellationToken)
    {
        await _consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var channel = await GetConsumeChannelAsync(cancellationToken).ConfigureAwait(false);
            if (!_boundKeys.Add(routingKey))
                return;

            await channel.QueueBindAsync(
                queue: _options.QueueName,
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Bound {QueueName} to {ExchangeName} with routing key {RoutingKey}",
                _options.QueueName,
                _options.ExchangeName,
                routingKey);
        }
        finally
        {
            _consumeGate.Release();
        }
    }

    private async Task StartConsumerAsync(CancellationToken cancellationToken)
    {
        await _consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_consumerTag is not null)
                return;

            var channel = await GetConsumeChannelAsync(cancellationToken).ConfigureAwait(false);
            await channel.BasicQosAsync(0, 16, false, cancellationToken).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(channel, delivery);
            _consumerTag = await channel.BasicConsumeAsync(
                queue: _options.QueueName,
                autoAck: false,
                consumerTag: _options.SubscriptionClient,
                consumer: consumer,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _consumeGate.Release();
        }
    }

    private async Task<IChannel> GetConsumeChannelAsync(CancellationToken cancellationToken)
    {
        if (_consumeChannel is { IsOpen: true })
            return _consumeChannel;

        _consumeChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await _consumeChannel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await _consumeChannel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return _consumeChannel;
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery)
    {
        List<RegisteredHandler> snapshot;
        lock (_handlerGate)
        {
            if (!_handlers.TryGetValue(delivery.RoutingKey, out var handlers) || handlers.Count == 0)
            {
                snapshot = [];
            }
            else
            {
                snapshot = [.. handlers];
            }
        }

        if (snapshot.Count == 0)
        {
            _logger.LogDebug(
                "No local handler for routing key {RoutingKey} on {QueueName}. Acknowledging.",
                delivery.RoutingKey,
                _options.QueueName);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false).ConfigureAwait(false);
            return;
        }

        try
        {
            foreach (var handler in snapshot)
            {
                await handler.Invoke(delivery.Body, delivery.BasicProperties, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var requeue = !delivery.Redelivered;
            _logger.LogError(
                ex,
                "Handler failed for {RoutingKey} on {QueueName}. Requeue={Requeue}",
                delivery.RoutingKey,
                _options.QueueName,
                requeue);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: requeue)
                .ConfigureAwait(false);
        }
    }

    private void AddHandler(RegisteredHandler registration)
    {
        lock (_handlerGate)
        {
            if (!_handlers.TryGetValue(registration.RoutingKey, out var handlers))
            {
                handlers = [];
                _handlers[registration.RoutingKey] = handlers;
            }

            handlers.Add(registration);
        }
    }

    private void RemoveHandler(RegisteredHandler registration)
    {
        lock (_handlerGate)
        {
            if (!_handlers.TryGetValue(registration.RoutingKey, out var handlers))
                return;

            handlers.Remove(registration);
            if (handlers.Count == 0)
                _handlers.Remove(registration.RoutingKey);
        }
    }

    private static async Task CloseQuietlyAsync(IChannel? channel)
    {
        if (channel is null)
            return;

        try
        {
            if (channel.IsOpen)
                await channel.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Shutdown already closed the channel.
        }

        await channel.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class RegisteredHandler(
        string routingKey,
        Func<ReadOnlyMemory<byte>, IReadOnlyBasicProperties?, CancellationToken, Task> invoke)
    {
        public string RoutingKey { get; } = routingKey;

        public Func<ReadOnlyMemory<byte>, IReadOnlyBasicProperties?, CancellationToken, Task> Invoke { get; } = invoke;
    }
}
