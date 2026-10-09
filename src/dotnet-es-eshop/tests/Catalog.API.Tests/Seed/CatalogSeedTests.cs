using System.Net;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using LawnDart.Samples.EShop.Catalog.API.Seed;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogType;
using LawnDart.Samples.EShop.Catalog.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Catalog.API.Tests.Seed;

public class CatalogSeedTests
{
    [Fact]
    public async Task Empty_store_seeds_brands_types_and_items()
    {
        var catalog = CatalogSeedCatalog.Load();
        await using var host = await CatalogSeedHost.StartAsync(enabled: true);
        var before = await host.CountSeedEventsAsync(catalog);

        await host.SeedAsync();

        Assert.Equal(0, before);
        Assert.Equal(13, catalog.Brands.Count);
        Assert.Equal(8, catalog.Types.Count);
        Assert.Equal(101, catalog.Items.Count);
        Assert.Equal(13 + 8 + 101, await host.CountSeedEventsAsync(catalog));

        var first = catalog.Items[0];
        var item = await host.Repository.GetOrCreateAsync<CatalogItem>(first.ItemId);
        Assert.Equal("Wanderer Black Hiking Boots", item.State.Name);
        Assert.Equal(109.99m, item.State.Price);
        Assert.Equal(100, item.State.AvailableStock);
        Assert.Equal(10, item.State.RestockThreshold);
        Assert.Equal(200, item.State.MaxStockThreshold);
        Assert.Equal("1.webp", item.State.PictureFileName);
        Assert.Equal(catalog.Brands[0].BrandId, item.State.BrandId);
        Assert.True((await host.Repository.GetOrCreateAsync<CatalogBrand>(catalog.Brands[0].BrandId)).State.Exists);
        Assert.True((await host.Repository.GetOrCreateAsync<CatalogType>(catalog.Types[0].TypeId)).State.Exists);

        var after = await host.CountSeedEventsAsync(catalog);
        await host.SeedAsync();
        Assert.Equal(after, await host.CountSeedEventsAsync(catalog));
    }

    [Fact]
    public async Task Partial_store_finishes_the_rest_without_duplicates()
    {
        var catalog = CatalogSeedCatalog.Load();
        await using var host = await CatalogSeedHost.StartAsync(enabled: true);
        var brand = catalog.Brands[0];
        await host.Brands.HandleAsync(CatalogSeedCatalog.BrandCommand(brand));
        Assert.Equal(1, await host.StreamLengthAsync($"CatalogBrand:{brand.BrandId}"));

        await host.SeedAsync();

        Assert.Equal(1, await host.StreamLengthAsync($"CatalogBrand:{brand.BrandId}"));
        Assert.Equal(13 + 8 + 101, await host.CountSeedEventsAsync(catalog));
        Assert.Contains(host.Logs, line => line.Contains("already", StringComparison.OrdinalIgnoreCase));

        var events = await host.CountSeedEventsAsync(catalog);
        await host.SeedAsync();
        Assert.Equal(events, await host.CountSeedEventsAsync(catalog));
    }

    [Fact]
    public async Task Removed_item_does_not_resend_the_catalog()
    {
        var catalog = CatalogSeedCatalog.Load();
        await using var host = await CatalogSeedHost.StartAsync(enabled: true);
        await host.SeedAsync();
        var seeded = await host.CountSeedEventsAsync(catalog);
        var item = catalog.Items[0];

        await host.Remove.HandleAsync(new RemoveCatalogItemCommand(Guid.NewGuid(), item.ItemId));

        var afterRemove = await host.CountSeedEventsAsync(catalog);
        Assert.Equal(seeded + 1, afterRemove);
        var removed = await host.Repository.GetOrCreateAsync<CatalogItem>(item.ItemId);
        Assert.True(removed.State.Removed);
        Assert.True(removed.Version > 0);

        var logCount = host.Logs.Count;
        await host.SeedAsync();

        Assert.Equal(afterRemove, await host.CountSeedEventsAsync(catalog));
        var addedLogs = host.Logs.Skip(logCount).ToArray();
        Assert.Contains(addedLogs, line => line.Contains("already present", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(addedLogs, line => line.Contains("skipped a command", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Disabled_seed_writes_nothing()
    {
        var catalog = CatalogSeedCatalog.Load();
        await using var host = await CatalogSeedHost.StartAsync(enabled: false);

        await host.SeedAsync();

        Assert.Equal(0, await host.CountSeedEventsAsync(catalog));
        Assert.Contains(host.Logs, line => line.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Seed_pictures_match_catalog_items()
    {
        var catalog = CatalogSeedCatalog.Load();
        var pics = FindPicsDirectory();
        foreach (var item in catalog.Items)
            Assert.True(File.Exists(Path.Combine(pics, item.PictureFileName)), item.PictureFileName);
    }

    private static string FindPicsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Catalog.API", "Seed", "Pics");
            if (Directory.Exists(candidate))
                return candidate;

            candidate = Path.Combine(dir.FullName, "Seed", "Pics");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(dir.FullName, "Seed", "catalog.json")))
                return candidate;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Catalog seed pictures were not found.");
    }

    private sealed class CatalogSeedHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly AsyncServiceScope _scope;
        private readonly List<string> _logs;

        private CatalogSeedHost(
            WebApplication app,
            AsyncServiceScope scope,
            List<string> logs,
            CatalogSeeder seeder,
            IEventStore store,
            IAggregateRepository repository,
            ICommandHandler<DefineCatalogBrandCommand> brands,
            ICommandHandler<RemoveCatalogItemCommand> remove)
        {
            _app = app;
            _scope = scope;
            _logs = logs;
            Seeder = seeder;
            Store = store;
            Repository = repository;
            Brands = brands;
            Remove = remove;
        }

        public CatalogSeeder Seeder { get; }

        public IEventStore Store { get; }

        public IAggregateRepository Repository { get; }

        public ICommandHandler<DefineCatalogBrandCommand> Brands { get; }

        public ICommandHandler<RemoveCatalogItemCommand> Remove { get; }

        public IReadOnlyList<string> Logs => _logs;

        public Task SeedAsync() => Seeder.SeedAsync();

        public static async Task<CatalogSeedHost> StartAsync(bool enabled)
        {
            var logs = new List<string>();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new ListLoggerProvider(logs));
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EShop:Seed:Enabled"] = enabled ? "true" : "false"
            });
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddCatalogContext(typeof(CatalogItem).Assembly, typeof(OrderStockConfirmed).Assembly);
            builder.AddCatalogSeed();

            var app = builder.Build();
            var scope = app.Services.CreateAsyncScope();
            var provider = scope.ServiceProvider;
            return new CatalogSeedHost(
                app,
                scope,
                logs,
                provider.GetRequiredService<CatalogSeeder>(),
                provider.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName),
                provider.GetRequiredService<IAggregateRepository>(),
                provider.GetRequiredService<ICommandHandler<DefineCatalogBrandCommand>>(),
                provider.GetRequiredService<ICommandHandler<RemoveCatalogItemCommand>>());
        }

