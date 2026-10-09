using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.CancelOrderForInsufficientStock;
using LawnDart.Samples.EShop.Ordering.API.Slices.OrderSummary;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.API.Slices.ShipOrder;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.OrderSummary;

public class OrderSummaryTests
{
    [Fact]
    public void Status_events_update_the_same_order()
    {
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var placedAt = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var projection = new OrderSummaryProjection();

        projection.ProcessEvent(Placed(orderId, buyerId, placedAt, bootId, "Alpine boot", 40m, 2), 1);
        var placed = projection.GetView();
        Assert.Equal(orderId, placed.OrderId);
        Assert.Equal(buyerId, placed.BuyerId);
        Assert.Equal(Order.SubmittedStatus, placed.OrderStatus);
        Assert.Equal(placedAt, placed.OrderDate);
        Assert.Equal(80m, placed.Total);
        Assert.Null(placed.Description);
        Assert.NotNull(placed.ShipTo);
        Assert.Equal("1 Main", placed.ShipTo.Street);
        Assert.Equal("Redmond", placed.ShipTo.City);
        Assert.Equal("WA", placed.ShipTo.State);
        Assert.Equal("USA", placed.ShipTo.Country);
        Assert.Equal("98052", placed.ShipTo.ZipCode);
        var line = Assert.Single(placed.OrderItems);
        Assert.Equal(bootId, line.ProductId);
        Assert.Equal("Alpine boot", line.ProductName);
        Assert.Equal(40m, line.UnitPrice);
        Assert.Equal(2, line.Units);

        projection.ProcessEvent(
            new OrderAwaitingValidation(
                Guid.NewGuid(),
                placedAt.AddMinutes(1),
                orderId,
                [new OrderStockItem(bootId, 2)],
                buyerId),
            2);
        Assert.Equal(Order.AwaitingValidationStatus, projection.GetView().OrderStatus);
        Assert.Null(projection.GetView().Description);
        Assert.Equal(80m, projection.GetView().Total);

        projection.ProcessEvent(
            new OrderStatusStockConfirmed(
                Guid.NewGuid(),
                placedAt.AddMinutes(2),
                orderId,
                buyerId,
                80m,
                "alice"),
            3);
        Assert.Equal(Order.StockConfirmedStatus, projection.GetView().OrderStatus);
        Assert.Equal(Order.StockConfirmedDescription, projection.GetView().Description);

        projection.ProcessEvent(
            new OrderPaid(
                Guid.NewGuid(),
                placedAt.AddMinutes(3),
                orderId,
                [new OrderStockItem(bootId, 2)],
                buyerId),
            4);
        Assert.Equal(Order.PaidStatus, projection.GetView().OrderStatus);
        Assert.Equal(Order.PaidDescription, projection.GetView().Description);
        Assert.Equal(80m, projection.GetView().Total);
        Assert.Equal(placedAt, projection.GetView().OrderDate);

        projection.ProcessEvent(
            new OrderShipped(
                Guid.NewGuid(),
                placedAt.AddMinutes(4),
                orderId,
                Order.ShippedStatus,
                Order.ShippedDescription),
            5);
        var shipped = projection.GetView();
        Assert.Equal(Order.ShippedStatus, shipped.OrderStatus);
        Assert.Equal(Order.ShippedDescription, shipped.Description);
        Assert.Equal(buyerId, shipped.BuyerId);
        Assert.Equal("Alpine boot", Assert.Single(shipped.OrderItems).ProductName);
    }

    [Fact]
    public void Cancel_sets_cancelled_status_text()
    {
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var projection = new OrderSummaryProjection();
        projection.ProcessEvent(Placed(orderId, buyerId, DateTime.UtcNow, Guid.NewGuid(), "Cap", 10m, 1), 1);
        projection.ProcessEvent(
            new OrderCancelled(Guid.NewGuid(), DateTime.UtcNow, orderId, Order.CancelledStatus, "Changed my mind"),
            2);

        var view = projection.GetView();
        Assert.Equal(Order.CancelledStatus, view.OrderStatus);
        Assert.Equal(Order.CancelledDescription, view.Description);
        Assert.Equal(10m, view.Total);
    }

    [Fact]
    public void Stock_rejection_names_the_missing_products()
    {
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var projection = new OrderSummaryProjection();
        projection.ProcessEvent(Placed(orderId, buyerId, DateTime.UtcNow, bootId, "Alpine boot", 40m, 1), 1);
        projection.ProcessEvent(
            new OrderAwaitingValidation(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [new OrderStockItem(bootId, 1)],
                buyerId),
            2);
        projection.ProcessEvent(
            new OrderCancelledForStock(
                Guid.NewGuid(),
                DateTime.UtcNow,
                orderId,
                [bootId, missingId],
                Order.CancelledStatus),
            3);

        var view = projection.GetView();
        Assert.Equal(Order.CancelledStatus, view.OrderStatus);
        Assert.Equal(
            $"The product items don't have stock: (Alpine boot, {missingId:D}).",
            view.Description);
        Assert.Equal(40m, view.Total);
    }

