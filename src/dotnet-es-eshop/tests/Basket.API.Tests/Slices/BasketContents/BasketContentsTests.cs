using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;
using LawnDart.Samples.EShop.Basket.API.Slices.ChangeBasketItemQuantity;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasket;
using LawnDart.Samples.EShop.Basket.API.Slices.RemoveItemFromBasket;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.BasketContents;

public class BasketContentsTests
{
    [Fact]
    public void Add_creates_the_line_and_totals()
    {
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var poleId = Guid.NewGuid();
        var projection = new BasketContentsProjection();

        projection.ProcessEvent(Added(buyerId, bootId, "Alpine boot", 120.50m, 2, "boot.png"), 1);
        projection.ProcessEvent(Added(buyerId, poleId, "Ski pole", 40m, 1, null), 2);

        var view = projection.GetView();
        Assert.Equal(buyerId, view.BuyerId);
        Assert.Equal(2, view.Items.Count);
        Assert.Equal(3, view.ItemCount);
        Assert.Equal(281m, view.Subtotal);
        Assert.False(view.IsEmpty);
        Assert.Equal(120.50m, view.Items[0].UnitPrice);
        Assert.Equal(241m, view.Items[0].UnitPrice * view.Items[0].Quantity);
        Assert.Equal("boot.png", view.Items[0].PictureUrl);
        Assert.Null(view.Items[1].PictureUrl);
    }

    [Fact]
    public void Second_add_keeps_the_first_price()
    {
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var projection = new BasketContentsProjection();

        projection.ProcessEvent(Added(buyerId, productId, "Alpine boot", 120m, 1, "boot.png"), 1);
        projection.ProcessEvent(Added(buyerId, productId, "Alpine boot", 120m, 3, "boot.png"), 2);

        var view = projection.GetView();
        var line = Assert.Single(view.Items);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(120m, line.UnitPrice);
        Assert.Equal(360m, view.Subtotal);
        Assert.Equal(3, view.ItemCount);
    }

