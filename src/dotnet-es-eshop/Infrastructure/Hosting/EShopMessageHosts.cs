using System.Security.Cryptography;
using System.Text;
using LawnDart.Messaging;
using LawnDart.Messaging.Telemetry;
using LawnDart.Patterns.EventProcessing;
using LawnDart.Patterns.Reaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Subscribes to one event and runs <typeparamref name="TReactor"/> for each message.
/// </summary>
/// <typeparam name="TReactor">The reactor implementation.</typeparam>
/// <typeparam name="TEvent">The event the reactor handles.</typeparam>
/// <remarks>
/// The host is a singleton. Each message opens a scope and resolves <typeparamref name="TReactor"/> there.
/// The inbox key matches LawnDart <c>AddReactor</c>: <c>reactor</c>, a hash of the reactor full name, and the message id.
/// </remarks>
public sealed class EShopReactorHost<TReactor, TEvent> : BackgroundService
    where TReactor : class, IReactor<TEvent>
    where TEvent : class, IEvent
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IMessageTransport _transport;
    private readonly IInboxStore _inbox;
    private readonly ICommandDispatcher? _commands;
    private readonly ILogger<EShopReactorHost<TReactor, TEvent>> _logger;

    private static readonly string ReactorTypeName = typeof(TReactor).Name;
    private static readonly string EventTypeName = typeof(TEvent).Name;

    /// <summary>
    /// Creates the host.
    /// </summary>
    /// <param name="scopes">Scope factory. The reactor is resolved inside a new scope for each message.</param>
    /// <param name="transport">Transport that delivers <typeparamref name="TEvent"/>.</param>
    /// <param name="inbox">Inbox that drops a second delivery of the same message id.</param>
    /// <param name="logger">Logger for duplicates, skipped commands, and failures.</param>
    /// <param name="commands">Dispatcher for commands the reactor returns. Null when none is registered.</param>
    public EShopReactorHost(
        IServiceScopeFactory scopes,
        IMessageTransport transport,
        IInboxStore inbox,
        ILogger<EShopReactorHost<TReactor, TEvent>> logger,
        ICommandDispatcher? commands = null)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(logger);
        _scopes = scopes;
        _transport = transport;
        _inbox = inbox;
        _logger = logger;
        _commands = commands;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _transport.SubscribeAsync<TEvent>(HandleAsync, stoppingToken);

    private async Task HandleAsync(TEvent @event, MessageContext context, CancellationToken cancellationToken)
    {
        var messageId = context.MessageId;
        var inboxKey = messageId is null ? null : EShopConsumerInbox.ForReactor(typeof(TReactor), messageId);

        if (inboxKey is not null && await _inbox.IsProcessedAsync(inboxKey, cancellationToken).ConfigureAwait(false))
        {
            MessagingTelemetry.RecordInboxDuplicate(EventTypeName);
            _logger.LogDebug(
                "Reactor {Reactor} skipping duplicate message {MessageId} for event {EventType}",
                ReactorTypeName,
                messageId,
                EventTypeName);
            return;
        }

        using var activity = ReactorTelemetry.StartHandle(ReactorTypeName, EventTypeName, context);
        var start = TimeProvider.System.GetTimestamp();

        try
        {
            List<ICommand> commands;
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var reactor = scope.ServiceProvider.GetRequiredService<TReactor>();
                commands = (await reactor.ReactAsync(@event, context, cancellationToken).ConfigureAwait(false)).ToList();
            }

            if (_commands is not null)
            {
                foreach (var command in commands)
                {
                    await _commands.DispatchAsync(command, context.CreateChild(), cancellationToken).ConfigureAwait(false);
                }
            }
            else if (commands.Count > 0)
            {
                _logger.LogWarning(
                    "Reactor {Reactor} emitted {Count} command(s) but no ICommandDispatcher is registered. Commands will not be dispatched.",
                    ReactorTypeName,
                    commands.Count);
            }

            if (inboxKey is not null)
            {
                await _inbox.MarkProcessedAsync(inboxKey, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }

            var elapsed = TimeProvider.System.GetElapsedTime(start);
            ReactorTelemetry.RecordHandle(ReactorTypeName, EventTypeName, elapsed, commands.Count);
            _logger.LogDebug(
                "Reactor {Reactor} handled {EventType} in {ElapsedMs:F1}ms, emitted {CommandCount} command(s)",
                ReactorTypeName,
                EventTypeName,
                elapsed.TotalMilliseconds,
                commands.Count);
        }
        catch (Exception ex)
        {
            ReactorTelemetry.RecordError(ReactorTypeName, EventTypeName);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "Reactor {Reactor} failed while handling {EventType} (MessageId={MessageId})",
                ReactorTypeName,
                EventTypeName,
                messageId);
            throw;
        }
    }
}

