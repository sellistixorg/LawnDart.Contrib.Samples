using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Slices.BrowseCatalog;
using LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;
using LawnDart.Samples.EShop.Catalog.API.Slices.DeductCatalogStock;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;
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

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.BrowseCatalog;

public class BrowseCatalogTests
{
    private static readonly Guid BrandA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid BrandB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    private static readonly Guid BrandUnused = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
    private static readonly Guid TypeT = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
    private static readonly Guid TypeU = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    private static readonly Guid TypeUnused = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3");

    [Fact]
    public void Filters_by_brand()
    {
        var projection = Catalog();
        var view = BrowseCatalogProjection.Page(projection.GetView(), [BrandA], null, 0, 10);

        Assert.Equal(3, view.TotalItems);
        Assert.Equal(["Alpine", "Camp", "Trail"], view.Items.Select(item => item.Name).ToArray());
        Assert.Equal([BrandA], view.SelectedBrandIds);
        Assert.Empty(view.SelectedTypeIds);
    }

    [Fact]
    public void Filters_by_type()
    {
        var projection = Catalog();
        var view = BrowseCatalogProjection.Page(projection.GetView(), null, [TypeT], 0, 10);

        Assert.Equal(3, view.TotalItems);
        Assert.Equal(["Alpine", "Camp", "Summit"], view.Items.Select(item => item.Name).ToArray());
        Assert.Equal([TypeT], view.SelectedTypeIds);
    }

    [Fact]
    public void Filters_by_brand_and_type()
    {
        var projection = Catalog();
        var view = BrowseCatalogProjection.Page(projection.GetView(), [BrandA], [TypeT], 0, 10);

        Assert.Equal(2, view.TotalItems);
        Assert.Equal(["Alpine", "Camp"], view.Items.Select(item => item.Name).ToArray());
        Assert.Equal([BrandA], view.SelectedBrandIds);
        Assert.Equal([TypeT], view.SelectedTypeIds);
        Assert.Equal(120m, view.Items[0].Price);
        Assert.Equal("boot.png", view.Items[0].PictureUrl);
        Assert.Equal(4, view.Items[0].AvailableStock);
    }

    [Fact]
    public void Paging_returns_a_page_and_the_total()
    {
        var projection = Catalog();
        var first = BrowseCatalogProjection.Page(projection.GetView(), null, null, 0, 2);
        Assert.Equal(4, first.TotalItems);
        Assert.Equal(0, first.PageIndex);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(["Alpine", "Camp"], first.Items.Select(item => item.Name).ToArray());

        var second = BrowseCatalogProjection.Page(projection.GetView(), null, null, 1, 2);
        Assert.Equal(4, second.TotalItems);
        Assert.Equal(["Summit", "Trail"], second.Items.Select(item => item.Name).ToArray());
    }

    [Fact]
    public void Rows_are_ordered_by_name()
    {
        var later = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var earlier = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var projection = new BrowseCatalogProjection();
        projection.ProcessEvent(Added(Guid.NewGuid(), "Trail", 10m, null, 1, BrandB, TypeU), 1);
        projection.ProcessEvent(Added(later, "Alpine", 20m, null, 1, BrandA, TypeT), 2);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Camp", 30m, null, 1, BrandA, TypeT), 3);
        projection.ProcessEvent(Added(earlier, "Alpine", 40m, null, 1, BrandB, TypeU), 4);

        var names = BrowseCatalogProjection.Page(projection.GetView(), null, null, 0, 10).Items;
        Assert.Equal(earlier, names[0].ItemId);
        Assert.Equal(later, names[1].ItemId);
        Assert.Equal("Camp", names[2].Name);
        Assert.Equal("Trail", names[3].Name);
    }

