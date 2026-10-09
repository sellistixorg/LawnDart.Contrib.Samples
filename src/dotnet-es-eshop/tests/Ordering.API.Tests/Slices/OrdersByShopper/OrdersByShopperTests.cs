using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.OrdersByShopper;

public class OrdersByShopperTests
{
    [Fact]
    public void Place_lists_the_order_with_the_line_total()
    {
        var buyerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var placedAt = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var projection = new OrdersByShopperProjection();

        projection.ProcessEvent(Placed(orderId, buyerId, placedAt, 10m, 2, 3.5m, 4), 1);

        var view = OrdersByShopperProjection.ForBuyer(projection.GetView(), buyerId);
        var row = Assert.Single(view.Orders);
        Assert.Equal(buyerId, view.BuyerId);
        Assert.Equal(orderId, row.Id);
        Assert.Equal(placedAt, row.Date);
        Assert.Equal(Order.SubmittedStatus, row.Status);
        Assert.Equal(34m, row.Total);
    }

    [Fact]
    public void Status_events_update_the_matching_row_and_keep_the_total()
    {
        var buyerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var placedAt = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var projection = new OrdersByShopperProjection();

        projection.ProcessEvent(Placed(orderId, buyerId, placedAt, 10m, 2, 3.5m, 4), 1);
        projection.ProcessEvent(new OrderShipped(
            Guid.NewGuid(),
            placedAt.AddHours(1),
            otherId,
            Order.ShippedStatus,
            Order.ShippedDescription), 2);
        projection.ProcessEvent(new OrderAwaitingValidation(
            Guid.NewGuid(),
            placedAt.AddMinutes(1),
            orderId,
            [new OrderStockItem(Guid.NewGuid(), 2)],
            buyerId), 3);

        var row = OnlyRow(projection, buyerId);
        Assert.Equal(Order.AwaitingValidationStatus, row.Status);
        Assert.Equal(34m, row.Total);

        projection.ProcessEvent(new OrderStatusStockConfirmed(
            Guid.NewGuid(),
            placedAt.AddMinutes(2),
            orderId,
            buyerId,
            999m,
            "Ada"), 4);
        row = OnlyRow(projection, buyerId);
        Assert.Equal(Order.StockConfirmedStatus, row.Status);
        Assert.Equal(34m, row.Total);

        projection.ProcessEvent(new OrderPaid(
            Guid.NewGuid(),
            placedAt.AddMinutes(3),
            orderId,
            [new OrderStockItem(Guid.NewGuid(), 2)],
            Guid.NewGuid()), 5);
        row = OnlyRow(projection, buyerId);
        Assert.Equal(Order.StockConfirmedStatus, row.Status);

        projection.ProcessEvent(new OrderPaid(
            Guid.NewGuid(),
            placedAt.AddMinutes(4),
            orderId,
            [new OrderStockItem(Guid.NewGuid(), 2)],
            buyerId), 6);
        row = OnlyRow(projection, buyerId);
        Assert.Equal(Order.PaidStatus, row.Status);

        projection.ProcessEvent(new OrderShipped(
            Guid.NewGuid(),
            placedAt.AddMinutes(5),
            orderId,
            Order.ShippedStatus,
            Order.ShippedDescription), 7);
        row = OnlyRow(projection, buyerId);
        Assert.Equal(Order.ShippedStatus, row.Status);
        Assert.Equal(34m, row.Total);
        Assert.Equal(placedAt, row.Date);
    }