/// <summary>
/// Subscribes to one event and runs <typeparamref name="TProcessor"/> for each message.
/// </summary>
/// <typeparam name="TProcessor">The event processor implementation.</typeparam>
/// <typeparam name="TEvent">The input event the processor handles.</typeparam>
/// <remarks>
/// The host is a singleton. Each message opens a scope and resolves <typeparamref name="TProcessor"/> there.
/// Derived events are published on the same transport.
/// The inbox key matches LawnDart <c>AddEventProcessor</c>.
/// </remarks>
public sealed class EShopEventProcessorHost<TProcessor, TEvent> : BackgroundService
    where TProcessor : class, IEventProcessor<TEvent>
    where TEvent : class, IEvent
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IMessageTransport _transport;
    private readonly IInboxStore _inbox;
    private readonly ILogger<EShopEventProcessorHost<TProcessor, TEvent>> _logger;

    private static readonly string ProcessorTypeName = typeof(TProcessor).Name;
    private static readonly string EventTypeName = typeof(TEvent).Name;

    /// <summary>
    /// Creates the host.
    /// </summary>
    /// <param name="scopes">Scope factory. The processor is resolved inside a new scope for each message.</param>
    /// <param name="transport">Transport that delivers <typeparamref name="TEvent"/> and carries derived events.</param>
    /// <param name="inbox">Inbox that drops a second delivery of the same message id.</param>
    /// <param name="logger">Logger for duplicates and failures.</param>
    public EShopEventProcessorHost(
        IServiceScopeFactory scopes,
        IMessageTransport transport,
        IInboxStore inbox,
        ILogger<EShopEventProcessorHost<TProcessor, TEvent>> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(logger);
        _scopes = scopes;
        _transport = transport;
        _inbox = inbox;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _transport.SubscribeAsync<TEvent>(HandleAsync, stoppingToken);

    private async Task HandleAsync(TEvent @event, MessageContext context, CancellationToken cancellationToken)
    {
        var messageId = context.MessageId;
        var inboxKey = messageId is null ? null : EShopConsumerInbox.ForProcessor(typeof(TProcessor), messageId);

        if (inboxKey is not null && await _inbox.IsProcessedAsync(inboxKey, cancellationToken).ConfigureAwait(false))
        {
            MessagingTelemetry.RecordInboxDuplicate(EventTypeName);
            _logger.LogDebug(
                "EventProcessor {Processor} skipping duplicate message {MessageId} for event {EventType}",
                ProcessorTypeName,
                messageId,
                EventTypeName);
            return;
        }

        using var activity = EventProcessorTelemetry.StartProcess(ProcessorTypeName, EventTypeName, context);
        var start = TimeProvider.System.GetTimestamp();

        try
        {
            List<IEvent> derivedEvents;
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<TProcessor>();
                derivedEvents = (await processor.ProcessAsync(@event, context, cancellationToken).ConfigureAwait(false)).ToList();
            }

            foreach (var derived in derivedEvents)
            {
                await _transport.PublishEventAsync(derived, context.CreateChild(), cancellationToken).ConfigureAwait(false);
            }

            if (inboxKey is not null)
            {
                await _inbox.MarkProcessedAsync(inboxKey, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }

            var elapsed = TimeProvider.System.GetElapsedTime(start);
            EventProcessorTelemetry.RecordProcess(ProcessorTypeName, EventTypeName, elapsed, derivedEvents.Count);
            _logger.LogDebug(
                "EventProcessor {Processor} handled {EventType} in {ElapsedMs:F1}ms, emitted {EventCount} derived event(s)",
                ProcessorTypeName,
                EventTypeName,
                elapsed.TotalMilliseconds,
                derivedEvents.Count);
        }
        catch (Exception ex)
        {
            EventProcessorTelemetry.RecordError(ProcessorTypeName, EventTypeName);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "EventProcessor {Processor} failed while handling {EventType} (MessageId={MessageId})",
                ProcessorTypeName,
                EventTypeName,
                messageId);
            throw;
        }
    }
}

/// <summary>
/// Inbox keys for reactors and event processors.
/// </summary>
/// <remarks>
/// The text matches LawnDart <c>InboxConsumerKey</c> so a row written by this host is the same row <c>AddReactor</c> would write.
/// </remarks>
public static class EShopConsumerInbox
{
    private const int MaxKeyLength = 256;
    private const int ConsumerHashLength = 32;

    /// <summary>
    /// Inbox key for one reactor and message id.
    /// </summary>
    /// <param name="reactorType">Reactor type. The hash uses <see cref="Type.FullName"/>.</param>
    /// <param name="messageId">Transport message id.</param>
    /// <returns><c>reactor:{hash}:{messageId}</c>, or a hashed message id when the key would pass 256 characters.</returns>
    public static string ForReactor(Type reactorType, string messageId) =>
        Compose("reactor", reactorType, messageId);

    /// <summary>
    /// Inbox key for one event processor and message id.
    /// </summary>
    /// <param name="processorType">Processor type. The hash uses <see cref="Type.FullName"/>.</param>
    /// <param name="messageId">Transport message id.</param>
    /// <returns><c>processor:{hash}:{messageId}</c>, or a hashed message id when the key would pass 256 characters.</returns>
    public static string ForProcessor(Type processorType, string messageId) =>
        Compose("processor", processorType, messageId);

    private static string Compose(string role, Type consumerType, string messageId)
    {
        ArgumentNullException.ThrowIfNull(consumerType);
        ArgumentNullException.ThrowIfNull(messageId);

        var name = consumerType.FullName ?? consumerType.Name;
        var consumer = HashSegment(name);
        var key = role + ":" + consumer + ":" + messageId;
        if (key.Length <= MaxKeyLength)
            return key;

        return role + ":" + consumer + ":" + HashSegment(messageId);
    }

    private static string HashSegment(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, ConsumerHashLength / 2)).ToLowerInvariant();
    }
}