    [Fact]
    public void Quantity_change_keeps_the_add_time_price()
    {
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var projection = new BasketContentsProjection();

        projection.ProcessEvent(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"), 1);
        projection.ProcessEvent(
            new BasketItemQuantityChanged(Guid.NewGuid(), DateTime.UtcNow, buyerId, productId, 2, 5),
            2);

        var view = projection.GetView();
        var line = Assert.Single(view.Items);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(120m, line.UnitPrice);
        Assert.Equal("Alpine boot", line.ProductName);
        Assert.Equal("boot.png", line.PictureUrl);
        Assert.Equal(5, view.ItemCount);
        Assert.Equal(600m, view.Subtotal);
        Assert.False(view.IsEmpty);
    }

    [Fact]
    public void Remove_drops_the_line()
    {
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var poleId = Guid.NewGuid();
        var projection = new BasketContentsProjection();

        projection.ProcessEvent(Added(buyerId, bootId, "Alpine boot", 120m, 2, "boot.png"), 1);
        projection.ProcessEvent(Added(buyerId, poleId, "Ski pole", 40m, 1, null), 2);
        projection.ProcessEvent(new BasketItemRemoved(Guid.NewGuid(), DateTime.UtcNow, buyerId, bootId), 3);

        var view = projection.GetView();
        var line = Assert.Single(view.Items);
        Assert.Equal(poleId, line.ProductId);
        Assert.Equal(1, view.ItemCount);
        Assert.Equal(40m, view.Subtotal);
        Assert.False(view.IsEmpty);

        projection.ProcessEvent(new BasketItemRemoved(Guid.NewGuid(), DateTime.UtcNow, buyerId, poleId), 4);

        view = projection.GetView();
        Assert.Empty(view.Items);
        Assert.Equal(0, view.ItemCount);
        Assert.Equal(0m, view.Subtotal);
        Assert.True(view.IsEmpty);
        Assert.Equal(buyerId, view.BuyerId);
    }

    [Fact]
    public void Clear_empties_the_basket()
    {
        var buyerId = Guid.NewGuid();
        var projection = new BasketContentsProjection();

        projection.ProcessEvent(Added(buyerId, Guid.NewGuid(), "Alpine boot", 120m, 2, "boot.png"), 1);
        projection.ProcessEvent(new BasketCleared(Guid.NewGuid(), DateTime.UtcNow, buyerId, "UserCleared"), 2);

        var view = projection.GetView();
        Assert.Empty(view.Items);
        Assert.Equal(0, view.ItemCount);
        Assert.Equal(0m, view.Subtotal);
        Assert.True(view.IsEmpty);
        Assert.Equal(buyerId, view.BuyerId);
    }

    [Fact]
    public void Add_after_clear_starts_a_fresh_line()
    {
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var projection = new BasketContentsProjection();

        projection.ProcessEvent(Added(buyerId, productId, "Alpine boot", 120m, 2, "boot.png"), 1);
        projection.ProcessEvent(new BasketCleared(Guid.NewGuid(), DateTime.UtcNow, buyerId, "UserCleared"), 2);
        projection.ProcessEvent(Added(buyerId, productId, "Trail boot", 80m, 1, "trail.png"), 3);

        var view = projection.GetView();
        var line = Assert.Single(view.Items);
        Assert.Equal("Trail boot", line.ProductName);
        Assert.Equal(80m, line.UnitPrice);
        Assert.Equal(1, line.Quantity);
        Assert.Equal("trail.png", line.PictureUrl);
        Assert.Equal(80m, view.Subtotal);
        Assert.Equal(1, view.ItemCount);
        Assert.False(view.IsEmpty);
    }

    [Fact]
    public async Task Get_returns_the_basket()
    {
        await using var host = await BasketHost.StartAsync();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            BasketContentsProjection.InstanceId(buyerId),
            [Added(buyerId, productId, "Alpine boot", 120.50m, 2, "boot.png")]);

        var view = await host.PollAsync(buyerId, item => item.ItemCount == 2);
        Assert.Equal(buyerId, view.BuyerId);
        Assert.Equal(241m, view.Subtotal);
        Assert.False(view.IsEmpty);
        var line = Assert.Single(view.Items);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(120.50m, line.UnitPrice);
        Assert.Equal(241m, line.UnitPrice * line.Quantity);

        using var response = await host.Client.GetAsync(BasketHost.Url(buyerId));
        AssertModelFields(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_buyer_returns_an_empty_basket()
    {
        await using var host = await BasketHost.StartAsync();
        var buyerId = Guid.NewGuid();

        using var response = await host.Client.GetAsync(BasketHost.Url(buyerId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = JsonSerializer.Deserialize<BasketContentsView>(
            await response.Content.ReadAsStringAsync(),
            ProjectionViewJson.Read);
        Assert.NotNull(view);
        Assert.Equal(buyerId, view.BuyerId);
        Assert.Empty(view.Items);
        Assert.Equal(0, view.ItemCount);
        Assert.Equal(0m, view.Subtotal);
        Assert.True(view.IsEmpty);
    }

    [Fact]
    public async Task Missing_or_empty_buyer_id_without_a_token_returns_401()
    {
        await using var host = await BasketHost.StartAsync();

        using var missing = await host.Client.GetAsync(BasketContentsEndpoint.Route);
        using var empty = await host.Client.GetAsync(BasketHost.Url(Guid.Empty));
        using var blank = await host.Client.GetAsync($"{BasketContentsEndpoint.Route}?buyerId=");

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, blank.StatusCode);
    }

    private static void AssertModelFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["buyerId", "items", "itemCount", "subtotal", "isEmpty"], names);

        var item = document.RootElement.GetProperty("items").EnumerateArray().Single();
        var itemNames = item.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["productId", "productName", "unitPrice", "quantity", "pictureUrl"], itemNames);
    }

    private static BasketItemAdded Added(
        Guid buyerId,
        Guid productId,
        string name,
        decimal unitPrice,
        int quantity,
        string? pictureUrl) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            buyerId,
            productId,
            name,
            unitPrice,
            quantity,
            pictureUrl);

    private sealed class BasketHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private BasketHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<BasketHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddBasketContext(typeof(BasketContentsProjection).Assembly);
            var bearer = new MatchingShopperBearerHandler().Protect(builder.Services);

            var app = builder.Build();
            app.MapBasketContents();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new BasketHost(app, bearer.CreateClient(address));
        }

        public async Task<BasketContentsView> PollAsync(Guid buyerId, Func<BasketContentsView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(buyerId));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<BasketContentsView>(lastBody, ProjectionViewJson.Read);
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Basket view was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public static string Url(Guid buyerId) =>
            $"{BasketContentsEndpoint.Route}?buyerId={buyerId:D}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
