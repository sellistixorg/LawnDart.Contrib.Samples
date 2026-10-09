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

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.MarkOrderAwaitingValidation;

public class MarkOrderAwaitingValidationRegistrationTests
{
    [Fact]
    public void Ordering_assembly_scan_registers_mark_order_awaiting_validation_once()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores("default");
        var context = services.AddBoundedContext("default");
        var assembly = typeof(OrderPlaced).Assembly;
        var shared = typeof(OrderAwaitingValidation).Assembly;

        context.AddOrderingContext(assembly, shared);

        Assert.NotNull(context.EventCatalog);
        Assert.True(
            context.EventCatalog.TryResolveType("order-awaiting-validation", out var signalType));
        Assert.Equal(typeof(OrderAwaitingValidation), signalType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<MarkOrderAwaitingValidationCommand>));

        var ex = Assert.Throws<InvalidOperationException>(() => context.AddOrderingContext(assembly, shared));
        Assert.Contains("once", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assembly_scan_maps_post_mark_order_awaiting_validation()
    {
        await using var host = await OrderingHost.StartAsync();
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await PlaceAsync(host, orderId, buyerId, productId);

        using var response = await host.Client.PostAsync(
            "/api/ordering/mark-order-awaiting-validation",
            Json(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId)));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var orders = host.Services.GetRequiredService<IAggregateRepository>();
        var order = await orders.GetOrCreateAsync<OrderAggregate>(orderId);
        Assert.Equal(OrderAggregate.AwaitingValidationStatus, order.State.OrderStatus);
        Assert.Equal(buyerId, order.State.BuyerId);
        var line = Assert.Single(order.State.OrderItems);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(3, line.Units);
        Assert.Equal(3, order.Version);

        using var again = await host.Client.PostAsync(
            "/api/ordering/mark-order-awaiting-validation",
            Json(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), orderId)));
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);

        var reread = await orders.GetOrCreateAsync<OrderAggregate>(orderId);
        Assert.Equal(3, reread.Version);
        Assert.Equal(OrderAggregate.AwaitingValidationStatus, reread.State.OrderStatus);
    }

    [Fact]
    public async Task Unknown_order_returns_order_not_found()
    {
        await using var host = await OrderingHost.StartAsync();

        using var response = await host.Client.PostAsync(
            "/api/ordering/mark-order-awaiting-validation",
            Json(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(OrderAggregate.OrderNotFoundTitle, document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Empty_order_id_returns_order_id_required()
    {
        await using var host = await OrderingHost.StartAsync();

        using var response = await host.Client.PostAsync(
            "/api/ordering/mark-order-awaiting-validation",
            Json(new MarkOrderAwaitingValidationCommand(Guid.NewGuid(), Guid.Empty)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(OrderAggregate.OrderIdRequiredTitle, document.RootElement.GetProperty("title").GetString());
    }

    private static async Task PlaceAsync(OrderingHost host, Guid orderId, Guid buyerId, Guid productId)
    {
        var command = new PlaceOrderCommand(
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
            [new OrderLine(productId, "Alpine boot", 120m, 3)]);

        using var response = await host.Client.PostAsync(
            "/api/ordering/place-order",
            Json(command));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static StringContent Json<T>(T command)
    {
        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new StringContent(json, Encoding.UTF8, "application/json");
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
            var assembly = typeof(OrderPlaced).Assembly;
            context.AddOrderingContext(assembly, typeof(OrderAwaitingValidation).Assembly);
            builder.Services.AddLawnDartHttpCommands(assembly);

            var app = builder.Build();
            app.MapLawnDartCommands();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new OrderingHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
