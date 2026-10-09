using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Placeholders;
using LawnDart.Samples.EShop.WebApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawnDart.Samples.EShop.WebApp.Tests;

public class BasketNotificationTests : IClassFixture<StorefrontApplication>
{
    private readonly StorefrontApplication _app;

    public BasketNotificationTests(StorefrontApplication app) => _app = app;

    [Fact(Timeout = 30000)]
    public async Task Item_page_add_to_cart_refreshes_the_header_bag()
    {
        _app.Identity.Reset();
        _app.Basket.Reset();
        using var client = SignedInClient();
        await SignInAsync(client);

        var path = "/item/" + CatalogApiStub.ItemId.ToString("D");
        var page = await client.GetAsync(path);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Add to shopping bag", html, StringComparison.Ordinal);

        var posted = await PostFormAsync(client, path, html, "add-to-cart");
        var body = await posted.Content.ReadAsStringAsync();

        Assert.True(posted.IsSuccessStatusCode, Snip(body));
        Assert.DoesNotContain("not associated with the Dispatcher", body, StringComparison.Ordinal);
        Assert.Equal(1, _app.Basket.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));
        Assert.Equal("Shopping bag, 1 item", SettledBagLabel(body));
        Assert.Contains("in your", body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30000)]
    public async Task Cart_page_set_quantity_refreshes_the_header_bag()
    {
        _app.Identity.Reset();
        _app.Basket.Reset();
        _app.Basket.Seed(
            IdentityStub.AliceId,
            CatalogApiStub.ItemId,
            CatalogApiStub.ItemName,
            CatalogApiStub.ItemPrice,
            1,
            CatalogApiStub.PictureUrl);
        using var client = SignedInClient();
        await SignInAsync(client);

        var page = await client.GetAsync("/cart");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var fields = FieldsInForm(html, "UpdateQuantityId");
        fields["UpdateQuantityValue"] = "3";
        var posted = await PostFieldsAsync(client, "/cart", fields);
        var body = await posted.Content.ReadAsStringAsync();

        Assert.True(posted.IsSuccessStatusCode, Snip(body));
        Assert.DoesNotContain("not associated with the Dispatcher", body, StringComparison.Ordinal);
        Assert.Equal(3, _app.Basket.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));
        Assert.Equal("Shopping bag, 3 items", SettledBagLabel(body));
    }

    [Fact(Timeout = 30000)]
    public async Task Cart_page_remove_refreshes_the_header_bag()
    {
        _app.Identity.Reset();
        _app.Basket.Reset();
        _app.Basket.Seed(
            IdentityStub.AliceId,
            CatalogApiStub.ItemId,
            CatalogApiStub.ItemName,
            CatalogApiStub.ItemPrice,
            2,
            CatalogApiStub.PictureUrl);
        using var client = SignedInClient();
        await SignInAsync(client);

        var page = await client.GetAsync("/cart");
        var html = await page.Content.ReadAsStringAsync();
        var fields = FieldsInForm(html, "UpdateQuantityId");
        fields["UpdateQuantityValue"] = "0";
        var posted = await PostFieldsAsync(client, "/cart", fields);
        var body = await posted.Content.ReadAsStringAsync();

        Assert.True(posted.IsSuccessStatusCode, Snip(body));
        Assert.DoesNotContain("not associated with the Dispatcher", body, StringComparison.Ordinal);
        Assert.Equal(0, _app.Basket.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));
        Assert.Equal("Shopping bag", SettledBagLabel(body));
        Assert.Contains("Your shopping bag is empty", body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30000)]
    public async Task Subscribed_component_survives_a_write_off_its_dispatcher()
    {
        await using var harness = BasketHarness.Start();
        var slot = new BadgeSlot();
        await using var renderer = new HtmlRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);
        HtmlRootComponent root = default;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            root = await renderer.RenderComponentAsync<ReceiverBadge>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(ReceiverBadge.Slot)] = slot }));
        });

        var before = slot.Badge!.Renders;
        var productId = CatalogApiStub.ItemId;
        await harness.Basket.AddAsync(productId, CatalogApiStub.ItemName, CatalogApiStub.ItemPrice, CatalogApiStub.PictureUrl);
        await harness.Basket.SetQuantityAsync(productId, 4);
        await harness.Basket.SetQuantityAsync(productId, 0);

        Assert.Equal(0, slot.Badge.Quantity);
        Assert.True(slot.Badge.Renders > before);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
        var html = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        Assert.Equal("0", html);
    }

    [Fact(Timeout = 30000)]
    public async Task Add_started_on_the_renderer_notifies_after_it_leaves()
    {
        await using var harness = BasketHarness.Start();
        var slot = new BadgeSlot();
        await using var renderer = new HtmlRenderer(harness.Scope.ServiceProvider, NullLoggerFactory.Instance);

        var work = renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<SelfMarshalingBadge>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SelfMarshalingBadge.Slot)] = slot }));
            var before = slot.Badge!.Renders;
            await harness.Basket.AddAsync(
                CatalogApiStub.ItemId,
                CatalogApiStub.ItemName,
                CatalogApiStub.ItemPrice,
                CatalogApiStub.PictureUrl);
            return before;
        });
        var finished = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(work, finished);
        var before = await work;

        Assert.Equal(1, slot.Badge!.Quantity);
        Assert.True(slot.Badge.Renders > before);
        Assert.DoesNotContain(harness.Logs.Lines, FailedNotification);
    }

    [Fact]
    public async Task Disposed_subscription_is_not_called()
    {
        await using var harness = BasketHarness.Start();
        var calls = 0;
        var subscription = harness.Basket.NotifyOnChange(() =>
        {
            calls++;
            return Task.CompletedTask;
        });

        subscription.Dispose();
        subscription.Dispose();
        await harness.Basket.AddAsync(
            CatalogApiStub.ItemId,
            CatalogApiStub.ItemName,
            CatalogApiStub.ItemPrice,
            CatalogApiStub.PictureUrl);

        Assert.Equal(0, calls);
        Assert.Equal(1, harness.Stub.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));
    }

    [Fact]
    public async Task One_shopper_does_not_refresh_another_shoppers_components()
    {
        var services = BasketHarness.Services(out var stub, out _);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = ShopperSession.CreatePrincipal(
                IdentityStub.AliceId,
                "Alice",
                IdentityStub.AccessToken,
                DateTimeOffset.UtcNow.AddHours(1))
        };

        await using var scopeA = provider.CreateAsyncScope();
        await using var scopeB = provider.CreateAsyncScope();
        var clientA = scopeA.ServiceProvider.GetRequiredService<IBasketContents>();
        var clientB = scopeB.ServiceProvider.GetRequiredService<IBasketContents>();
        var countA = 0;
        var countB = 0;
        clientA.NotifyOnChange(() =>
        {
            countA++;
            return Task.CompletedTask;
        });
        clientB.NotifyOnChange(() =>
        {
            countB++;
            return Task.CompletedTask;
        });

        await clientA.AddAsync(CatalogApiStub.ItemId, CatalogApiStub.ItemName, CatalogApiStub.ItemPrice, CatalogApiStub.PictureUrl);

        Assert.NotSame(clientA, clientB);
        Assert.Equal(1, countA);
        Assert.Equal(0, countB);
        Assert.Equal(1, stub.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));
    }

    [Fact]
    public async Task Listener_exception_is_logged_and_the_write_finishes()
    {
        await using var harness = BasketHarness.Start();
        harness.Basket.NotifyOnChange(() => throw new InvalidOperationException("badge broke"));

        await harness.Basket.AddAsync(
            CatalogApiStub.ItemId,
            CatalogApiStub.ItemName,
            CatalogApiStub.ItemPrice,
            CatalogApiStub.PictureUrl);

        Assert.Equal(1, harness.Stub.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));
        Assert.Contains(harness.Logs.Lines, line => line.Contains("badge broke", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Notification_runs_on_the_renderer_that_subscribed()
    {
        await using var harness = BasketHarness.Start();
        var context = new MarkerContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext? seen = null;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            harness.Basket.NotifyOnChange(() =>
            {
                seen = SynchronizationContext.Current;
                return Task.CompletedTask;
            });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await harness.Basket.AddAsync(
            CatalogApiStub.ItemId,
            CatalogApiStub.ItemName,
            CatalogApiStub.ItemPrice,
            CatalogApiStub.PictureUrl);

        Assert.Same(context, seen);
        Assert.True(context.Posts > 0);
    }

    [Fact]
    public void Basket_client_is_scoped_to_one_shopper()
    {
        var services = new ServiceCollection();
        services.AddEShopWebAppPlaceholders();
        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IBasketContents));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(BasketContentsClient), descriptor.ImplementationType);

        using var scopeA = _app.Services.CreateScope();
        using var scopeB = _app.Services.CreateScope();
        var first = scopeA.ServiceProvider.GetRequiredService<IBasketContents>();
        var again = scopeA.ServiceProvider.GetRequiredService<IBasketContents>();
        var other = scopeB.ServiceProvider.GetRequiredService<IBasketContents>();
        Assert.Same(first, again);
        Assert.NotSame(first, other);
    }

    private HttpClient SignedInClient()
    {
        var client = _app.CreateNoRedirectClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        return client;
    }

    private static async Task SignInAsync(HttpClient client)
    {
        var loginUrl = "/user/login?returnUrl=" + Uri.EscapeDataString("/");
        var page = await client.GetAsync(loginUrl);
        var html = await page.Content.ReadAsStringAsync();
        var fields = HiddenFields(html);
        fields["UserName"] = IdentityStub.UserName;
        fields["Password"] = IdentityStub.Password;
        using var content = new FormUrlEncodedContent(fields);
        var response = await client.PostAsync(loginUrl, content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        string html,
        string handler)
    {
        var fields = FieldsInForm(html, handler);
        return await PostFieldsAsync(client, path, fields);
    }

    private static Task<HttpResponseMessage> PostFieldsAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> fields)
    {
        var content = new FormUrlEncodedContent(fields);
        return client.PostAsync(path, content);
    }

    private static Dictionary<string, string> FieldsInForm(string html, string marker)
    {
        var form = FormContaining(html, marker);
        Assert.False(string.IsNullOrEmpty(form), Snip(html));
        var fields = HiddenFields(form);
        Assert.True(fields.Count > 0, Snip(form));
        return fields;
    }

    private static string? FormContaining(string html, string marker)
    {
        foreach (Match match in Regex.Matches(html, "<form\\b.*?</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            if (match.Value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return match.Value;
        }

        return null;
    }

    private static Dictionary<string, string> HiddenFields(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var tag = match.Value;
            if (!tag.Contains("hidden", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = Attribute(tag, "name");
            if (string.IsNullOrEmpty(name))
                continue;

            fields[name] = Attribute(tag, "value") ?? "";
        }

        return fields;
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, name + "\\s*=\\s*(\"[^\"]*\"|'[^']*')", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var value = match.Groups[1].Value;
        return value.Substring(1, value.Length - 2);
    }

    private static string? SettledBagLabel(string html)
    {
        var matches = Regex.Matches(html, "aria-label=\"(Shopping bag[^\"]*)\"", RegexOptions.IgnoreCase);
        return matches.Count == 0 ? null : matches[^1].Groups[1].Value;
    }

    private static bool FailedNotification(string line) =>
        line.Contains("Basket change notification failed", StringComparison.Ordinal)
        || line.Contains("not associated with the Dispatcher", StringComparison.Ordinal);

    private static string Snip(string html) => html.Length > 2500 ? html[..2500] : html;

    private sealed class BadgeSlot
    {
        public IBadge? Badge { get; set; }
    }

    private interface IBadge
    {
        int Quantity { get; }

        int Renders { get; }
    }

    private sealed class ReceiverBadge : ComponentBase, IDisposable, IBadge
    {
        private IDisposable? _subscription;

        [Inject]
        public IBasketContents Basket { get; set; } = default!;

        [Parameter]
        public BadgeSlot Slot { get; set; } = default!;

        public int Quantity { get; private set; }

        public int Renders { get; private set; }

        protected override void OnInitialized()
        {
            Slot.Badge = this;
            _subscription = Basket.NotifyOnChange(EventCallback.Factory.Create(this, ReloadAsync));
        }

        protected override async Task OnInitializedAsync() => await ReloadAsync();

        public void Dispose() => _subscription?.Dispose();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Renders++;
            builder.AddContent(0, Quantity.ToString(CultureInfo.InvariantCulture));
        }

        private async Task ReloadAsync()
        {
            var items = await Basket.GetBasketItemsAsync();
            Quantity = 0;
            foreach (var item in items)
                Quantity += item.Quantity;
        }
    }

    private sealed class SelfMarshalingBadge : ComponentBase, IDisposable, IBadge
    {
        private IDisposable? _subscription;

        [Inject]
        public IBasketContents Basket { get; set; } = default!;

        [Parameter]
        public BadgeSlot Slot { get; set; } = default!;

        public int Quantity { get; private set; }

        public int Renders { get; private set; }

        protected override void OnInitialized()
        {
            Slot.Badge = this;
            _subscription = Basket.NotifyOnChange(() => InvokeAsync(async () =>
            {
                await ReloadAsync();
                StateHasChanged();
            }));
        }

        protected override async Task OnInitializedAsync() => await ReloadAsync();

        public void Dispose() => _subscription?.Dispose();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Renders++;
            builder.AddContent(0, Quantity.ToString(CultureInfo.InvariantCulture));
        }

        private async Task ReloadAsync()
        {
            var items = await Basket.GetBasketItemsAsync();
            Quantity = 0;
            foreach (var item in items)
                Quantity += item.Quantity;
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

    private sealed class BasketHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private BasketHarness(ServiceProvider provider, AsyncServiceScope scope, BasketApiStub stub, LineLog logs)
        {
            _provider = provider;
            Scope = scope;
            Stub = stub;
            Logs = logs;
            Basket = scope.ServiceProvider.GetRequiredService<IBasketContents>();
        }

        public AsyncServiceScope Scope { get; }

        public BasketApiStub Stub { get; }

        public LineLog Logs { get; }

        public IBasketContents Basket { get; }

        public static BasketHarness Start()
        {
            var services = Services(out var stub, out var logs);
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext
            {
                User = ShopperSession.CreatePrincipal(
                IdentityStub.AliceId,
                "Alice",
                IdentityStub.AccessToken,
                DateTimeOffset.UtcNow.AddHours(1))
            };
            return new BasketHarness(provider, scope, stub, logs);
        }

        public static ServiceCollection Services(out BasketApiStub stub, out LineLog logs)
        {
            stub = new BasketApiStub();
            logs = new LineLog();
            var basket = stub;
            var log = logs;
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(log).SetMinimumLevel(LogLevel.Information));
            services.AddHttpContextAccessor();
            services.AddHttpClient(EShopStorefrontClients.Basket, client => client.BaseAddress = new Uri("http://basket.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => basket);
            services.AddEShopWebAppPlaceholders();
            return services;
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await _provider.DisposeAsync();
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
