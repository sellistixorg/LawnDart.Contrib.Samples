using System.Net;
using System.Text;
using System.Text.Json;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.AspNetCore;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderAggregate = LawnDart.Samples.EShop.Ordering.API.Aggregates.Order.Order;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.PlaceOrder;

public class PlaceOrderRegistrationTests
{
    [Fact]
    public void Ordering_assembly_scan_registers_place_order_once()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores("default");
        var context = services.AddBoundedContext("default");
        var assembly = typeof(OrderPlaced).Assembly;
        var shared = typeof(OrderStarted).Assembly;

        context.AddOrderingContext(assembly, shared);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("order-placed", out var placedType));
        Assert.Equal(typeof(OrderPlaced), placedType);
        Assert.True(context.EventCatalog.TryResolveType("order-started", out var startedType));
        Assert.Equal(typeof(OrderStarted), startedType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<PlaceOrderCommand>));

        var ex = Assert.Throws<InvalidOperationException>(() => context.AddOrderingContext(assembly, shared));
        Assert.Contains("once", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assembly_scan_maps_post_place_order()
    {
        await using var host = await OrderingHost.StartAsync();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = Command(orderId, buyerId, productId);
        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var response = await host.Client.PostAsync(
            "/api/ordering/place-order",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var orders = host.Services.GetRequiredService<IAggregateRepository>();
        var order = await orders.GetOrCreateAsync<OrderAggregate>(orderId);
        Assert.Equal(OrderAggregate.SubmittedStatus, order.State.OrderStatus);
        Assert.Equal(buyerId, order.State.BuyerId);
        var line = Assert.Single(order.State.OrderItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(120m, line.UnitPrice);
        Assert.Equal(2, line.Units);
        Assert.True(order.State.Started);

        using var duplicate = await host.Client.PostAsync(
            "/api/ordering/place-order",
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);

        var again = await orders.GetOrCreateAsync<OrderAggregate>(orderId);
        Assert.Equal(2, again.Version);
    }

    [Fact]
    public async Task Empty_basket_returns_order_items_empty()
    {
        await using var host = await OrderingHost.StartAsync();
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) with
        {
            OrderItems = []
        };
        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var response = await host.Client.PostAsync(
            "/api/ordering/place-order",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(OrderAggregate.ItemsEmptyTitle, document.RootElement.GetProperty("title").GetString());
    }

    private static PlaceOrderCommand Command(Guid orderId, Guid buyerId, Guid productId) =>
        new(
            orderId,
            orderId,
            buyerId,
            "Ada Lovelace",
            "1 Main",
            "Seattle",
            "WA",
            "USA",
            "98101",
            1,
            "Ada Lovelace",
            "4444",
            new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            [new OrderLine(productId, "Alpine boot", 120m, 2)]);

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
            var assembly = typeof(OrderPlaced).Assembly;
            context.AddOrderingContext(assembly, typeof(OrderStarted).Assembly);
            builder.Services.AddLawnDartHttpCommands(assembly);
            var bearer = new MatchingShopperBearerHandler().Protect(builder.Services);

            var app = builder.Build();
            app.MapLawnDartCommands();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new OrderingHost(app, bearer.CreateClient(address));
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
