using LawnDart.Messaging;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;

/// <summary>
/// Timer runs in the OrderProcessor worker (GracePeriodManagerService, GracePeriodTime minutes). When the grace period elapses it hands off to Ordering API, which issues Mark Order Awaiting Validation.
/// </summary>
public sealed class GracePeriodManagerService : BackgroundService
{
    private readonly PendingGraceOrders _pending;
    private readonly IMessageTransport _transport;
    private readonly TimeProvider _time;
    private readonly ILogger<GracePeriodManagerService> _logger;
    private readonly TimeSpan _gracePeriod;
    private readonly TimeSpan _checkInterval;

    /// <summary>
    /// Creates the timer.
    /// </summary>
    /// <param name="pending">Orders waiting out the grace period.</param>
    /// <param name="transport">Transport that publishes Grace Period Confirmed.</param>
    /// <param name="options">CheckUpdateTime and GracePeriodTime.</param>
    /// <param name="time">Clock used for the grace period and the delay between checks.</param>
    /// <param name="logger">Logger for publish failures.</param>
    /// <remarks>
    /// A non-positive GracePeriodTime or CheckUpdateTime uses the eShop default
    /// (1 minute, 30 seconds).
    /// </remarks>
    public GracePeriodManagerService(
        PendingGraceOrders pending,
        IMessageTransport transport,
        IOptions<BackgroundTaskOptions> options,
        TimeProvider time,
        ILogger<GracePeriodManagerService> logger)
    {
        _pending = pending;
        _transport = transport;
        _time = time;
        _logger = logger;
        var configured = options.Value;
        _gracePeriod = TimeSpan.FromMinutes(
            configured.GracePeriodTime > 0
                ? configured.GracePeriodTime
                : BackgroundTaskOptions.DefaultGracePeriodMinutes);
        _checkInterval = TimeSpan.FromSeconds(
            configured.CheckUpdateTime > 0
                ? configured.CheckUpdateTime
                : BackgroundTaskOptions.DefaultCheckUpdateSeconds);
    }

    /// <summary>
    /// Seconds between checks. CheckUpdateTime. Default is 30 seconds.
    /// </summary>
    public TimeSpan CheckInterval => _checkInterval;

    /// <summary>
    /// GracePeriodTime. Default is 1 minute.
    /// </summary>
    public TimeSpan GracePeriod => _gracePeriod;

    /// <summary>
    /// Publishes Grace Period Confirmed for each order whose age is at least <see cref="GracePeriod"/>.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the publish.</param>
    /// <returns>A task that completes when each due order has been published or the first publish fails.</returns>
    /// <remarks>
    /// Pass order id. The message id is <see cref="GracePeriodConfirmed.IdFor"/> so a second publish of the same order is the same message.
    /// The view row is deleted after a successful publish.
    /// A failed publish leaves the row in place. Orders not yet published in this check are released too.
    /// </remarks>
    public async Task ConfirmDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var due = await _pending.ClaimDueAsync(now, _gracePeriod, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < due.Count; index++)
        {
            var orderId = due[index];
            try
            {
                var id = GracePeriodConfirmed.IdFor(orderId);
                var confirmed = new GracePeriodConfirmed(id, now.UtcDateTime, orderId);
                var context = new MessageContext
                {
                    MessageId = id.ToString("D"),
                    CorrelationId = id.ToString("D"),
                    EnqueuedAt = now
                };
                await _transport.PublishAsync(confirmed, context, cancellationToken).ConfigureAwait(false);
                await _pending.CompleteAsync(orderId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                for (var remaining = index; remaining < due.Count; remaining++)
                    _pending.Release(due[remaining]);

                if (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "Grace period confirmation failed for order {OrderId}.",
                        orderId);
                }

                throw;
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Grace period manager is starting. CheckUpdateTime is {CheckUpdateSeconds} seconds. GracePeriodTime is {GracePeriodMinutes} minutes.",
            _checkInterval.TotalSeconds,
            _gracePeriod.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConfirmDueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Grace period check failed.");
            }

            try
            {
                await Task.Delay(_checkInterval, _time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
