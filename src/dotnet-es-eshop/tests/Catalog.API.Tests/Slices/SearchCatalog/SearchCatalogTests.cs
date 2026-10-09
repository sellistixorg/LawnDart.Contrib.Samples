using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.RemoveCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.RestockCatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.SearchCatalog;
using LawnDart.Samples.EShop.Catalog.API.Slices.UpdateCatalogItem;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.SearchCatalog;

public class SearchCatalogTests
{
    [Fact]
    public void Prefix_match_is_case_sensitive()
    {
        var alpine = Guid.NewGuid();
        var trail = Guid.NewGuid();
        var projection = new SearchCatalogProjection();

        projection.ProcessEvent(Added(alpine, "Alpine boot", "High-ankle boot", 120m, "boot.png", 4), 1);
        projection.ProcessEvent(Added(trail, "Trail glove", "Light glove", 30m, "glove.png", 2), 2);

        var match = SearchCatalogProjection.Match(projection.GetView(), "Alpine", 0, 10);
        var item = Assert.Single(match.Items);
        Assert.Equal("Alpine", match.Query);
        Assert.Equal(1, match.TotalItems);
        Assert.Equal(alpine, item.ItemId);
        Assert.Equal("Alpine boot", item.Name);
        Assert.Equal("High-ankle boot", item.Description);
        Assert.Equal(120m, item.Price);
        Assert.Equal("boot.png", item.PictureUrl);
        Assert.Equal(4, item.AvailableStock);
        Assert.True(item.IsAvailable);

        var lower = SearchCatalogProjection.Match(projection.GetView(), "alpine", 0, 10);
        Assert.Equal(0, lower.TotalItems);
        Assert.Empty(lower.Items);

        var middle = SearchCatalogProjection.Match(projection.GetView(), "boot", 0, 10);
        Assert.Equal(0, middle.TotalItems);
    }

    [Fact]
    public void Updated_name_replaces_the_old_name()
    {
        var itemId = Guid.NewGuid();
        var projection = new SearchCatalogProjection();

        projection.ProcessEvent(Added(itemId, "Alpine boot", "High-ankle boot", 120m, "boot.png", 4), 1);
        projection.ProcessEvent(
            new CatalogItemUpdated(
                Guid.NewGuid(),
                DateTime.UtcNow,
                itemId,
                "Trail boot",
                "Lightweight boot",
                140m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "trail.png"),
            2);
        projection.ProcessEvent(
            new CatalogItemPriceChanged(Guid.NewGuid(), DateTime.UtcNow, itemId, 140m, 150m),
            3);

        var oldName = SearchCatalogProjection.Match(projection.GetView(), "Alpine", 0, 10);
        Assert.Equal(0, oldName.TotalItems);

        var newName = SearchCatalogProjection.Match(projection.GetView(), "Trail", 0, 10);
        var item = Assert.Single(newName.Items);
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal("Trail boot", item.Name);
        Assert.Equal("Lightweight boot", item.Description);
        Assert.Equal(150m, item.Price);
        Assert.Equal("trail.png", item.PictureUrl);
    }

    [Fact]
    public void Removed_items_are_excluded()
    {
        var kept = Guid.NewGuid();
        var gone = Guid.NewGuid();
        var projection = new SearchCatalogProjection();

        projection.ProcessEvent(Added(kept, "Alpine boot", null, 120m, null, 4), 1);
        projection.ProcessEvent(Added(gone, "Alpine sock", null, 12m, null, 8), 2);
        projection.ProcessEvent(new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, gone), 3);
        projection.ProcessEvent(
            new CatalogItemUpdated(
                Guid.NewGuid(),
                DateTime.UtcNow,
                gone,
                "Alpine sock revived",
                null,
                12m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null),
            4);

