using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
using System.Text;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Samples.EShop.WebApp.Components.Pages.User;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;
using LawnDart.Samples.EShop.WebApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawnDart.Samples.EShop.WebApp.Tests;

public class OrderStatusNotificationTests
{
    private static readonly Guid AliceId = IdentityStub.AliceId;

    private static readonly Guid BobId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task Status_message_shows_the_new_order_without_a_refresh()
    {
        var orderId = Guid.NewGuid();
        await using var harness = OrderPageHarness.Start();
        harness.Orders.Then();
        harness.Orders.Then(Row(orderId, "Submitted", 199.99m));
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);

        var root = await RenderAsync(renderer);
        var before = await HtmlAsync(renderer, root);
        Assert.DoesNotContain(orderId.ToString("D"), before, StringComparison.OrdinalIgnoreCase);

        await harness.Notifications.NotifyOrderStatusChangedAsync(AliceId);
        var after = await HtmlAsync(renderer, root);

        Assert.Contains(orderId.ToString("D"), after, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Submitted", after, StringComparison.Ordinal);
        Assert.Equal(2, harness.Orders.Calls);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
    }

    [Fact]
    public async Task Status_message_on_another_thread_refreshes_the_orders_page()
    {
        var orderId = Guid.NewGuid();
        await using var harness = OrderPageHarness.Start();
        harness.Orders.Then(Row(Guid.NewGuid(), "Submitted", 12m));
        harness.Orders.Then(Row(orderId, "Submitted", 42m));
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);
        var root = await RenderAsync(renderer);

        await Task.Run(() => harness.Notifications.NotifyOrderStatusChangedAsync(AliceId));
        var html = await HtmlAsync(renderer, root);

        Assert.Contains(orderId.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
        Assert.Equal(2, harness.Orders.Calls);
    }

    [Fact]
    public async Task Bounded_retry_shows_a_placed_order_missing_from_the_first_read()
    {
        var orderId = Guid.NewGuid();
        await using var harness = OrderPageHarness.Start($"user/orders?placed={orderId:D}");
        harness.Orders.Then();
        harness.Orders.Then();
        harness.Orders.Then(Row(orderId, "Submitted", 88m));
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);

        var root = await RenderAsync(renderer);
        var html = await HtmlAsync(renderer, root);