    [Fact]
    public void Rename_and_price_change_update_the_row()
    {
        var itemId = Guid.NewGuid();
        var projection = new BrowseCatalogProjection();
        projection.ProcessEvent(Added(itemId, "Camp", 40m, "camp.png", 3, BrandA, TypeT), 1);
        projection.ProcessEvent(
            new CatalogItemUpdated(
                Guid.NewGuid(),
                DateTime.UtcNow,
                itemId,
                "Alpine",
                "Renamed",
                55m,
                BrandB,
                TypeU,
                "alpine.png"),
            2);
        projection.ProcessEvent(
            new CatalogItemPriceChanged(Guid.NewGuid(), DateTime.UtcNow, itemId, 55m, 70m),
            3);

        var view = BrowseCatalogProjection.Page(projection.GetView(), [BrandB], [TypeU], 0, 10);
        var item = Assert.Single(view.Items);
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal("Alpine", item.Name);
        Assert.Equal(70m, item.Price);
        Assert.Equal("alpine.png", item.PictureUrl);
    }

    [Fact]
    public void Removed_items_are_excluded()
    {
        var projection = Catalog();
        var view = BrowseCatalogProjection.Page(projection.GetView(), null, null, 0, 10);

        Assert.DoesNotContain(view.Items, item => item.Name == "Gone");
        Assert.Equal(4, view.TotalItems);
        Assert.Equal(3, CountOf(view.BrandFacets, BrandA));
        Assert.Equal(3, CountOf(view.TypeFacets, TypeT));
    }

    [Fact]
    public void Facet_counts_follow_the_other_filter()
    {
        var projection = Catalog();
        var open = BrowseCatalogProjection.Page(projection.GetView(), null, null, 0, 10);
        Assert.Equal(3, CountOf(open.BrandFacets, BrandA));
        Assert.Equal(1, CountOf(open.BrandFacets, BrandB));
        Assert.Equal(0, CountOf(open.BrandFacets, BrandUnused));
        Assert.Equal(3, CountOf(open.TypeFacets, TypeT));
        Assert.Equal(1, CountOf(open.TypeFacets, TypeU));
        Assert.Equal(0, CountOf(open.TypeFacets, TypeUnused));

        var typeOnly = BrowseCatalogProjection.Page(projection.GetView(), null, [TypeT], 0, 10);
        Assert.Equal(2, CountOf(typeOnly.BrandFacets, BrandA));
        Assert.Equal(1, CountOf(typeOnly.BrandFacets, BrandB));
        Assert.Equal(3, CountOf(typeOnly.TypeFacets, TypeT));
        Assert.Equal(1, CountOf(typeOnly.TypeFacets, TypeU));

        var brandOnly = BrowseCatalogProjection.Page(projection.GetView(), [BrandA], null, 0, 10);
        Assert.Equal(3, CountOf(brandOnly.BrandFacets, BrandA));
        Assert.Equal(1, CountOf(brandOnly.BrandFacets, BrandB));
        Assert.Equal(2, CountOf(brandOnly.TypeFacets, TypeT));
        Assert.Equal(1, CountOf(brandOnly.TypeFacets, TypeU));

        var both = BrowseCatalogProjection.Page(projection.GetView(), [BrandA], [TypeT], 0, 10);
        Assert.Equal(2, CountOf(both.BrandFacets, BrandA));
        Assert.Equal(1, CountOf(both.BrandFacets, BrandB));
        Assert.Equal(2, CountOf(both.TypeFacets, TypeT));
        Assert.Equal(1, CountOf(both.TypeFacets, TypeU));
    }

