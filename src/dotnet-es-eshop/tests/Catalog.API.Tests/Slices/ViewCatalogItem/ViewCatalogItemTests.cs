using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.ViewCatalogItem;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.ViewCatalogItem;

public class ViewCatalogItemTests
{
    [Fact]
    public void Add_creates_the_view()
    {
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var projection = new ViewCatalogItemProjection();

        projection.ProcessEvent(Added(itemId, brandId, typeId, "Alpine boot", "High-ankle boot", 120m, "boot.png", 10), 1);

        var view = projection.GetView();
        Assert.Equal(itemId, view.ItemId);
        Assert.Equal("Alpine boot", view.Name);
        Assert.Equal("High-ankle boot", view.Description);
        Assert.Equal(120m, view.Price);
        Assert.Equal(brandId, view.BrandId);
        Assert.Equal(typeId, view.TypeId);
        Assert.Equal("boot.png", view.PictureUrl);
        Assert.Equal(10, view.AvailableStock);
        Assert.True(view.IsAvailable);
        Assert.False(view.Removed);
    }

    [Fact]
    public void Update_changes_the_view()
    {
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var nextBrand = Guid.NewGuid();
        var nextType = Guid.NewGuid();
        var projection = new ViewCatalogItemProjection();

        projection.ProcessEvent(Added(itemId, brandId, typeId, "Alpine boot", "High-ankle boot", 120m, "boot.png", 10), 1);
        projection.ProcessEvent(
            new CatalogItemUpdated(
                Guid.NewGuid(),
                DateTime.UtcNow,
                itemId,
                "Trail boot",
                "Lightweight boot",
                140m,
                nextBrand,
                nextType,
                "trail.png"),
            2);
        projection.ProcessEvent(
            new CatalogItemPriceChanged(Guid.NewGuid(), DateTime.UtcNow, itemId, 120m, 140m),
            3);

        var view = projection.GetView();
        Assert.Equal("Trail boot", view.Name);
        Assert.Equal("Lightweight boot", view.Description);
        Assert.Equal(140m, view.Price);
        Assert.Equal(nextBrand, view.BrandId);
        Assert.Equal(nextType, view.TypeId);
        Assert.Equal("trail.png", view.PictureUrl);
        Assert.Equal(10, view.AvailableStock);
        Assert.True(view.IsAvailable);
    }

    [Fact]
    public void Restock_and_deduct_change_stock()
    {
        var itemId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var projection = new ViewCatalogItemProjection();

        projection.ProcessEvent(Added(itemId, Guid.NewGuid(), Guid.NewGuid(), "Alpine boot", null, 120m, null, 10), 1);
        projection.ProcessEvent(
            new CatalogItemRestocked(Guid.NewGuid(), DateTime.UtcNow, itemId, 6, 16),
            2);
        projection.ProcessEvent(
            new CatalogStockDeducted(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Guid.NewGuid(),
                [
                    new CatalogStockDeduction(otherId, 7),
                    new CatalogStockDeduction(itemId, 5)
                ]),
            3);

        var view = projection.GetView();
        Assert.Equal(11, view.AvailableStock);
        Assert.True(view.IsAvailable);

        projection.ProcessEvent(
            new CatalogStockDeducted(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Guid.NewGuid(),
                [new CatalogStockDeduction(itemId, 40)]),
            4);

        view = projection.GetView();
        Assert.Equal(0, view.AvailableStock);
        Assert.False(view.IsAvailable);
        Assert.False(view.Removed);
    }

    [Fact]
    public void Deduct_sums_two_lines_for_the_same_item()
    {
        var itemId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var projection = new ViewCatalogItemProjection();

        projection.ProcessEvent(Added(itemId, Guid.NewGuid(), Guid.NewGuid(), "Alpine boot", null, 120m, null, 20), 1);
        projection.ProcessEvent(
            new CatalogStockDeducted(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Guid.NewGuid(),
                [
                    new CatalogStockDeduction(itemId, 3),
                    new CatalogStockDeduction(otherId, 8),
                    new CatalogStockDeduction(itemId, 4)
                ]),
            2);

        var view = projection.GetView();
        Assert.Equal(13, view.AvailableStock);
        Assert.True(view.IsAvailable);
    }

    [Fact]
    public async Task Remove_makes_the_route_return_404()
    {
        await using var host = await ItemHost.StartAsync();
        var itemId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            ViewCatalogItemProjection.InstanceId(itemId),
            [Added(itemId, Guid.NewGuid(), Guid.NewGuid(), "Alpine boot", "High-ankle boot", 120m, "boot.png", 4)]);
        var created = await host.PollAsync(itemId, view => view.Name == "Alpine boot");
        Assert.Equal(4, created.AvailableStock);

        await store.AppendAsync(
            ViewCatalogItemProjection.InstanceId(itemId),
            [new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, itemId)]);

        var status = await host.PollStatusAsync(itemId, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        await using var host = await ItemHost.StartAsync();

        var missing = await host.Client.GetAsync(ItemHost.Url(Guid.NewGuid()));
        var empty = await host.Client.GetAsync(ItemHost.Url(Guid.Empty));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_item()
    {
        await using var host = await ItemHost.StartAsync();
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            ViewCatalogItemProjection.InstanceId(itemId),
            [Added(itemId, brandId, typeId, "Alpine boot", "High-ankle boot", 120m, "boot.png", 10)]);

        var view = await host.PollAsync(itemId, item => item.Name == "Alpine boot");
        Assert.Equal(itemId, view.ItemId);
        Assert.Equal("High-ankle boot", view.Description);
        Assert.Equal(120m, view.Price);
        Assert.Equal("boot.png", view.PictureUrl);
        Assert.Equal(10, view.AvailableStock);
        Assert.True(view.IsAvailable);
        Assert.Null(view.BrandName);
        Assert.Null(view.TypeName);

        using var response = await host.Client.GetAsync(ItemHost.Url(itemId));
        AssertModelFields(await response.Content.ReadAsStringAsync());
    }

    private static void AssertModelFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(
            new[]
            {
                "availableStock",
                "brandName",
                "description",
                "isAvailable",
                "itemId",
                "name",
                "pictureUrl",
                "price",
                "typeName"
            },
            names);
    }

    private static CatalogItemAdded Added(
        Guid itemId,
        Guid brandId,
        Guid typeId,
        string name,
        string? description,
        decimal price,
        string? pictureFileName,
        int availableStock) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            itemId,
            name,
            description,
            price,
            pictureFileName,
            brandId,
            typeId,
            availableStock,
            2,
            40);

    private sealed class ItemHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ItemHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<ItemHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddCatalogContext(typeof(ViewCatalogItemProjection).Assembly);

            var app = builder.Build();
            app.MapViewCatalogItem();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new ItemHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async Task<ViewCatalogItemView> PollAsync(Guid itemId, Func<ViewCatalogItemView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(itemId));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<ViewCatalogItemView>(
                        lastBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Item view was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public async Task<HttpStatusCode> PollStatusAsync(Guid itemId, HttpStatusCode expected)
        {
            HttpStatusCode last = 0;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(itemId));
                last = response.StatusCode;
                if (last == expected)
                    return last;

                await Task.Delay(100);
            }

            return last;
        }

        public static string Url(Guid itemId) =>
            $"/views/view-catalog-item/{itemId:D}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