    [Fact]
    public async Task Get_returns_the_buyer_order_as_status_changes()
    {
        await using var host = await SummaryHost.StartAsync();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var bootId = Guid.NewGuid();
        var placedAt = new DateTime(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            OrderSummaryProjection.InstanceId(orderId),
            [
                Placed(orderId, buyerId, placedAt, bootId, "Alpine boot", 40m, 2),
                new OrderStarted(Guid.NewGuid(), placedAt, orderId, buyerId)
            ]);

        var placed = await host.PollAsync(
            orderId,
            buyerId,
            view => view.OrderStatus == Order.SubmittedStatus);
        Assert.Equal(orderId, placed.OrderId);
        Assert.Equal(buyerId, placed.BuyerId);
        Assert.Equal(placedAt, placed.OrderDate);
        Assert.Equal(80m, placed.Total);
        Assert.Null(placed.Description);
        Assert.Equal("Redmond", placed.ShipTo!.City);

        await store.AppendAsync(
            OrderSummaryProjection.InstanceId(orderId),
            [
                new OrderAwaitingValidation(
                    Guid.NewGuid(),
                    placedAt.AddMinutes(1),
                    orderId,
                    [new OrderStockItem(bootId, 2)],
                    buyerId),
                new OrderStatusStockConfirmed(
                    Guid.NewGuid(),
                    placedAt.AddMinutes(2),
                    orderId,
                    buyerId,
                    80m,
                    "alice"),
                new OrderPaid(
                    Guid.NewGuid(),
                    placedAt.AddMinutes(3),
                    orderId,
                    [new OrderStockItem(bootId, 2)],
                    buyerId)
            ]);

        var paid = await host.PollAsync(orderId, buyerId, view => view.OrderStatus == Order.PaidStatus);
        Assert.Equal(Order.PaidDescription, paid.Description);
        Assert.Equal(80m, paid.Total);
        Assert.Equal(placedAt, paid.OrderDate);

        using var response = await host.Client.GetAsync(SummaryHost.Url(orderId, buyerId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertModelFields(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Other_buyer_and_unknown_order_return_404()
    {
        await using var host = await SummaryHost.StartAsync();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            OrderSummaryProjection.InstanceId(orderId),
            [Placed(orderId, buyerId, DateTime.UtcNow, Guid.NewGuid(), "Cap", 10m, 1)]);
        await host.PollAsync(orderId, buyerId, view => view.OrderStatus == Order.SubmittedStatus);

        var otherBuyer = await host.Client.GetAsync(SummaryHost.Url(orderId, Guid.NewGuid()));
        var missing = await host.Client.GetAsync(SummaryHost.Url(Guid.NewGuid(), buyerId));
        var emptyOrder = await host.Client.GetAsync(SummaryHost.Url(Guid.Empty, buyerId));
        var missingBuyer = await host.Client.GetAsync($"/views/order-summary/{orderId:D}");

        Assert.Equal(HttpStatusCode.NotFound, otherBuyer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, emptyOrder.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missingBuyer.StatusCode);
    }

    private static void AssertModelFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(
            new[]
            {
                "buyerId",
                "description",
                "orderDate",
                "orderId",
                "orderItems",
                "orderStatus",
                "shipTo",
                "total"
            },
            names);

        var line = document.RootElement.GetProperty("orderItems").EnumerateArray().Single();
        var lineNames = line.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(new[] { "productId", "productName", "unitPrice", "units" }, lineNames);

        var shipTo = document.RootElement.GetProperty("shipTo");
        var shipNames = shipTo.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(new[] { "city", "country", "state", "street", "zipCode" }, shipNames);
    }

    private static OrderPlaced Placed(
        Guid orderId,
        Guid buyerId,
        DateTime placedAt,
        Guid productId,
        string productName,
        decimal unitPrice,
        int units) =>
        new(
            Guid.NewGuid(),
            placedAt,
            orderId,
            buyerId,
            "alice",
            Order.SubmittedStatus,
            [new OrderLine(productId, productName, unitPrice, units)],
            "1 Main",
            "Redmond",
            "WA",
            "USA",
            "98052",
            1,
            "TESTUSER",
            "4444",
            placedAt.AddYears(1),
            Guid.NewGuid());

    private sealed class SummaryHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private SummaryHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<SummaryHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddOrderingContext(
                typeof(OrderSummaryProjection).Assembly,
                typeof(OrderPaid).Assembly);
            var bearer = new MatchingShopperBearerHandler().Protect(builder.Services);

            var app = builder.Build();
            app.MapEShopSlices(typeof(OrderSummaryProjection).Assembly);
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new SummaryHost(app, bearer.CreateClient(address));
        }

        public async Task<OrderSummaryView> PollAsync(
            Guid orderNumber,
            Guid buyerId,
            Func<OrderSummaryView, bool> matches)
        {
            string? lastBody = null;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                using var response = await Client.GetAsync(Url(orderNumber, buyerId));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<OrderSummaryView>(
                        lastBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Order summary was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public static string Url(Guid orderNumber, Guid buyerId) =>
            $"/views/order-summary/{orderNumber:D}?buyerId={buyerId:D}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
