using System.Text.Json;
using LawnDart;
using LawnDart.Messaging;
using LawnDart.Outbox;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Catalog.API.Slices.CatalogBrandsAndTypes;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Database.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class CatalogDatabaseTests
{
    private readonly DatabaseFixture _database;

    public CatalogDatabaseTests(DatabaseFixture database) => _database = database;

    [DatabaseFact]
    public async Task Define_brand_projects_one_view_writes_one_outbox_row_and_claims_inbox_once()
    {
        var connectionString = await _database.CreateDatabaseAsync("catalog");
        using var host = DatabaseHosts.Catalog(_database.Database, connectionString);

        await host.InitializeEShopLawnDartSchemaAsync();
        var before = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);
        await host.InitializeEShopLawnDartSchemaAsync();
        Assert.Equal(before, await SchemaSnapshot.ReadAsync(_database.Database, connectionString));

        var brandId = Guid.NewGuid();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<DefineCatalogBrandCommand>>();
            await handler.HandleAsync(new DefineCatalogBrandCommand(Guid.NewGuid(), brandId, "Alpine"));
        }

        var outbox = host.Services.GetRequiredKeyedService<IOutboxWriter>(EShopLawnDartHost.ContextName);
        var pending = await outbox.GetUnprocessedAsync(10);
        var message = Assert.Single(pending);
        Assert.False(string.IsNullOrWhiteSpace(message.EventType));

        var inbox = host.Services.GetRequiredService<IInboxStore>();
        Assert.True(await inbox.TryClaimAsync("catalog-database-test", CancellationToken.None));
        Assert.False(await inbox.TryClaimAsync("catalog-database-test", CancellationToken.None));

        var runners = host.Services.GetRequiredKeyedService<IProjectionRunnerManager>(EShopLawnDartHost.ContextName);
        await runners.StartAsync(CatalogBrandsAndTypesProjection.StorageKey);

        var views = host.Services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName);
        CatalogBrandsAndTypesView? view = null;
        var until = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < until)
        {
            var json = await views.GetViewAsync(
                CatalogBrandsAndTypesProjection.StorageKey,
                ProjectionInstanceIds.UnpartitionedBase);
            if (json is not null)
            {
                view = JsonSerializer.Deserialize<CatalogBrandsAndTypesView>(json, ProjectionViewJson.Read);
                if (view?.Brands.Any(brand => brand.BrandId == brandId && brand.Name == "Alpine") == true)
                    break;
            }

            await Task.Delay(200);
        }

        Assert.NotNull(view);
        Assert.Contains(view.Brands, brand => brand.BrandId == brandId && brand.Name == "Alpine");

        await runners.StopAsync(CatalogBrandsAndTypesProjection.StorageKey);
        var afterWork = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);
        await host.InitializeEShopLawnDartSchemaAsync();
        Assert.Equal(afterWork, await SchemaSnapshot.ReadAsync(_database.Database, connectionString));
    }
}
