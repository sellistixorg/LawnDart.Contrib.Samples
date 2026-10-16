using LawnDart.Messaging;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Shared;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.OrderProcessor.Tests.Slices.ConfirmGracePeriod;

public class ConfirmGracePeriodTests
{
    [Fact]
    public async Task Order_is_confirmed_after_the_grace_period()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var pending = List();
        var transport = new RecordingTransport();
        var orderId = Guid.NewGuid();
        await pending.RememberAsync(orderId, time.GetUtcNow().UtcDateTime);
        var manager = Manager(pending, transport, time);

        time.Advance(TimeSpan.FromMinutes(1));
        await manager.ConfirmDueAsync();

        var published = Assert.Single(transport.Published);
        Assert.Equal(orderId, published.Event.OrderId);
        Assert.Equal(GracePeriodConfirmed.IdFor(orderId), published.Event.Id);
        Assert.Equal(published.Event.Id.ToString("D"), published.Context.MessageId);
        Assert.Equal("grace-period-confirmed", MessageTypeNames.Require(typeof(GracePeriodConfirmed)));
    }

    [Fact]
    public async Task Order_is_not_confirmed_before_the_grace_period()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var pending = List();
        var transport = new RecordingTransport();
        await pending.RememberAsync(Guid.NewGuid(), time.GetUtcNow().UtcDateTime);
        var manager = Manager(pending, transport, time);

        time.Advance(TimeSpan.FromSeconds(59));
        await manager.ConfirmDueAsync();

        Assert.Empty(transport.Published);
    }

    [Fact]
    public async Task Second_check_does_not_publish_again()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var views = new InMemoryViewStore();
        var pending = new PendingGraceOrders(views);
        var transport = new RecordingTransport();
        var orderId = Guid.NewGuid();
        await pending.RememberAsync(orderId, time.GetUtcNow().UtcDateTime);
        var manager = Manager(pending, transport, time);

        time.Advance(TimeSpan.FromMinutes(1));
        await manager.ConfirmDueAsync();
        await manager.ConfirmDueAsync();

        Assert.Single(transport.Published);
        Assert.Null(await views.GetViewAsync(PendingGraceOrders.ViewType, orderId.ToString("D")));
    }

    [Fact]
    public async Task Redelivered_order_started_keeps_the_first_time()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var pending = List();
        var transport = new RecordingTransport();
        var orderId = Guid.NewGuid();
        var reactor = new TrackSubmittedOrderReactor(pending, NullLogger<TrackSubmittedOrderReactor>.Instance);
        var first = new OrderStarted(Guid.NewGuid(), time.GetUtcNow().UtcDateTime, orderId, Guid.NewGuid());
        await reactor.ReactAsync(first, MessageContext.New());
        time.Advance(TimeSpan.FromHours(2));
        var later = new OrderStarted(Guid.NewGuid(), time.GetUtcNow().UtcDateTime, orderId, first.BuyerId);
        await reactor.ReactAsync(later, MessageContext.New());

        time.Set(first.Timestamp);
        time.Advance(TimeSpan.FromMinutes(1));
        await Manager(pending, transport, time).ConfirmDueAsync();

        Assert.Equal(orderId, Assert.Single(transport.Published).Event.OrderId);
    }

    [Fact]
    public async Task Failed_publish_is_retried_on_the_next_check()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var pending = List();
        var transport = new RecordingTransport { FailuresRemaining = 1 };
        var orderId = Guid.NewGuid();
        await pending.RememberAsync(orderId, time.GetUtcNow().UtcDateTime);
        var manager = Manager(pending, transport, time);
        time.Advance(TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ConfirmDueAsync());
        await manager.ConfirmDueAsync();

        Assert.Single(transport.Published);
        Assert.Equal(orderId, transport.Published[0].Event.OrderId);
    }

    [Fact]
    public async Task Restart_over_the_same_store_publishes_without_a_new_order_started()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var views = new InMemoryViewStore();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var reactor = new TrackSubmittedOrderReactor(
            new PendingGraceOrders(views),
            NullLogger<TrackSubmittedOrderReactor>.Instance);
        await reactor.ReactAsync(
            new OrderStarted(Guid.NewGuid(), time.GetUtcNow().UtcDateTime, orderId, buyerId),
            MessageContext.New());

        time.Advance(TimeSpan.FromMinutes(1));
        var transport = new RecordingTransport();
        await Manager(new PendingGraceOrders(views), transport, time).ConfirmDueAsync();

        var published = Assert.Single(transport.Published);
        Assert.Equal(orderId, published.Event.OrderId);
        Assert.Equal(GracePeriodConfirmed.IdFor(orderId), published.Event.Id);
        Assert.Null(await views.GetViewAsync(PendingGraceOrders.ViewType, orderId.ToString("D")));
    }

    [Fact]
    public void Confirmation_id_is_a_version_5_guid()
    {
        var orderId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var id = GracePeriodConfirmed.IdFor(orderId);

        Assert.Equal(id, GracePeriodConfirmed.IdFor(orderId));
        Assert.NotEqual(orderId, id);
        Assert.Equal(
            new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"),
            EShopVersion5Id.ForName("www.example.com"));
    }

    [Fact]
    public async Task Empty_order_id_is_logged_and_skipped()
    {
        var pending = List();
        var logger = new ListLogger();
        var reactor = new TrackSubmittedOrderReactor(pending, logger);
        var started = new OrderStarted(Guid.NewGuid(), DateTime.UtcNow, Guid.Empty, Guid.NewGuid());

        var commands = await reactor.ReactAsync(started, MessageContext.New());

        Assert.Empty(commands);
        var time = new ManualTime(DateTimeOffset.UtcNow);
        time.Advance(TimeSpan.FromHours(1));
        await Manager(pending, new RecordingTransport(), time).ConfirmDueAsync();
        var message = Assert.Single(logger.Warnings);
        Assert.Contains(started.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("empty order id", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_match_eshop()
    {
        var manager = Manager(List(), new RecordingTransport(), new ManualTime(DateTimeOffset.UtcNow));

        Assert.Equal(TimeSpan.FromMinutes(1), manager.GracePeriod);
        Assert.Equal(TimeSpan.FromSeconds(30), manager.CheckInterval);
        var coerced = Manager(
            List(),
            new RecordingTransport(),
            new ManualTime(DateTimeOffset.UtcNow),
            new BackgroundTaskOptions { GracePeriodTime = 0, CheckUpdateTime = 0 });
        Assert.Equal(TimeSpan.FromMinutes(1), coerced.GracePeriod);
        Assert.Equal(TimeSpan.FromSeconds(30), coerced.CheckInterval);
    }

    private static PendingGraceOrders List() => new(new InMemoryViewStore());

    private static GracePeriodManagerService Manager(
        PendingGraceOrders pending,
        IMessageTransport transport,
        TimeProvider time,
        BackgroundTaskOptions? options = null) =>
        new(
            pending,
            transport,
            Options.Create(options ?? new BackgroundTaskOptions()),
            time,
            NullLogger<GracePeriodManagerService>.Instance);

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _utc;

        public ManualTime(DateTimeOffset utc) => _utc = utc;

        public override DateTimeOffset GetUtcNow() => _utc;

        public void Advance(TimeSpan by) => _utc += by;

        public void Set(DateTime utc) => _utc = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    private sealed class RecordingTransport : IMessageTransport
    {
        public int FailuresRemaining { get; set; }

        public List<(GracePeriodConfirmed Event, MessageContext Context)> Published { get; } = [];

        public Task PublishAsync<TMessage>(
            TMessage message,
            MessageContext context,
            CancellationToken cancellationToken = default)
            where TMessage : class
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("publish failed");
            }

            Published.Add(((GracePeriodConfirmed)(object)message, context));
            return Task.CompletedTask;
        }

        public Task SubscribeAsync<TMessage>(
            Func<TMessage, MessageContext, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
            where TMessage : class
            => Task.CompletedTask;
    }

    private sealed class ListLogger : ILogger<TrackSubmittedOrderReactor>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