        Assert.Contains(orderId.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, harness.Orders.Calls);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
    }

    [Fact]
    public async Task Another_buyers_message_does_not_refresh_alices_page()
    {
        var aliceOrder = Guid.NewGuid();
        var bobOrder = Guid.NewGuid();
        await using var harness = OrderPageHarness.Start();
        harness.Orders.Then(Row(aliceOrder, "Submitted", 10m));
        harness.Orders.Then(Row(bobOrder, "Submitted", 99m));
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);
        var root = await RenderAsync(renderer);
        var beforeCalls = harness.Orders.Calls;

        await harness.Notifications.NotifyOrderStatusChangedAsync(BobId);
        var html = await HtmlAsync(renderer, root);

        Assert.Equal(beforeCalls, harness.Orders.Calls);
        Assert.Contains(aliceOrder.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(bobOrder.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("shipped", "Shipped")]
    [InlineData("cancelled", "Cancelled")]
    [InlineData("stock", "Cancelled")]
    public async Task Listed_order_status_refreshes_alices_page(string kind, string status)
    {
        var orderId = Guid.NewGuid();
        await using var harness = await ConnectedOrders.StartAsync();
        harness.Orders.Then(Row(orderId, "Submitted", 15m));
        harness.Orders.Then(Row(orderId, status, 15m));
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);
        var root = await RenderAsync(renderer);
        var before = harness.Orders.Calls;

        await PublishListedAsync(harness.Transport, kind, orderId);
        var html = await HtmlAsync(renderer, root);

        Assert.Equal(before + 1, harness.Orders.Calls);
        Assert.Contains(orderId.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(status, html, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
    }

    [Fact]
    public async Task Listed_order_status_for_bob_does_not_refresh_alice()
    {
        var aliceOrder = Guid.NewGuid();
        var bobOrder = Guid.NewGuid();
        await using var harness = await ConnectedOrders.StartAsync();
        harness.Orders.Then(Row(aliceOrder, "Submitted", 15m));
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);
        var root = await RenderAsync(renderer);
        var before = harness.Orders.Calls;

        await PublishListedAsync(harness.Transport, "shipped", bobOrder);
        await PublishListedAsync(harness.Transport, "cancelled", bobOrder);
        await PublishListedAsync(harness.Transport, "stock", bobOrder);
        var html = await HtmlAsync(renderer, root);

        Assert.Equal(before, harness.Orders.Calls);
        Assert.Contains(aliceOrder.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(bobOrder.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
    }

    [Fact]
    public async Task Missing_placed_order_is_read_attempts_times()
    {
        var placed = Guid.NewGuid();
        var shown = Guid.NewGuid();
        await using var harness = OrderPageHarness.Start($"user/orders?placed={placed:D}");
        harness.Orders.Then(Row(shown, "Submitted", 4m));
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);

        var root = await RenderAsync(renderer);
        var html = await HtmlAsync(renderer, root);

        Assert.Equal(4, harness.Orders.Calls);
        Assert.Contains(shown.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(placed.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporarily unavailable", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Status_reload_after_placed_load_reads_once()
    {
        var placed = Guid.NewGuid();
        await using var harness = await ConnectedOrders.StartAsync($"user/orders?placed={placed:D}");
        harness.Orders.Then();
        await using var renderer = new PageRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);
        var root = await RenderAsync(renderer);
        Assert.Equal(4, harness.Orders.Calls);

        await PublishAsync(harness.Transport, new OrderStarted(Guid.NewGuid(), DateTime.UtcNow, placed, AliceId));
        var html = await HtmlAsync(renderer, root);

        Assert.Equal(5, harness.Orders.Calls);
        Assert.Contains("No orders yet", html, StringComparison.Ordinal);
        Assert.DoesNotContain("temporarily unavailable", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
    }

    [Fact]
    public async Task Published_status_message_notifies_only_that_buyer()
    {
        var logs = new LineLog();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.AddOrderStatusNotifications();
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscriberAsync(transport);
            var notifications = host.Services.GetRequiredService<OrderStatusNotificationService>();
            var alice = 0;
            var bob = 0;
            notifications.Subscribe(AliceId, () =>
            {
                alice++;
                return Task.CompletedTask;
            });
            notifications.Subscribe(BobId, () =>
            {
                bob++;
                return Task.CompletedTask;
            });

            var orderId = Guid.NewGuid();
            await PublishAsync(transport, new OrderStarted(Guid.NewGuid(), DateTime.UtcNow, orderId, AliceId));
            await PublishAsync(transport, new OrderAwaitingValidation(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [new OrderStockItem(Guid.NewGuid(), 1)],
                AliceId));
            await PublishAsync(transport, new OrderStatusStockConfirmed(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                AliceId,
                10m,
                "Alice"));
            await PublishAsync(transport, new OrderPaid(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [new OrderStockItem(Guid.NewGuid(), 1)],
                AliceId));
            await PublishAsync(transport, new OrderStarted(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), BobId));

            Assert.Equal(4, alice);
            Assert.Equal(1, bob);
            Assert.DoesNotContain(logs.Lines, FailedNotification);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Listener_exception_is_logged_and_the_next_listener_runs()
    {
        var logs = new LineLog();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Error));
        services.AddSingleton<OrderStatusNotificationService>();
        await using var provider = services.BuildServiceProvider();
        var notifications = provider.GetRequiredService<OrderStatusNotificationService>();
        var ran = 0;
        notifications.Subscribe(AliceId, () => throw new InvalidOperationException("orders broke"));
        notifications.Subscribe(AliceId, () =>
        {
            ran++;
            return Task.CompletedTask;
        });

        await notifications.NotifyOrderStatusChangedAsync(AliceId);

        Assert.Equal(1, ran);
        Assert.Contains(logs.Lines, line => line.Contains("orders broke", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Disposed_subscription_is_not_called()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<OrderStatusNotificationService>();
        await using var provider = services.BuildServiceProvider();
        var notifications = provider.GetRequiredService<OrderStatusNotificationService>();
        var calls = 0;
        var subscription = notifications.Subscribe(AliceId, () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        subscription.Dispose();
        subscription.Dispose();
        await notifications.NotifyOrderStatusChangedAsync(AliceId);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Notification_runs_on_the_renderer_that_subscribed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<OrderStatusNotificationService>();
        await using var provider = services.BuildServiceProvider();
        var notifications = provider.GetRequiredService<OrderStatusNotificationService>();
        var context = new MarkerContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext? seen = null;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            notifications.Subscribe(AliceId, () =>
            {
                seen = SynchronizationContext.Current;
                return Task.CompletedTask;
            });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await notifications.NotifyOrderStatusChangedAsync(AliceId);

        Assert.Same(context, seen);
        Assert.True(context.Posts > 0);
    }

    [Fact]
    public async Task Circuit_reload_sends_the_shopper_bearer_token()
    {
        var recorded = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<ShopperEndedAccess>();
        services.AddTransient<ShopperBearerHandler>();
        services.AddScoped<ShopperSession>();
        services.AddScoped<OrdersByShopperClient>();
        services.AddHttpClient(EShopStorefrontClients.Ordering, client => client.BaseAddress = new Uri("http://ordering.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => recorded)
            .AddHttpMessageHandler<ShopperBearerHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = null;
        var principal = ShopperSession.CreatePrincipal(
            AliceId,
            "Alice",
            IdentityStub.AccessToken,
            DateTimeOffset.UtcNow.AddHours(1));

        using (ShopperCallPrincipal.Push(principal))
        {
            var client = scope.ServiceProvider.GetRequiredService<OrdersByShopperClient>();
            var rows = await client.GetOrders();
            Assert.Empty(rows);
        }

        Assert.Equal("Bearer " + IdentityStub.AccessToken, recorded.Authorization);
    }

    private static OrderRecord Row(Guid id, string status, decimal total) =>
        new(id, new DateTime(2026, 10, 9, 1, 33, 0, DateTimeKind.Utc), status, total);

    private static async Task<HtmlRootComponent> RenderAsync(PageRenderer renderer)
    {
        HtmlRootComponent root = default;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            root = await renderer.RenderComponentAsync<Orders>(ParameterView.Empty);
        });
        return root;
    }

    private static Task<string> HtmlAsync(PageRenderer renderer, HtmlRootComponent root) =>
        renderer.Dispatcher.InvokeAsync(root.ToHtmlString);

    /// <summary>
    /// Static renderer that still builds the orders page.
    /// The page declares interactive server mode so the subscription stays on the circuit.
    /// </summary>
    private sealed class PageRenderer : StaticHtmlRenderer
    {
        public PageRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
            : base(services, loggerFactory)
        {
        }

        public async Task<HtmlRootComponent> RenderComponentAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(
            ParameterView parameters)
            where TComponent : IComponent
        {
            var content = BeginRenderingComponent(typeof(TComponent), parameters);
            await content.QuiescenceTask;
            return content;
        }

        protected override IComponent ResolveComponentForRenderMode(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType,
            int? parentComponentId,
            IComponentActivator componentActivator,
            IComponentRenderMode renderMode) =>
            componentActivator.CreateInstance(componentType);
    }

    private static async Task WaitForSubscriberAsync(InMemoryMessageTransport transport)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (transport.SubscriberCount<OrderStarted>() == 0
            || transport.SubscriberCount<OrderAwaitingValidation>() == 0
            || transport.SubscriberCount<OrderStatusStockConfirmed>() == 0
            || transport.SubscriberCount<OrderPaid>() == 0
            || transport.SubscriberCount<OrderShippedMessage>() == 0
            || transport.SubscriberCount<OrderCancelledMessage>() == 0
            || transport.SubscriberCount<OrderCancelledForStockMessage>() == 0)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Order status subscription did not start.");

            await Task.Delay(20);
        }
    }

    private static Task PublishListedAsync(IMessageTransport transport, string kind, Guid orderId)
    {
        var id = Guid.NewGuid();
        var time = DateTime.UtcNow;
        return kind switch
        {
            "shipped" => PublishAsync(transport, new OrderShippedMessage(id, time, orderId)),
            "cancelled" => PublishAsync(transport, new OrderCancelledMessage(id, time, orderId)),
            "stock" => PublishAsync(transport, new OrderCancelledForStockMessage(id, time, orderId)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static Task PublishAsync<TMessage>(IMessageTransport transport, TMessage message)
        where TMessage : class
    {
        var id = Guid.NewGuid().ToString("D");
        return transport.PublishAsync(message, new MessageContext
        {
            MessageId = id,
            CorrelationId = id,
            EnqueuedAt = DateTimeOffset.UtcNow
        });
    }

    private static bool FailedNotification(string line) =>
        line.Contains("Order status notification failed", StringComparison.Ordinal)
        || line.Contains("not associated with the Dispatcher", StringComparison.Ordinal);

    private sealed class OrderPageHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private OrderPageHarness(ServiceProvider provider, AsyncServiceScope scope, ScriptedOrders orders, LineLog logs)
        {
            _provider = provider;
            Scope = scope;
            Orders = orders;
            Logs = logs;
            Notifications = provider.GetRequiredService<OrderStatusNotificationService>();
        }

        public AsyncServiceScope Scope { get; }

        public ScriptedOrders Orders { get; }

        public LineLog Logs { get; }

        public OrderStatusNotificationService Notifications { get; }

        public static OrderPageHarness Start(string relativeUri = "user/orders")
        {
            var orders = new ScriptedOrders();
            var logs = new LineLog();
            var principal = ShopperSession.CreatePrincipal(
                AliceId,
                "Alice",
                IdentityStub.AccessToken,
                DateTimeOffset.UtcNow.AddHours(1));
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Information));
            services.AddHttpContextAccessor();
            services.AddSingleton<OrderStatusNotificationService>();
            services.AddSingleton(new OrderListRefresh
            {
                Attempts = 4,
                Delay = TimeSpan.FromMilliseconds(1)
            });
            services.AddScoped<ShopperSession>();
            services.AddScoped<IShopperOrders>(_ => orders);
            services.AddScoped<AuthenticationStateProvider>(_ => new FixedAuth(principal));
            services.AddScoped<NavigationManager>(_ => new TestNav(relativeUri));
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { User = principal };
            return new OrderPageHarness(provider, scope, orders, logs);
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private sealed class ConnectedOrders : IAsyncDisposable
    {
        private readonly IHost _host;

        private ConnectedOrders(IHost host, AsyncServiceScope scope, ScriptedOrders orders, LineLog logs)
        {
            _host = host;
            Scope = scope;
            Orders = orders;
            Logs = logs;
            Transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
        }

        public AsyncServiceScope Scope { get; }

        public ScriptedOrders Orders { get; }

        public LineLog Logs { get; }

        public InMemoryMessageTransport Transport { get; }

        public static async Task<ConnectedOrders> StartAsync(string relativeUri = "user/orders")
        {
            var orders = new ScriptedOrders();
            var logs = new LineLog();
            var principal = ShopperSession.CreatePrincipal(
                AliceId,
                "Alice",
                IdentityStub.AccessToken,
                DateTimeOffset.UtcNow.AddHours(1));
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(logs);
            builder.AddOrderStatusNotifications();
            builder.Services.AddSingleton(new OrderListRefresh
            {
                Attempts = 4,
                Delay = TimeSpan.FromMilliseconds(1)
            });
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton(orders);
            builder.Services.AddScoped<ShopperSession>();
            builder.Services.AddScoped<IShopperOrders>(sp => sp.GetRequiredService<ScriptedOrders>());
            builder.Services.AddScoped<AuthenticationStateProvider>(_ => new FixedAuth(principal));
            builder.Services.AddScoped<NavigationManager>(_ => new TestNav(relativeUri));
            var host = builder.Build();
            await host.StartAsync();
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            await WaitForSubscriberAsync(transport);
            var scope = host.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
                new DefaultHttpContext { User = principal };
            return new ConnectedOrders(host, scope, orders, logs);
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private sealed class ScriptedOrders : IShopperOrders
    {
        private readonly Queue<OrderRecord[]> _pages = new();
        private OrderRecord[] _last = [];

        public int Calls { get; private set; }

        public void Then(params OrderRecord[] rows) => _pages.Enqueue(rows);

        public Task<OrderRecord[]> GetOrders(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (_pages.Count > 0)
                _last = _pages.Dequeue();

            return Task.FromResult(_last);
        }
    }

    private sealed class FixedAuth(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class TestNav : NavigationManager
    {
        public TestNav(string relativeUri)
        {
            var path = relativeUri.StartsWith('/') ? relativeUri : "/" + relativeUri;
            Initialize("https://localhost/", "https://localhost" + path);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            var json = """{"buyerId":"11111111-2222-3333-4444-555555555555","orders":[]}""";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class MarkerContext : SynchronizationContext
    {
        public int Posts { get; private set; }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Posts++;
            var prior = Current;
            SetSynchronizationContext(this);
            try
            {
                callback(state);
            }
            finally
            {
                SetSynchronizationContext(prior);
            }
        }
    }

    private sealed class LineLog : ILoggerProvider, ILogger
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var line = formatter(state, exception);
            if (exception is not null)
                line = line + " " + exception.Message;

            lock (Lines)
                Lines.Add(line);
        }
    }
}