    [Fact]
    public void Brand_and_type_defined_after_items_count_those_items()
    {
        var browse = new BrowseCatalogProjection();
        var names = new CatalogBrandsAndTypesProjection();
        var itemId = Guid.NewGuid();

        browse.ProcessEvent(Added(itemId, "Alpine", 120m, "boot.png", 4, BrandA, TypeT), 1);

        var before = BrowseCatalogProjection.Page(browse.GetView(), null, null, 0, 10);
        var row = Assert.Single(before.Items);
        Assert.Equal(itemId, row.ItemId);
        Assert.Equal(1, CountOf(before.BrandFacets, BrandA));
        Assert.Equal(1, CountOf(before.TypeFacets, TypeT));
        Assert.Empty(names.GetView().Brands);
        Assert.Empty(names.GetView().Types);

        var brand = Brand(BrandA, "Alpine works");
        var type = Type(TypeT, "Clothing");
        browse.ProcessEvent(brand, 2);
        browse.ProcessEvent(type, 3);
        names.ProcessEvent(brand, 1);
        names.ProcessEvent(type, 2);

        var after = BrowseCatalogProjection.Page(browse.GetView(), null, null, 0, 10);
        Assert.Equal(1, after.TotalItems);
        Assert.Equal(1, CountOf(after.BrandFacets, BrandA));
        Assert.Equal(1, CountOf(after.TypeFacets, TypeT));

        var brandName = Assert.Single(names.GetView().Brands);
        Assert.Equal(BrandA, brandName.BrandId);
        Assert.Equal("Alpine works", brandName.Name);
        var typeName = Assert.Single(names.GetView().Types);
        Assert.Equal(TypeT, typeName.TypeId);
        Assert.Equal("Clothing", typeName.Name);
    }

    [Fact]
    public void Undefined_brand_and_type_stay_in_the_rows_and_the_all_count()
    {
        var browse = new BrowseCatalogProjection();
        var names = new CatalogBrandsAndTypesProjection();
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        browse.ProcessEvent(Added(itemId, "Alpine", 120m, null, 4, brandId, typeId), 1);
        names.ProcessEvent(Brand(BrandA, "Named"), 1);
        names.ProcessEvent(Type(TypeT, "Clothing"), 2);

        var view = BrowseCatalogProjection.Page(browse.GetView(), null, null, 0, 10);
        var row = Assert.Single(view.Items);
        Assert.Equal(itemId, row.ItemId);
        Assert.Equal(1, view.TotalItems);
        Assert.Equal(1, CountOf(view.BrandFacets, brandId));
        Assert.Equal(1, CountOf(view.TypeFacets, typeId));

        var all = view.BrandFacets.Sum(facet => facet.Count);
        Assert.Equal(1, all);
        Assert.DoesNotContain(names.GetView().Brands, brand => brand.BrandId == brandId);
        Assert.DoesNotContain(names.GetView().Types, type => type.TypeId == typeId);

        var visible = names.GetView().Brands.Select(brand => brand.BrandId).ToHashSet();
        var visibleSum = view.BrandFacets.Where(facet => visible.Contains(facet.Id)).Sum(facet => facet.Count);
        Assert.True(all > visibleSum);
    }

    [Fact]
    public void Removed_items_leave_a_facet_with_count_zero()
    {
        var browse = new BrowseCatalogProjection();
        var itemId = Guid.NewGuid();

        browse.ProcessEvent(Brand(BrandA, "Alpine"), 1);
        browse.ProcessEvent(Type(TypeT, "Clothing"), 2);
        browse.ProcessEvent(Added(itemId, "Alpine", 120m, null, 4, BrandA, TypeT), 3);
        browse.ProcessEvent(new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, itemId), 4);

