using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;
using LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Database.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class OrderingSharedDatabaseTests
{
    private readonly DatabaseFixture _database;

    public OrderingSharedDatabaseTests(DatabaseFixture database) => _database = database;

    [DatabaseFact]
    public async Task One_setup_serves_ordering_and_the_processor_can_read_views()
    {
        var connectionString = await _database.CreateDatabaseAsync("ordering");
        using var ordering = DatabaseHosts.Ordering(_database.Database, connectionString);
        using var processor = DatabaseHosts.OrderProcessor(_database.Database, connectionString);

        await ordering.InitializeEShopLawnDartSchemaAsync();
        var first = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);
        await ordering.InitializeEShopLawnDartSchemaAsync();
        Assert.Equal(first, await SchemaSnapshot.ReadAsync(_database.Database, connectionString));

        var orderId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        var processorViews = processor.Services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName);
        await new PendingGraceOrders(processorViews).RememberAsync(orderId, startedAt);

        var runners = ordering.Services.GetRequiredKeyedService<IProjectionRunnerManager>(EShopLawnDartHost.ContextName);
        using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await runners.StartAsync(OrdersByShopperProjection.StorageKey, startup.Token);
        try
        {
            using var read = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var json = await processorViews.GetViewAsync(
                PendingGraceOrders.ViewType,
                orderId.ToString("D"),
                read.Token);
            Assert.NotNull(json);
            Assert.Contains("2026-11-08", json, StringComparison.Ordinal);
        }
        finally
        {
            await runners.StopAsync(OrdersByShopperProjection.StorageKey);
        }
    }
}