        var match = SearchCatalogProjection.Match(projection.GetView(), "Alpine", 0, 10);
        var item = Assert.Single(match.Items);
        Assert.Equal(kept, item.ItemId);
        Assert.Equal(1, match.TotalItems);
    }

    [Fact]
    public void Restock_and_deduct_change_stock_on_a_listed_item()
    {
        var itemId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var projection = new SearchCatalogProjection();

        projection.ProcessEvent(Added(itemId, "Alpine boot", null, 120m, null, 10), 1);
        projection.ProcessEvent(Added(otherId, "Alpine hat", null, 20m, null, 6), 2);
        projection.ProcessEvent(
            new CatalogItemRestocked(Guid.NewGuid(), DateTime.UtcNow, itemId, 5, 15),
            3);
        projection.ProcessEvent(
            new CatalogStockDeducted(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Guid.NewGuid(),
                [
                    new CatalogStockDeduction(itemId, 4),
                    new CatalogStockDeduction(otherId, 2),
                    new CatalogStockDeduction(itemId, 1)
                ]),
            4);

        var match = SearchCatalogProjection.Match(projection.GetView(), "Alpine", 0, 10);
        Assert.Equal(2, match.TotalItems);
        var boot = Assert.Single(match.Items, item => item.ItemId == itemId);
        var hat = Assert.Single(match.Items, item => item.ItemId == otherId);
        Assert.Equal(10, boot.AvailableStock);
        Assert.True(boot.IsAvailable);
        Assert.Equal(4, hat.AvailableStock);
        Assert.True(hat.IsAvailable);
    }

    [Fact]
    public void Paging_returns_a_page_and_the_total_count()
    {
        var projection = new SearchCatalogProjection();
        projection.ProcessEvent(Added(Guid.NewGuid(), "Alpine tent", null, 80m, null, 1), 1);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Alpine boot", null, 120m, null, 1), 2);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Alpine hat", null, 20m, null, 1), 3);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Trail glove", null, 30m, null, 1), 4);

        var first = SearchCatalogProjection.Match(projection.GetView(), "Alpine", 0, 2);
        Assert.Equal(3, first.TotalItems);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal("Alpine boot", first.Items[0].Name);
        Assert.Equal("Alpine hat", first.Items[1].Name);

        var second = SearchCatalogProjection.Match(projection.GetView(), "Alpine", 1, 2);
        var last = Assert.Single(second.Items);
        Assert.Equal(3, second.TotalItems);
        Assert.Equal("Alpine tent", last.Name);
    }

    [Fact]
    public void Bad_paging_values_are_clamped()
    {
        var projection = new SearchCatalogProjection();
        for (var i = 0; i < 101; i++)
            projection.ProcessEvent(Added(Guid.NewGuid(), $"Alpine {i:D3}", null, 1m, null, 1), i + 1);

        var state = projection.GetView();

        var negativeIndex = SearchCatalogProjection.Match(state, "Alpine", -1, 10);
        Assert.Equal(101, negativeIndex.TotalItems);
        Assert.Equal(10, negativeIndex.Items.Count);
        Assert.Equal("Alpine 000", negativeIndex.Items[0].Name);

        var zeroSize = SearchCatalogProjection.Match(state, "Alpine", 0, 0);
        Assert.Equal(101, zeroSize.TotalItems);
        Assert.Equal(SearchCatalogProjection.DefaultPageSize, zeroSize.Items.Count);

        var huge = SearchCatalogProjection.Match(state, "Alpine", 0, 10_000);
        Assert.Equal(101, huge.TotalItems);
        Assert.Equal(SearchCatalogProjection.MaxPageSize, huge.Items.Count);
    }

    [Fact]
    public void Empty_text_and_unknown_prefix_return_no_rows()
    {
        var projection = new SearchCatalogProjection();
        projection.ProcessEvent(Added(Guid.NewGuid(), "Alpine boot", null, 120m, null, 1), 1);

        var blank = SearchCatalogProjection.Match(projection.GetView(), "", 0, 10);
        Assert.Equal("", blank.Query);
        Assert.Equal(0, blank.TotalItems);
        Assert.Empty(blank.Items);

        var missing = SearchCatalogProjection.Match(projection.GetView(), "Zzz", 0, 10);
        Assert.Equal("Zzz", missing.Query);
        Assert.Equal(0, missing.TotalItems);
        Assert.Empty(missing.Items);
    }

    [Fact]
    public async Task Get_returns_the_page()
    {
        await using var host = await SearchHost.StartAsync();
        var boot = Guid.NewGuid();
        var hat = Guid.NewGuid();
        var tent = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(Stream(boot), [Added(boot, "Alpine boot", "High-ankle boot", 120m, "boot.png", 4)]);
        await store.AppendAsync(Stream(hat), [Added(hat, "Alpine hat", null, 20m, null, 2)]);
        await store.AppendAsync(Stream(tent), [Added(tent, "Alpine tent", null, 80m, null, 1)]);

        var first = await host.PollAsync("Alpine", 0, 2, view => view.TotalItems == 3 && view.Items.Count == 2);
        Assert.Equal("Alpine boot", first.Items[0].Name);
        Assert.Equal("Alpine hat", first.Items[1].Name);

        var second = await host.PollAsync("Alpine", 1, 2, view => view.Items.Count == 1);
        Assert.Equal(3, second.TotalItems);
        Assert.Equal("Alpine tent", second.Items[0].Name);

        using var response = await host.Client.GetAsync(SearchHost.Url("Alpine", 0, 2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertModelFields(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_returns_an_empty_result()
    {
        await using var host = await SearchHost.StartAsync();

        var blank = await host.PollAsync("", 0, 10, view => view.TotalItems == 0);
        Assert.Empty(blank.Items);
        Assert.Equal("", blank.Query);

        var missing = await host.PollAsync("Zzz", 0, 10, view => view.Query == "Zzz");
        Assert.Equal(0, missing.TotalItems);
        Assert.Empty(missing.Items);
    }

    private static void AssertModelFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(new[] { "items", "query", "totalItems" }, names);

        var item = document.RootElement.GetProperty("items").EnumerateArray().First();
        var itemNames = item.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(
            new[]
            {
                "availableStock",
                "description",
                "isAvailable",
                "itemId",
                "name",
                "pictureUrl",
                "price"
            },
            itemNames);
    }

    private static string Stream(Guid itemId) => $"CatalogItem:{itemId:D}";

    private static CatalogItemAdded Added(
        Guid itemId,
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
            Guid.NewGuid(),
            Guid.NewGuid(),
            availableStock,
            2,
            40);

    private sealed class SearchHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private SearchHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<SearchHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddCatalogContext(typeof(SearchCatalogProjection).Assembly);

            var app = builder.Build();
            app.MapSearchCatalog();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new SearchHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async Task<SearchCatalogView> PollAsync(
            string text,
            int pageIndex,
            int pageSize,
            Func<SearchCatalogView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(text, pageIndex, pageSize));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<SearchCatalogView>(lastBody, ProjectionViewJson.Read);
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Search view was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public static string Url(string text, int pageIndex, int pageSize) =>
            $"{SearchCatalogEndpoint.Route}?text={Uri.EscapeDataString(text)}&pageIndex={pageIndex}&pageSize={pageSize}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
