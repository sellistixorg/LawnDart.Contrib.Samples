using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.CatalogItemDetailAdmin;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.CatalogItemDetailAdmin;

public class CatalogItemDetailAdminTests
{
    [Fact]
    public void Add_then_update_restock_and_deduct()
    {
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var nextBrand = Guid.NewGuid();
        var nextType = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var projection = new CatalogItemDetailAdminProjection();

        projection.ProcessEvent(
            Added(itemId, brandId, typeId, "Alpine boot", "High-ankle boot", 120m, "boot.png", 10, 2, 40),
            1);

        var view = projection.GetView();
        Assert.Equal(itemId, view.ItemId);
        Assert.Equal("Alpine boot", view.Name);
        Assert.Equal("High-ankle boot", view.Description);
        Assert.Equal(120m, view.Price);
        Assert.Equal(brandId, view.BrandId);
        Assert.Equal(typeId, view.TypeId);
        Assert.Equal(10, view.AvailableStock);
        Assert.Equal(2, view.RestockThreshold);
        Assert.Equal(40, view.MaxStockThreshold);
        Assert.False(view.OnReorder);
        Assert.Equal("boot.png", view.PictureFileName);
        Assert.False(view.Removed);

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

        view = projection.GetView();
        Assert.Equal("Trail boot", view.Name);
        Assert.Equal("Lightweight boot", view.Description);
        Assert.Equal(140m, view.Price);
        Assert.Equal(nextBrand, view.BrandId);
        Assert.Equal(nextType, view.TypeId);
        Assert.Equal("trail.png", view.PictureFileName);
        Assert.Equal(10, view.AvailableStock);
        Assert.Equal(2, view.RestockThreshold);
        Assert.Equal(40, view.MaxStockThreshold);
        Assert.False(view.OnReorder);

        projection.ProcessEvent(
            new CatalogItemRestocked(Guid.NewGuid(), DateTime.UtcNow, itemId, 6, 16),
            4);

        view = projection.GetView();
        Assert.Equal(16, view.AvailableStock);
        Assert.False(view.OnReorder);
        Assert.Equal(2, view.RestockThreshold);
        Assert.Equal(40, view.MaxStockThreshold);

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
            5);

        view = projection.GetView();
        Assert.Equal(9, view.AvailableStock);
        Assert.False(view.OnReorder);
        Assert.False(view.Removed);

        projection.ProcessEvent(
            new CatalogStockDeducted(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Guid.NewGuid(),
                [new CatalogStockDeduction(itemId, 40)]),
            6);

        view = projection.GetView();
        Assert.Equal(0, view.AvailableStock);
        Assert.False(view.OnReorder);
    }

    [Fact]
    public async Task Remove_returns_the_item_with_removed_state()
    {
        await using var host = await AdminHost.StartAsync();
        var itemId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            CatalogItemDetailAdminProjection.InstanceId(itemId),
            [Added(itemId, Guid.NewGuid(), Guid.NewGuid(), "Alpine boot", "High-ankle boot", 120m, "boot.png", 4, 2, 40)]);
        var created = await host.PollAsync(itemId, view => view.Name == "Alpine boot" && !view.Removed);
        Assert.Equal(4, created.AvailableStock);
        Assert.Equal(2, created.RestockThreshold);
        Assert.Equal(40, created.MaxStockThreshold);

        await store.AppendAsync(
            CatalogItemDetailAdminProjection.InstanceId(itemId),
            [new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, itemId)]);

        var removed = await host.PollAsync(itemId, view => view.Removed);
        Assert.Equal(HttpStatusCode.OK, await host.StatusAsync(itemId));
        Assert.Equal(itemId, removed.ItemId);
        Assert.Equal("Alpine boot", removed.Name);
        Assert.Equal("High-ankle boot", removed.Description);
        Assert.Equal(120m, removed.Price);
        Assert.Equal(4, removed.AvailableStock);
        Assert.Equal(2, removed.RestockThreshold);
        Assert.Equal(40, removed.MaxStockThreshold);
        Assert.Equal("boot.png", removed.PictureFileName);
        Assert.True(removed.Removed);
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        await using var host = await AdminHost.StartAsync();

        var missing = await host.Client.GetAsync(AdminHost.Url(Guid.NewGuid()));
        var empty = await host.Client.GetAsync(AdminHost.Url(Guid.Empty));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_admin_item()
    {
        await using var host = await AdminHost.StartAsync();
        var itemId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            CatalogItemDetailAdminProjection.InstanceId(itemId),
            [Added(itemId, brandId, typeId, "Alpine boot", "High-ankle boot", 120m, "boot.png", 10, 2, 40)]);

        var view = await host.PollAsync(itemId, item => item.Name == "Alpine boot");
        Assert.Equal(itemId, view.ItemId);
        Assert.Equal("High-ankle boot", view.Description);
        Assert.Equal(120m, view.Price);
        Assert.Equal(brandId, view.BrandId);
        Assert.Equal(typeId, view.TypeId);
        Assert.Equal(10, view.AvailableStock);
        Assert.Equal(2, view.RestockThreshold);
        Assert.Equal(40, view.MaxStockThreshold);
        Assert.False(view.OnReorder);
        Assert.Equal("boot.png", view.PictureFileName);
        Assert.False(view.Removed);

        using var response = await host.Client.GetAsync(AdminHost.Url(itemId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
                "brandId",
                "description",
                "itemId",
                "maxStockThreshold",
                "name",
                "onReorder",
                "pictureFileName",
                "price",
                "removed",
                "restockThreshold",
                "typeId"
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
        int availableStock,
        int? restockThreshold,
        int? maxStockThreshold) =>
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
            restockThreshold,
            maxStockThreshold);

    private sealed class AdminHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private AdminHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<AdminHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddCatalogContext(typeof(CatalogItemDetailAdminProjection).Assembly);

            var app = builder.Build();
            app.MapCatalogItemDetailAdmin();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new AdminHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async Task<CatalogItemDetailAdminView> PollAsync(
            Guid itemId,
            Func<CatalogItemDetailAdminView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(itemId));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<CatalogItemDetailAdminView>(
                        lastBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Admin item view was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public async Task<HttpStatusCode> StatusAsync(Guid itemId)
        {
            using var response = await Client.GetAsync(Url(itemId));
            return response.StatusCode;
        }

        public static string Url(Guid itemId) =>
            $"/views/catalog-item-detail-admin/{itemId:D}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
