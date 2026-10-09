using System.Net;
using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Slices.CatalogBrandsAndTypes;

public class CatalogBrandsAndTypesTests
{
    [Fact]
    public void Brands_and_types_appear_with_their_names()
    {
        var alpine = Guid.NewGuid();
        var trail = Guid.NewGuid();
        var clothing = Guid.NewGuid();
        var projection = new CatalogBrandsAndTypesProjection();

        projection.ProcessEvent(Brand(alpine, "Alpine"), 1);
        projection.ProcessEvent(Brand(trail, "Trail"), 2);
        projection.ProcessEvent(Type(clothing, "Clothing"), 3);
        projection.ProcessEvent(Brand(alpine, "Alpine Co"), 4);

        var view = projection.GetView();

        Assert.Equal(2, view.Brands.Count);
        Assert.Equal(alpine, view.Brands[0].BrandId);
        Assert.Equal("Alpine Co", view.Brands[0].Name);
        Assert.Equal(trail, view.Brands[1].BrandId);
        Assert.Equal("Trail", view.Brands[1].Name);
        var type = Assert.Single(view.Types);
        Assert.Equal(clothing, type.TypeId);
        Assert.Equal("Clothing", type.Name);
    }

    [Fact]
    public async Task Get_returns_brand_and_type_names()
    {
        var brandId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        await using var app = await StartAppAsync();
        var store = app.Services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName);
        var projection = new CatalogBrandsAndTypesProjection();
        projection.ProcessEvent(Brand(brandId, "Alpine"), 1);
        projection.ProcessEvent(Type(typeId, "Clothing"), 2);
        await store.SaveViewAsync(
            CatalogBrandsAndTypesProjection.StorageKey,
            "global",
            JsonSerializer.Serialize(projection.GetView(), ProjectionViewJson.Write),
            2);

        using var client = Client(app);
        var response = await client.GetAsync(CatalogBrandsAndTypesEndpoint.Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = JsonSerializer.Deserialize<CatalogBrandsAndTypesView>(
            await response.Content.ReadAsStringAsync(),
            ProjectionViewJson.Read);
        Assert.NotNull(view);
        var brand = Assert.Single(view.Brands);
        Assert.Equal(brandId, brand.BrandId);
        Assert.Equal("Alpine", brand.Name);
        var type = Assert.Single(view.Types);
        Assert.Equal(typeId, type.TypeId);
        Assert.Equal("Clothing", type.Name);
    }

    [Fact]
    public async Task Get_returns_empty_lists_when_nothing_is_defined()
    {
        await using var app = await StartAppAsync();

        using var client = Client(app);
        var response = await client.GetAsync(CatalogBrandsAndTypesEndpoint.Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = JsonSerializer.Deserialize<CatalogBrandsAndTypesView>(
            await response.Content.ReadAsStringAsync(),
            ProjectionViewJson.Read);
        Assert.NotNull(view);
        Assert.Empty(view.Brands);
        Assert.Empty(view.Types);
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(CatalogBrandsAndTypesTests).Assembly.GetName().Name
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);

        var app = builder.Build();
        app.MapCatalogBrandsAndTypesView();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses;
        return new HttpClient { BaseAddress = new Uri(addresses.Single()) };
    }

    private static CatalogBrandDefined Brand(Guid brandId, string name) =>
        new(Guid.NewGuid(), DateTime.UtcNow, brandId, name);

    private static CatalogTypeDefined Type(Guid typeId, string name) =>
        new(Guid.NewGuid(), DateTime.UtcNow, typeId, name);
}