    [Fact]
    public void Cancel_events_set_cancelled()
    {
        var buyerId = Guid.NewGuid();
        var shopperCancelId = Guid.NewGuid();
        var stockCancelId = Guid.NewGuid();
        var placedAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var projection = new OrdersByShopperProjection();

        projection.ProcessEvent(Placed(shopperCancelId, buyerId, placedAt, 8m, 1, 0m, 0), 1);
        projection.ProcessEvent(Placed(stockCancelId, buyerId, placedAt.AddMinutes(1), 8m, 1, 0m, 0), 2);
        projection.ProcessEvent(new OrderCancelled(
            Guid.NewGuid(),
            placedAt.AddMinutes(2),
            shopperCancelId,
            Order.CancelledStatus,
            "Changed my mind"), 3);
        projection.ProcessEvent(new OrderCancelledForStock(
            Guid.NewGuid(),
            placedAt.AddMinutes(3),
            stockCancelId,
            [Guid.NewGuid()],
            Order.CancelledStatus), 4);

        var view = OrdersByShopperProjection.ForBuyer(projection.GetView(), buyerId);
        Assert.Equal(2, view.Orders.Count);
        Assert.All(view.Orders, row => Assert.Equal(Order.CancelledStatus, row.Status));
        Assert.Contains(view.Orders, row => row.Id == shopperCancelId);
        Assert.Contains(view.Orders, row => row.Id == stockCancelId);
    }

    [Fact]
    public void Rows_are_newest_first_and_hide_other_shoppers()
    {
        var buyerId = Guid.NewGuid();
        var otherBuyerId = Guid.NewGuid();
        var sameInstant = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var earlierId = Guid.NewGuid();
        var laterSameInstantId = Guid.NewGuid();
        var newestId = Guid.NewGuid();
        var projection = new OrdersByShopperProjection();

        projection.ProcessEvent(Placed(earlierId, buyerId, sameInstant, 1m, 1, 0m, 0), 1);
        projection.ProcessEvent(Placed(laterSameInstantId, buyerId, sameInstant, 1m, 1, 0m, 0), 2);
        projection.ProcessEvent(Placed(newestId, buyerId, sameInstant.AddDays(1), 1m, 1, 0m, 0), 3);
        projection.ProcessEvent(Placed(Guid.NewGuid(), otherBuyerId, sameInstant.AddDays(2), 9m, 3, 0m, 0), 4);

        var view = OrdersByShopperProjection.ForBuyer(projection.GetView(), buyerId);
        Assert.Equal([newestId, laterSameInstantId, earlierId], view.Orders.Select(row => row.Id).ToArray());

        var other = OrdersByShopperProjection.ForBuyer(projection.GetView(), otherBuyerId);
        var otherRow = Assert.Single(other.Orders);
        Assert.Equal(27m, otherRow.Total);

        var unknown = OrdersByShopperProjection.ForBuyer(projection.GetView(), Guid.NewGuid());
        Assert.Empty(unknown.Orders);
        Assert.NotEqual(Guid.Empty, unknown.BuyerId);
    }

