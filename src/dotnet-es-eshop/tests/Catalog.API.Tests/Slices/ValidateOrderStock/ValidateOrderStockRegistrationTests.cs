using System.Net;
using System.Text;
using System.Text.Json;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.AspNetCore;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockLine = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.ValidateOrderStock;

public class ValidateOrderStockRegistrationTests
{
    [Fact]
    public void Catalog_assembly_scan_registers_validate_order_stock()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(OrderStockValidation).Assembly;
        var shared = typeof(OrderStockConfirmed).Assembly;
        var confirmed = shared.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "order-stock-confirmed")
            .ToArray();
        var rejected = shared.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "order-stock-rejected")
            .ToArray();

        Assert.Single(confirmed);
        Assert.Equal(typeof(OrderStockConfirmed), confirmed[0]);
        Assert.Single(rejected);
        Assert.Equal(typeof(OrderStockRejected), rejected[0]);

        context.AddCatalogContext(assembly, shared);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("order-stock-confirmed", out var confirmedType));
        Assert.Equal(typeof(OrderStockConfirmed), confirmedType);
        Assert.True(context.EventCatalog.TryResolveType("order-stock-rejected", out var rejectedType));
        Assert.Equal(typeof(OrderStockRejected), rejectedType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<ValidateOrderStockCommand>));

        var ex = Assert.Throws<InvalidOperationException>(() => context.AddCatalogContext(assembly, shared));
        Assert.Contains("once", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assembly_scan_maps_post_validate_order_stock()
    {
        await using var host = await CatalogHost.StartAsync();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var items = host.Services.GetRequiredService<IAggregateRepository>();
        var item = await items.GetOrCreateAsync<CatalogItem>(itemId);
        await items.HandleCommandAsync(
            item,
            new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Alpine boot",
                120m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                4,
                "High-ankle boot",
                "boot.png",
                10,
                100));

        using var response = await host.Client.PostAsync(
            "/api/catalog/validate-order-stock",
            Json(new ValidateOrderStockCommand(Guid.NewGuid(), orderId, [new StockLine(itemId, 4)])));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var check = await items.GetOrCreateAsync<OrderStockValidation>(orderId);
        Assert.True(check.State.Checked);
        Assert.True(check.State.Confirmed);
        Assert.Equal(1, check.Version);

        using var again = await host.Client.PostAsync(
            "/api/catalog/validate-order-stock",
            Json(new ValidateOrderStockCommand(Guid.NewGuid(), orderId, [new StockLine(itemId, 4)])));
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        var reread = await items.GetOrCreateAsync<OrderStockValidation>(orderId);
        Assert.Equal(1, reread.Version);

        using var empty = await host.Client.PostAsync(
            "/api/catalog/validate-order-stock",
            Json(new ValidateOrderStockCommand(Guid.NewGuid(), Guid.NewGuid(), [])));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, empty.StatusCode);
        using var document = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.Equal(
            OrderStockValidation.ItemsRequiredTitle,
            document.RootElement.GetProperty("title").GetString());
    }

    private static StringContent Json<T>(T command)
    {
        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class CatalogHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private CatalogHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<CatalogHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            var assembly = typeof(OrderStockValidation).Assembly;
            context.AddCatalogContext(assembly, typeof(OrderStockConfirmed).Assembly);
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
            return new CatalogHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