        var view = BrowseCatalogProjection.Page(browse.GetView(), null, null, 0, 10);
        Assert.Empty(view.Items);
        Assert.Equal(0, view.TotalItems);
        Assert.Equal(0, CountOf(view.BrandFacets, BrandA));
        Assert.Equal(0, CountOf(view.TypeFacets, TypeT));
    }

    [Fact]
    public void Bad_paging_values_are_clamped()
    {
        var projection = new BrowseCatalogProjection();
        for (var i = 0; i < 101; i++)
            projection.ProcessEvent(Added(Guid.NewGuid(), $"Item {i:D3}", 1m, null, 1, BrandA, TypeT), i + 1);

        var state = projection.GetView();

        var negativeIndex = BrowseCatalogProjection.Page(state, null, null, -1, 10);
        Assert.Equal(0, negativeIndex.PageIndex);
        Assert.Equal(101, negativeIndex.TotalItems);
        Assert.Equal(10, negativeIndex.Items.Count);
        Assert.Equal("Item 000", negativeIndex.Items[0].Name);

        var zeroSize = BrowseCatalogProjection.Page(state, null, null, 0, 0);
        Assert.Equal(BrowseCatalogProjection.DefaultPageSize, zeroSize.PageSize);
        Assert.Equal(BrowseCatalogProjection.DefaultPageSize, zeroSize.Items.Count);

        var huge = BrowseCatalogProjection.Page(state, null, null, 0, 10_000);
        Assert.Equal(BrowseCatalogProjection.MaxPageSize, huge.PageSize);
        Assert.Equal(BrowseCatalogProjection.MaxPageSize, huge.Items.Count);
    }

    [Fact]
    public void Restock_and_deduct_change_available_stock()
    {
        var itemId = Guid.NewGuid();
        var projection = new BrowseCatalogProjection();
        projection.ProcessEvent(Added(itemId, "Alpine", 10m, null, 10, BrandA, TypeT), 1);
        projection.ProcessEvent(
            new CatalogItemRestocked(Guid.NewGuid(), DateTime.UtcNow, itemId, 5, 15),
            2);
        projection.ProcessEvent(
            new CatalogStockDeducted(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Guid.NewGuid(),
                [
                    new CatalogStockDeduction(itemId, 4),
                    new CatalogStockDeduction(itemId, 1)
                ]),
            3);

        var item = Assert.Single(BrowseCatalogProjection.Page(projection.GetView(), null, null, 0, 10).Items);
        Assert.Equal(10, item.AvailableStock);
    }

    [Fact]
    public async Task Get_returns_the_filtered_page()
    {
        await using var host = await BrowseHost.StartAsync();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);
        var alpine = Guid.NewGuid();
        var camp = Guid.NewGuid();
        var summit = Guid.NewGuid();

        await store.AppendAsync($"CatalogBrand:{BrandA:D}", [Brand(BrandA, "Alpine")]);
        await store.AppendAsync($"CatalogBrand:{BrandB:D}", [Brand(BrandB, "Summit")]);
        await store.AppendAsync($"CatalogType:{TypeT:D}", [Type(TypeT, "Clothing")]);
        await store.AppendAsync(Stream(alpine), [Added(alpine, "Alpine", 120m, "boot.png", 4, BrandA, TypeT)]);
        await store.AppendAsync(Stream(camp), [Added(camp, "Camp", 40m, null, 2, BrandA, TypeU)]);
        await store.AppendAsync(Stream(summit), [Added(summit, "Summit", 80m, null, 1, BrandB, TypeT)]);

        var view = await host.PollAsync(
            [BrandA],
            [TypeT],
            0,
            10_000,
            page => page.TotalItems == 1 && page.PageSize == BrowseCatalogProjection.MaxPageSize);
        var item = Assert.Single(view.Items);
        Assert.Equal(alpine, item.ItemId);
        Assert.Equal("Alpine", item.Name);
        Assert.Equal(120m, item.Price);
        Assert.Equal("boot.png", item.PictureUrl);
        Assert.Equal(4, item.AvailableStock);
        Assert.Equal(0, view.PageIndex);
        Assert.Equal([BrandA], view.SelectedBrandIds);
        Assert.Equal([TypeT], view.SelectedTypeIds);
        Assert.Equal(1, CountOf(view.BrandFacets, BrandA));
        Assert.Equal(1, CountOf(view.BrandFacets, BrandB));
        Assert.Equal(1, CountOf(view.TypeFacets, TypeT));
        Assert.Equal(1, CountOf(view.TypeFacets, TypeU));

        using var response = await host.Client.GetAsync(BrowseHost.Url([BrandA], [TypeT], 0, 10_000));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertModelFields(await response.Content.ReadAsStringAsync());
    }

    private static BrowseCatalogProjection Catalog()
    {
        var projection = new BrowseCatalogProjection();
        var version = 1;
        projection.ProcessEvent(Brand(BrandA, "Alpine"), version++);
        projection.ProcessEvent(Brand(BrandB, "Summit"), version++);
        projection.ProcessEvent(Brand(BrandUnused, "Unused"), version++);
        projection.ProcessEvent(Type(TypeT, "Clothing"), version++);
        projection.ProcessEvent(Type(TypeU, "Equipment"), version++);
        projection.ProcessEvent(Type(TypeUnused, "Spare"), version++);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Trail", 20m, null, 1, BrandA, TypeU), version++);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Summit", 80m, null, 1, BrandB, TypeT), version++);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Camp", 40m, null, 2, BrandA, TypeT), version++);
        var gone = Guid.NewGuid();
        projection.ProcessEvent(Added(gone, "Gone", 5m, null, 1, BrandA, TypeT), version++);
        projection.ProcessEvent(Added(Guid.NewGuid(), "Alpine", 120m, "boot.png", 4, BrandA, TypeT), version++);
        projection.ProcessEvent(new CatalogItemRemoved(Guid.NewGuid(), DateTime.UtcNow, gone), version);
        return projection;
    }

    private static int CountOf(IEnumerable<BrowseCatalogFacet> facets, Guid id)
    {
        var facet = Assert.Single(facets, candidate => candidate.Id == id);
        return facet.Count;
    }

    private static void AssertModelFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(
            new[]
            {
                "brandFacets",
                "items",
                "pageIndex",
                "pageSize",
                "selectedBrandIds",
                "selectedTypeIds",
                "totalItems",
                "typeFacets"
            },
            names);

        var item = document.RootElement.GetProperty("items").EnumerateArray().First();
        var itemNames = item.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(
            new[] { "availableStock", "itemId", "name", "pictureUrl", "price" },
            itemNames);

        var facet = document.RootElement.GetProperty("brandFacets").EnumerateArray().First();
        var facetNames = facet.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(new[] { "count", "id" }, facetNames);
    }

    private static string Stream(Guid itemId) => $"CatalogItem:{itemId:D}";

    private static CatalogItemAdded Added(
        Guid itemId,
        string name,
        decimal price,
        string? pictureFileName,
        int availableStock,
        Guid brandId,
        Guid typeId) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            itemId,
            name,
            null,
            price,
            pictureFileName,
            brandId,
            typeId,
            availableStock,
            2,
            40);

    private static CatalogBrandDefined Brand(Guid brandId, string name) =>
        new(Guid.NewGuid(), DateTime.UtcNow, brandId, name);

    private static CatalogTypeDefined Type(Guid typeId, string name) =>
        new(Guid.NewGuid(), DateTime.UtcNow, typeId, name);

    private sealed class BrowseHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private BrowseHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<BrowseHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddCatalogContext(typeof(BrowseCatalogProjection).Assembly);

            var app = builder.Build();
            app.MapBrowseCatalog();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new BrowseHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async Task<BrowseCatalogView> PollAsync(
            IReadOnlyList<Guid> brands,
            IReadOnlyList<Guid> types,
            int pageIndex,
            int pageSize,
            Func<BrowseCatalogView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(brands, types, pageIndex, pageSize));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<BrowseCatalogView>(lastBody, ProjectionViewJson.Read);
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Browse view was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public static string Url(IReadOnlyList<Guid> brands, IReadOnlyList<Guid> types, int pageIndex, int pageSize)
        {
            var query = $"{BrowseCatalogEndpoint.Route}?pageIndex={pageIndex}&pageSize={pageSize}";
            foreach (var brand in brands)
                query += $"&brand={Uri.EscapeDataString(brand.ToString("D"))}";
            foreach (var type in types)
                query += $"&type={Uri.EscapeDataString(type.ToString("D"))}";
            return query;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