    [Fact]
    public async Task Get_returns_that_shoppers_orders_newest_first()
    {
        await using var host = await OrderingHost.StartAsync();
        var buyerId = Guid.NewGuid();
        var otherBuyerId = Guid.NewGuid();
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            $"Order:{olderId:D}",
            [Placed(olderId, buyerId, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 4m, 2, 0m, 0)]);
        await store.AppendAsync(
            $"Order:{newerId:D}",
            [Placed(newerId, buyerId, new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc), 5m, 1, 0m, 0)]);
        await store.AppendAsync(
            $"Order:{Guid.NewGuid():D}",
            [Placed(Guid.NewGuid(), otherBuyerId, new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), 9m, 1, 0m, 0)]);

        var view = await host.PollAsync(buyerId, item => item.Orders.Count == 2);
        Assert.Equal(buyerId, view.BuyerId);
        Assert.Equal(newerId, view.Orders[0].Id);
        Assert.Equal(5m, view.Orders[0].Total);
        Assert.Equal(Order.SubmittedStatus, view.Orders[0].Status);
        Assert.Equal(olderId, view.Orders[1].Id);
        Assert.Equal(8m, view.Orders[1].Total);

        using var response = await host.Client.GetAsync(OrderingHost.Url(buyerId));
        AssertModelFields(await response.Content.ReadAsStringAsync());

        var other = await host.PollAsync(otherBuyerId, item => item.Orders.Count == 1);
        Assert.Equal(otherBuyerId, other.BuyerId);
        Assert.DoesNotContain(other.Orders, row => row.Id == newerId || row.Id == olderId);
    }

    [Fact]
    public async Task Shipped_updates_the_listed_status()
    {
        await using var host = await OrderingHost.StartAsync();
        var buyerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var placedAt = new DateTime(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc);
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync($"Order:{orderId:D}", [Placed(orderId, buyerId, placedAt, 12m, 1, 0m, 0)]);
        await host.PollAsync(buyerId, item => item.Orders.Count == 1);

        await store.AppendAsync(
            $"Order:{orderId:D}",
            [new OrderShipped(
                Guid.NewGuid(),
                placedAt.AddHours(2),
                orderId,
                Order.ShippedStatus,
                Order.ShippedDescription)],
            expectedVersion: 1);

        var view = await host.PollAsync(
            buyerId,
            item => item.Orders.Count == 1 && item.Orders[0].Status == Order.ShippedStatus);
        var row = Assert.Single(view.Orders);
        Assert.Equal(orderId, row.Id);
        Assert.Equal(12m, row.Total);
        Assert.Equal(placedAt, row.Date);
    }

    [Fact]
    public async Task Unknown_buyer_returns_an_empty_list()
    {
        await using var host = await OrderingHost.StartAsync();
        var buyerId = Guid.NewGuid();

        using var response = await host.Client.GetAsync(OrderingHost.Url(buyerId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = JsonSerializer.Deserialize<OrdersByShopperView>(
            await response.Content.ReadAsStringAsync(),
            ProjectionViewJson.Read);
        Assert.NotNull(view);
        Assert.Equal(buyerId, view.BuyerId);
        Assert.Empty(view.Orders);
    }

    [Fact]
    public async Task Missing_or_empty_buyer_id_without_a_token_returns_401()
    {
        await using var host = await OrderingHost.StartAsync();

        using var missing = await host.Client.GetAsync(OrdersByShopperEndpoint.Route);
        using var empty = await host.Client.GetAsync(OrderingHost.Url(Guid.Empty));
        using var blank = await host.Client.GetAsync($"{OrdersByShopperEndpoint.Route}?buyerId=");

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, blank.StatusCode);
    }

    private static OrdersByShopperItem OnlyRow(OrdersByShopperProjection projection, Guid buyerId)
    {
        var view = OrdersByShopperProjection.ForBuyer(projection.GetView(), buyerId);
        return Assert.Single(view.Orders);
    }

    private static void AssertModelFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["buyerId", "orders"], names);

        var item = document.RootElement.GetProperty("orders").EnumerateArray().First();
        var itemNames = item.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["id", "date", "status", "total"], itemNames);
    }

    private static OrderPlaced Placed(
        Guid orderId,
        Guid buyerId,
        DateTime timestamp,
        decimal firstPrice,
        int firstUnits,
        decimal secondPrice,
        int secondUnits)
    {
        var lines = new List<OrderLine>
        {
            new(Guid.NewGuid(), "Alpine boot", firstPrice, firstUnits)
        };
        if (secondUnits > 0)
            lines.Add(new OrderLine(Guid.NewGuid(), "Ski pole", secondPrice, secondUnits));

        return new OrderPlaced(
            Guid.NewGuid(),
            timestamp,
            orderId,
            buyerId,
            "Ada Lovelace",
            Order.SubmittedStatus,
            lines,
            "1 Main",
            "Seattle",
            "WA",
            "USA",
            "98101",
            1,
            "Ada Lovelace",
            "4444",
            new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid());
    }

    private sealed class OrderingHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private OrderingHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<OrderingHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            var assembly = typeof(OrdersByShopperProjection).Assembly;
            context.AddOrderingContext(assembly, typeof(OrderStarted).Assembly);
            var bearer = new MatchingShopperBearerHandler().Protect(builder.Services);

            var app = builder.Build();
            app.MapOrdersByShopper();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new OrderingHost(app, bearer.CreateClient(address));
        }

        public async Task<OrdersByShopperView> PollAsync(Guid buyerId, Func<OrdersByShopperView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(buyerId));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<OrdersByShopperView>(lastBody, ProjectionViewJson.Read);
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Orders view was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public static string Url(Guid buyerId) =>
            $"{OrdersByShopperEndpoint.Route}?buyerId={buyerId:D}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
