using LawnDart.Aggregates;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Aggregates.Order;
using LawnDart.Samples.EShop.Ordering.API.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Ordering.Commands;
using LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Projections.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.RabbitMq;

namespace LawnDart.Samples.EShop.Database.Tests;

/// <summary>
/// Ordering.API creates orderingdb. Order Processor then starts beside it and confirms one order.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderingRuntimeTests
{
    private readonly DatabaseFixture _database;

    public OrderingRuntimeTests(DatabaseFixture database) => _database = database;

    [DatabaseFact]
    public async Task Ordering_setup_then_both_processes_confirm_grace_without_schema_writes()
    {
        var connectionString = await _database.CreateDatabaseAsync("ordering_runtime");
        await using var rabbit = new RabbitMqBuilder("rabbitmq:4-alpine")
            .WithUsername("eshop")
            .WithPassword("eshop")
            .Build();
        await rabbit.StartAsync();
        var broker = rabbit.GetConnectionString();

        var time = new ManualTime(DateTimeOffset.UtcNow);
        using var ordering = BuildOrdering(_database.Database, connectionString, broker);
        using var processor = BuildProcessor(_database.Database, connectionString, broker, time);

        await ordering.InitializeEShopLawnDartSchemaAsync();
        await SchemaWriteAudit.InstallAndClearAsync(_database.Database, connectionString);
        var typesAfterSetup = await EventTypeTokens.ReadAsync(_database.Database, connectionString);
        var columnsAfterSetup = await ColumnFingerprintAsync(connectionString);
        Assert.Contains("grace-period-confirmed", typesAfterSetup);
        Assert.Contains("order-started", typesAfterSetup);
        Assert.DoesNotContain(EShopHostCatalog.ReadyToken, typesAfterSetup);

        var processorCatalog = processor.Services.GetRequiredKeyedService<IEventTypeCatalog>(EShopLawnDartHost.ContextName);
        Assert.True(processorCatalog.TryResolveType("grace-period-confirmed", out _));
        Assert.True(processorCatalog.TryResolveType("order-started", out _));
        Assert.False(processorCatalog.TryResolveType(EShopHostCatalog.ReadyToken, out _));

        await ordering.VerifyEShopLawnDartSchemaAsync();
        await ordering.StartAsync();
        await processor.VerifyEShopLawnDartSchemaAsync();
        await processor.StartAsync();
        try
        {
            var writesAfterStart = await SchemaWriteAudit.ReadAsync(_database.Database, connectionString);
            Assert.True(
                writesAfterStart.Length == 0,
                "Normal startup wrote schema commands: " + string.Join(", ", writesAfterStart));
            Assert.Equal(typesAfterSetup, await EventTypeTokens.ReadAsync(_database.Database, connectionString));
            Assert.Equal(columnsAfterSetup, await ColumnFingerprintAsync(connectionString));

            await WaitForConsumersAsync(rabbit);

            var orderId = Guid.NewGuid();
            var buyerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            await using (var scope = ordering.Services.CreateAsyncScope())
            {
                var orders = scope.ServiceProvider.GetRequiredService<IAggregateRepository>();
                var order = await orders.GetOrCreateAsync<Order>(orderId);
                await orders.HandleCommandAsync(
                    order,
                    new PlaceOrderCommand(
                        orderId,
                        orderId,
                        buyerId,
                        "Ada Lovelace",
                        "1 Main",
                        "Seattle",
                        "WA",
                        "USA",
                        "98101",
                        1,
                        "Ada Lovelace",
                        "4444",
                        new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                        [new OrderLine(productId, "Alpine boot", 120m, 2)]));
            }

            var views = processor.Services.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName);
            var pendingDeadline = DateTime.UtcNow.AddSeconds(40);
            string? pending = null;
            while (DateTime.UtcNow < pendingDeadline)
            {
                pending = await views.GetViewAsync(PendingGraceOrders.ViewType, orderId.ToString("D"));
                if (pending is not null)
                    break;
                await Task.Delay(200);
            }

            Assert.NotNull(pending);

            time.Advance(TimeSpan.FromMinutes(2));
            var manager = processor.Services.GetServices<IHostedService>().OfType<GracePeriodManagerService>().Single();
            await manager.ConfirmDueAsync();

            var store = ordering.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);
            var statusDeadline = DateTime.UtcNow.AddSeconds(20);
            OrderAwaitingValidation? awaiting = null;
            while (DateTime.UtcNow < statusDeadline)
            {
                var recorded = await store.ReadStreamAsync($"Order:{orderId}");
                awaiting = recorded.Select(row => row.Event).OfType<OrderAwaitingValidation>().SingleOrDefault();
                if (awaiting is not null)
                    break;
                await Task.Delay(200);
            }

            Assert.NotNull(awaiting);
            Assert.Equal(orderId, awaiting.OrderId);
            Assert.Null(await views.GetViewAsync(PendingGraceOrders.ViewType, orderId.ToString("D")));

            var writesAfterFlow = await SchemaWriteAudit.ReadAsync(_database.Database, connectionString);
            Assert.True(
                writesAfterFlow.Length == 0,
                "The grace-period flow wrote schema commands: " + string.Join(", ", writesAfterFlow));
            Assert.Equal(columnsAfterSetup, await ColumnFingerprintAsync(connectionString));
            Assert.Equal(typesAfterSetup, await EventTypeTokens.ReadAsync(_database.Database, connectionString));
        }
        finally
        {
            await processor.StopAsync();
            await ordering.StopAsync();
        }
    }

    private static IHost BuildOrdering(EShopDatabase database, string connectionString, string broker)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(Settings(database, connectionString, broker));
        var assembly = typeof(ConfirmGracePeriodReactor).Assembly;
        builder.AddEShopLawnDart(
            EShopDatabases.Ordering,
            context => context.AddOrderingContext(assembly, typeof(OrderStarted).Assembly),
            EShopServiceNames.Ordering);
        builder.AddEShopSlices(assembly);
        return builder.Build();
    }

    private static IHost BuildProcessor(
        EShopDatabase database,
        string connectionString,
        string broker,
        TimeProvider time)
    {
        var builder = Host.CreateApplicationBuilder();
        var settings = Settings(database, connectionString, broker);
        settings[$"{BackgroundTaskOptions.SectionName}:GracePeriodTime"] = "1";
        settings[$"{BackgroundTaskOptions.SectionName}:CheckUpdateTime"] = "30";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton(time);
        builder.AddEShopLawnDart(
            EShopDatabases.Ordering,
            context => context.AddOrderProcessorContext(typeof(OrderStarted).Assembly),
            EShopServiceNames.OrderProcessor,
            enableOutbox: false);
        builder.AddEShopSlices(typeof(TrackSubmittedOrderReactor).Assembly);
        return builder.Build();
    }

    private static Dictionary<string, string?> Settings(EShopDatabase database, string connectionString, string broker) =>
        new()
        {
            [EShopDatabaseSetting.ConfigurationKey] = database.ToString(),
            ["ConnectionStrings:" + EShopDatabases.Ordering] = connectionString,
            ["ConnectionStrings:" + EShopLawnDartHost.EventBusConnectionName] = broker,
            ["Logging:LogLevel:Default"] = "Warning"
        };

    private async Task<string> ColumnFingerprintAsync(string connectionString)
    {
        var snapshot = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);
        var columns = snapshot.Split('\n').Where(line => line.StartsWith("column ", StringComparison.Ordinal));
        return string.Join('\n', columns);
    }

    private static async Task WaitForConsumersAsync(RabbitMqContainer rabbit)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        string last = "";
        while (DateTime.UtcNow < deadline)
        {
            var result = await rabbit.ExecAsync(["rabbitmqctl", "list_queues", "name", "consumers"]);
            last = result.Stdout + result.Stderr;
            if (HasConsumer(last, EShopServiceNames.Ordering) && HasConsumer(last, EShopServiceNames.OrderProcessor))
                return;
            await Task.Delay(250);
        }

        throw new TimeoutException("RabbitMQ consumers did not start. " + last);
    }

    private static bool HasConsumer(string report, string queue)
    {
        foreach (var line in report.Split('\n'))
        {
            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && parts[0] == queue
                && int.TryParse(parts[1], out var consumers)
                && consumers > 0)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _utc;

        public ManualTime(DateTimeOffset utc) => _utc = utc;

        public override DateTimeOffset GetUtcNow() => _utc;

        public void Advance(TimeSpan by) => _utc += by;
    }
}