        public async Task<int> CountSeedEventsAsync(CatalogSeedCatalog catalog)
        {
            var total = 0;
            foreach (var brand in catalog.Brands)
                total += await StreamLengthAsync($"CatalogBrand:{brand.BrandId}");
            foreach (var type in catalog.Types)
                total += await StreamLengthAsync($"CatalogType:{type.TypeId}");
            foreach (var item in catalog.Items)
                total += await StreamLengthAsync($"CatalogItem:{item.ItemId}");
            return total;
        }

        public async Task<int> StreamLengthAsync(string streamId)
        {
            var recorded = await Store.ReadStreamAsync(streamId);
            var count = 0;
            foreach (var _ in recorded)
                count++;
            return count;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _logs;

        public ListLoggerProvider(List<string> logs) => _logs = logs;

        public ILogger CreateLogger(string categoryName) => new ListLogger(_logs);

        public void Dispose()
        {
        }

        private sealed class ListLogger : ILogger
        {
            private readonly List<string> _logs;

            public ListLogger(List<string> logs) => _logs = logs;

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull =>
                NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Information)
                    _logs.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

public class CatalogItemPictureTests
{
    [Fact]
    public async Task Picture_route_returns_the_seed_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "catalog-pics-" + Guid.NewGuid().ToString("N"));
        var pics = Path.Combine(root, "Seed", "Pics");
        Directory.CreateDirectory(pics);
        var payload = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x01 };
        await File.WriteAllBytesAsync(Path.Combine(pics, "1.webp"), payload);
        await File.WriteAllBytesAsync(Path.Combine(root, "secret.txt"), [9]);

        try
        {
            await using var host = await PictureHost.StartAsync(root);
            var itemId = Guid.NewGuid();
            var missingFileId = Guid.NewGuid();
            var outsideId = Guid.NewGuid();
            await host.AddItemAsync(itemId, "1.webp");
            await host.AddItemAsync(missingFileId, "missing.webp");
            await host.AddItemAsync(outsideId, "../secret.txt");

            var found = await host.Client.GetAsync($"/api/catalog/items/{itemId:D}/pic");
            var missingItem = await host.Client.GetAsync($"/api/catalog/items/{Guid.NewGuid():D}/pic");
            var missingFile = await host.Client.GetAsync($"/api/catalog/items/{missingFileId:D}/pic");
            var outside = await host.Client.GetAsync($"/api/catalog/items/{outsideId:D}/pic");

            Assert.Equal(HttpStatusCode.OK, found.StatusCode);
            Assert.Equal("image/webp", found.Content.Headers.ContentType?.MediaType);
            Assert.Equal(payload, await found.Content.ReadAsByteArrayAsync());
            Assert.Equal(HttpStatusCode.NotFound, missingItem.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingFile.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class PictureHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private PictureHost(WebApplication app, HttpClient client, ICommandHandler<AddCatalogItemCommand> items)
        {
            _app = app;
            Client = client;
            Items = items;
        }

        public HttpClient Client { get; }

        public ICommandHandler<AddCatalogItemCommand> Items { get; }

        public static async Task<PictureHost> StartAsync(string contentRoot)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = contentRoot
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddCatalogContext(typeof(CatalogItem).Assembly, typeof(OrderStockConfirmed).Assembly);

            var app = builder.Build();
            app.MapCatalogItemPictures();
            await app.StartAsync();
            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new PictureHost(
                app,
                new HttpClient { BaseAddress = new Uri(address) },
                app.Services.GetRequiredService<ICommandHandler<AddCatalogItemCommand>>());
        }

        public Task AddItemAsync(Guid itemId, string? pictureFileName) =>
            Items.HandleAsync(new AddCatalogItemCommand(
                Guid.NewGuid(),
                itemId,
                "Seed boot",
                10m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "Boot",
                pictureFileName));

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
            Client.Dispose();
        }
    }
}
