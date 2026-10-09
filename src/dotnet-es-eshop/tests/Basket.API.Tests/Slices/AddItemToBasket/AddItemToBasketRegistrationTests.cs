using System.Net;
using System.Text;
using System.Text.Json;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.AspNetCore;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Basket.Commands;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.AddItemToBasket;

public class AddItemToBasketRegistrationTests
{
    [Fact]
    public void Basket_assembly_scan_registers_add_item_to_basket_once()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(BasketItemAdded).Assembly;

        context.AddBasketContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("basket-item-added", out var eventType));
        Assert.Equal(typeof(BasketItemAdded), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<AddItemToBasketCommand>));

        var ex = Assert.Throws<InvalidOperationException>(() => context.AddBasketContext(assembly));
        Assert.Contains("once", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assembly_scan_maps_post_add_item_to_basket()
    {
        await using var host = await BasketHost.StartAsync();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new AddItemToBasketCommand(
            Guid.NewGuid(),
            buyerId,
            productId,
            "Alpine boot",
            120m,
            2,
            "boot.png");
        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var response = await host.Client.PostAsync(
            "/api/basket/add-item-to-basket",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var baskets = host.Services.GetRequiredService<IAggregateRepository>();
        var basket = await baskets.GetOrCreateAsync<BasketAggregate>(buyerId);
        var line = Assert.Single(basket.State.Items);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal("Alpine boot", line.ProductName);
    }

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
            var assembly = typeof(BasketItemAdded).Assembly;
            context.AddBasketContext(assembly);
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
            return new BasketHost(app, bearer.CreateClient(address));
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
