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
using LawnDart.Samples.EShop.Basket.API.Slices.RemoveItemFromBasket;
using LawnDart.Samples.EShop.Basket.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using BasketAggregate = global::LawnDart.Samples.EShop.Basket.API.Aggregates.Basket.Basket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Basket.API.Tests.Slices.RemoveItemFromBasket;

public class RemoveItemFromBasketRegistrationTests
{
    [Fact]
    public void Basket_assembly_scan_registers_remove_item_from_basket_once()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(BasketItemRemoved).Assembly;
        var removed = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "basket-item-removed")
            .ToArray();

        Assert.Single(removed);
        Assert.Equal(typeof(BasketItemRemoved), removed[0]);

        context.AddBasketContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("basket-item-removed", out var eventType));
        Assert.Equal(typeof(BasketItemRemoved), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<RemoveItemFromBasketCommand>));
    }

    [Fact]
    public async Task Assembly_scan_maps_post_remove_item_from_basket()
    {
        await using var host = await BasketHost.StartAsync();
        var buyerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        using var first = await PostAsync(
            host,
            "/api/basket/add-item-to-basket",
            new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                productId,
                "Alpine boot",
                120m,
                2,
                "boot.png"));
        using var second = await PostAsync(
            host,
            "/api/basket/add-item-to-basket",
            new AddItemToBasketCommand(
                Guid.NewGuid(),
                buyerId,
                otherProductId,
                "Wool cap",
                25m,
                4,
                "cap.png"));

        using var response = await PostAsync(
            host,
            "/api/basket/remove-item-from-basket",
            new RemoveItemFromBasketCommand(Guid.NewGuid(), buyerId, productId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var baskets = host.Services.GetRequiredService<IAggregateRepository>();
        var basket = await baskets.GetOrCreateAsync<BasketAggregate>(buyerId);
        var line = Assert.Single(basket.State.Items);
        Assert.Equal(otherProductId, line.ProductId);
        Assert.Equal("Wool cap", line.ProductName);
        Assert.Equal(25m, line.UnitPrice);
        Assert.Equal(4, line.Quantity);
        Assert.Equal("cap.png", line.PictureUrl);
    }

    private static async Task<HttpResponseMessage> PostAsync<TCommand>(
        BasketHost host,
        string path,
        TCommand command)
    {
        var json = JsonSerializer.Serialize(
            command,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return await host.Client.PostAsync(
            path,
            new StringContent(json, Encoding.UTF8, "application/json"));
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