internal static class EventTypeTokens
{
    public static async Task<string[]> ReadAsync(EShopDatabase database, string connectionString)
    {
        if (database == EShopDatabase.Postgres)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT token FROM lawndart.event_types ORDER BY token";
            return await ReadStringsAsync(command);
        }

        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var sqlCommand = sql.CreateCommand();
        sqlCommand.CommandText = "SELECT Token FROM dbo.EventTypes ORDER BY Token";
        return await ReadStringsAsync(sqlCommand);
    }

    private static async Task<string[]> ReadStringsAsync(System.Data.Common.DbCommand command)
    {
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names.ToArray();
    }
}

internal static class SchemaWriteAudit
{
    public static async Task InstallAndClearAsync(EShopDatabase database, string connectionString)
    {
        if (database == EShopDatabase.Postgres)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS public.ddl_audit (command text NOT NULL);
                CREATE OR REPLACE FUNCTION public.ddl_audit_fn() RETURNS event_trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                  INSERT INTO public.ddl_audit(command) VALUES (tg_tag);
                END;
                $$;
                DROP EVENT TRIGGER IF EXISTS ddl_audit_trg;
                CREATE EVENT TRIGGER ddl_audit_trg ON ddl_command_end
                EXECUTE FUNCTION public.ddl_audit_fn();
                DELETE FROM public.ddl_audit;
                """;
            await command.ExecuteNonQueryAsync();
            return;
        }

        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var sqlCommand = sql.CreateCommand();
        sqlCommand.CommandText =
            """
            IF OBJECT_ID(N'dbo.DdlAudit', N'U') IS NULL
            CREATE TABLE dbo.DdlAudit (Command nvarchar(200) NOT NULL);
            IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = N'trg_ddl_audit' AND parent_class = 0)
            DROP TRIGGER trg_ddl_audit ON DATABASE;
            EXEC(N'CREATE TRIGGER trg_ddl_audit ON DATABASE FOR DDL_DATABASE_LEVEL_EVENTS AS BEGIN SET NOCOUNT ON; INSERT INTO dbo.DdlAudit(Command) SELECT EVENTDATA().value(''(/EVENT_INSTANCE/EventType)[1]'', ''nvarchar(200)''); END');
            DELETE FROM dbo.DdlAudit;
            """;
        await sqlCommand.ExecuteNonQueryAsync();
    }

    public static async Task<string[]> ReadAsync(EShopDatabase database, string connectionString)
    {
        if (database == EShopDatabase.Postgres)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT command FROM public.ddl_audit ORDER BY command";
            return await ReadStringsAsync(command);
        }

        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var sqlCommand = sql.CreateCommand();
        sqlCommand.CommandText = "SELECT Command FROM dbo.DdlAudit ORDER BY Command";
        return await ReadStringsAsync(sqlCommand);
    }

    private static async Task<string[]> ReadStringsAsync(System.Data.Common.DbCommand command)
    {
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names.ToArray();
    }
}
