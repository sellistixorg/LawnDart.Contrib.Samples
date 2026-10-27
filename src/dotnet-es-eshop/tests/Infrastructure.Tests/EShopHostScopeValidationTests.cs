using System.Reflection;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.AspNetCore;
using LawnDart.EventSourcing.Postgres.EventStore;
using LawnDart.EventSourcing.SqlServer;
using LawnDart.EventSourcing.SqlServer.EventStore;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Patterns.Reaction;
using LawnDart.Samples.EShop.Basket.API.Slices.ClearBasketWhenOrderStarted;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Catalog.API.Seed;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Seed;
using LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Ordering.API;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentSucceeded;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.WhenOrderStatusStockConfirmed;
using LawnDart.Samples.EShop.Shared.Events;
using LawnDart.Samples.EShop.WebApp.Placeholders;
using LawnDart.Samples.EShop.WebApp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopHostScopeValidationTests
{
    private const string SqlConnectionString =
        "Server=localhost;Database=eshop-wave1;User Id=sa;Password=placeholder;TrustServerCertificate=True";

    private const string PostgresConnectionString =
        "Host=localhost;Database=eshop-wave1;Username=postgres;Password=placeholder";

    private const string RabbitConnectionString = "amqp://guest:guest@localhost:5672";

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Basket_api_resolves_hosted_services(EShopDatabase database) =>
        AssertHostedServices(BuildBasket(database), database);

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Ordering_api_resolves_hosted_services(EShopDatabase database) =>
        AssertHostedServices(BuildOrdering(database), database);

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Catalog_api_resolves_hosted_services(EShopDatabase database) =>
        AssertHostedServices(BuildCatalog(database), database);

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Identity_api_resolves_hosted_services(EShopDatabase database) =>
        AssertHostedServices(BuildIdentity(database), database);

    [Theory]
    [InlineData(EShopDatabase.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData(EShopDatabase.Postgres, "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Identity_api_login_store_uses_the_matching_provider(EShopDatabase database, string providerName)
    {
        using var host = BuildIdentity(database);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(providerName, db.Database.ProviderName);
    }

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Payment_processor_resolves_hosted_services(EShopDatabase database) =>
        AssertHostedServices(BuildPaymentProcessor(database), database);

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Order_processor_resolves_hosted_services(EShopDatabase database) =>
        AssertHostedServices(BuildOrderProcessor(database), database);

    [Theory]
    [InlineData(EShopDatabase.SqlServer)]
    [InlineData(EShopDatabase.Postgres)]
    public void Webhooks_api_resolves_hosted_services(EShopDatabase database) =>
        AssertHostedServices(BuildWebhooks(database), database);

    [Fact]
    public void Web_app_resolves_hosted_services() =>
        AssertHostedServices(BuildWebApp(), requireAny: false);

    [Fact]
    public void Webhook_client_resolves_hosted_services() =>
        AssertHostedServices(BuildWebhookClient(), requireAny: false);

    private static void AssertHostedServices(IHost host, bool requireAny = true)
    {
        using (host)
        {
            var hosted = host.Services.GetServices<IHostedService>().ToArray();
            if (requireAny)
                Assert.NotEmpty(hosted);
        }
    }

    private static void AssertHostedServices(IHost host, EShopDatabase database)
    {
        using (host)
        {
            Assert.NotEmpty(host.Services.GetServices<IHostedService>().ToArray());

            var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);
            if (database == EShopDatabase.Postgres)
                Assert.IsType<PostgresEventStore>(store);
            else
                Assert.IsType<SqlServerEventStore>(store);
        }
    }

    private static WebApplication BuildBasket(EShopDatabase database)
    {
        var basketAssembly = typeof(ClearBasketWhenOrderStartedReactor).Assembly;
        var builder = CreateWeb(database, EShopDatabases.Basket);
        builder.AddServiceDefaults();
        builder.AddEShopLawnDart(
            EShopDatabases.Basket,
            context => context.AddBasketContext(basketAssembly),
            EShopServiceNames.Basket);
        builder.Services.AddEShopIdentityUrl(builder.Configuration);
        builder.Services.AddShopperBearerAccess(builder.Configuration);
        builder.Services.AddLawnDartHttpCommands(basketAssembly);
        builder.AddEShopSlices(basketAssembly);
        PreferInMemoryTransport(builder.Services);
        return builder.Build();
    }

    private static WebApplication BuildOrdering(EShopDatabase database)
    {
        var orderingAssembly = typeof(WhenPaymentSucceededReactor).Assembly;
        var builder = CreateWeb(database, EShopDatabases.Ordering);
        builder.AddServiceDefaults();
        builder.AddEShopLawnDart(
            EShopDatabases.Ordering,
            context => context.AddOrderingContext(orderingAssembly, typeof(OrderStarted).Assembly),
            EShopServiceNames.Ordering);
        builder.Services.AddEShopIdentityUrl(builder.Configuration);
        builder.Services.AddShopperBearerAccess(builder.Configuration);
        builder.Services.AddScoped<IShopperOrderBuyerLookup, ShopperOrderBuyerLookup>();
        builder.Services.AddLawnDartHttpCommands(orderingAssembly);
        builder.AddEShopSlices(orderingAssembly);
        PreferInMemoryTransport(builder.Services);
        return builder.Build();
    }

    private static WebApplication BuildCatalog(EShopDatabase database)
    {
        var catalogAssembly = typeof(CatalogSeedStartup).Assembly;
        var builder = CreateWeb(database, EShopDatabases.Catalog);
        builder.AddServiceDefaults();
        builder.AddEShopLawnDart(
            EShopDatabases.Catalog,
            context => context.AddCatalogContext(catalogAssembly, typeof(OrderStockConfirmed).Assembly),
            EShopServiceNames.Catalog);
        builder.Services.AddLawnDartHttpCommands(catalogAssembly);
        builder.AddEShopSlices(catalogAssembly);
        builder.AddCatalogSeed();
        PreferInMemoryTransport(builder.Services);
        return builder.Build();
    }

    private static WebApplication BuildIdentity(EShopDatabase database)
    {
        var identityAssembly = typeof(IdentitySeedStartup).Assembly;
        var builder = CreateWeb(database, EShopDatabases.Identity, EShopDatabases.IdentityLogin);
        builder.AddServiceDefaults();
        builder.AddShopperLoginStore();
        builder.AddEShopLawnDart(
            EShopDatabases.Identity,
            context => context.AddIdentityContext(identityAssembly));
        builder.Services.AddIdentityDemoAuth(builder.Configuration);
        builder.Services.AddLawnDartHttpCommands(identityAssembly);
        builder.Services.AddShopperAccessTokens();
        builder.AddEShopSlices(identityAssembly);
        builder.AddIdentitySeed();
        return builder.Build();
    }

    private static WebApplication BuildPaymentProcessor(EShopDatabase database)
    {
        var paymentAssembly = typeof(WhenOrderStatusStockConfirmedReactor).Assembly;
        var builder = CreateWeb(database, EShopDatabases.Payment);
        builder.AddServiceDefaults();
        builder.AddEShopLawnDart(
            EShopDatabases.Payment,
            context => context.AddPaymentContext(paymentAssembly, typeof(OrderStarted).Assembly),
            EShopServiceNames.PaymentProcessor);
        builder.Services.AddEShopAutomationShell(EShopServiceNames.PaymentProcessor);
        builder.AddEShopSlices(paymentAssembly);
        PreferInMemoryTransport(builder.Services);
        return builder.Build();
    }

    private static IHost BuildOrderProcessor(EShopDatabase database)
    {
        var processorAssembly = typeof(TrackSubmittedOrderReactor).Assembly;
        var builder = CreateWorker(database, EShopDatabases.Ordering);
        builder.AddServiceDefaults();
        builder.AddEShopLawnDart(
            EShopDatabases.Ordering,
            context => context.AddOrderProcessorContext(),
            EShopServiceNames.OrderProcessor,
            enableOutbox: false);
        builder.Services.AddEShopAutomationShell(EShopServiceNames.OrderProcessor);
        builder.AddEShopSlices(processorAssembly);
        PreferInMemoryTransport(builder.Services);
        return builder.Build();
    }

    private static WebApplication BuildWebhooks(EShopDatabase database)
    {
        var builder = CreateWeb(database, EShopDatabases.Webhooks);
        builder.AddServiceDefaults();
        builder.AddEShopLawnDart(
            EShopDatabases.Webhooks,
            context => context.AddWebhooksContext(),
            EShopServiceNames.Webhooks);
        builder.Services.AddEShopIdentityUrl(builder.Configuration);
        PreferInMemoryTransport(builder.Services);
        return builder.Build();
    }

    private static WebApplication BuildWebApp()
    {
        var builder = CreateWeb(EShopDatabase.SqlServer);
        builder.AddServiceDefaults();
        builder.Services.AddEShopStorefront(builder.Configuration);
        builder.Services.AddShopperAuthentication();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddEShopWebAppPlaceholders();
        builder.AddOrderStatusNotifications();
        PreferInMemoryTransport(builder.Services);
        return builder.Build();
    }

    private static WebApplication BuildWebhookClient()
    {
        var builder = CreateWeb(EShopDatabase.SqlServer);
        builder.AddServiceDefaults();
        return builder.Build();
    }

    private static WebApplicationBuilder CreateWeb(EShopDatabase database, params string[] databaseNames)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            Args = []
        });
        builder.Configuration.AddInMemoryCollection(Connections(database, databaseNames));
        builder.Host.UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        return builder;
    }

    private static HostApplicationBuilder CreateWorker(EShopDatabase database, params string[] databaseNames)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development,
            Args = []
        });
        builder.Configuration.AddInMemoryCollection(Connections(database, databaseNames));
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        }));
        return builder;
    }

    private static Dictionary<string, string?> Connections(EShopDatabase database, params string[] databaseNames)
    {
        var values = new Dictionary<string, string?>
        {
            [EShopDatabaseSetting.ConfigurationKey] = database.ToString(),
            [$"ConnectionStrings:{EShopLawnDartHost.EventBusConnectionName}"] = RabbitConnectionString
        };
        var connectionString = database == EShopDatabase.Postgres
            ? PostgresConnectionString
            : SqlConnectionString;
        foreach (var name in databaseNames)
            values[$"ConnectionStrings:{name}"] = connectionString;
        return values;
    }

    private static void PreferInMemoryTransport(IServiceCollection services)
    {
        services.AddSingleton<IMessageTransport>(sp =>
            new InMemoryMessageTransport(sp.GetRequiredService<ILogger<InMemoryMessageTransport>>()));
    }
}

public class EShopConsumerInboxTests
{
    [Fact]
    public void Reactor_inbox_key_matches_lawndart()
    {
        var messageId = "order-started-1";
        Assert.Equal(
            LawnDartKey("ForReactor", typeof(ClearBasketWhenOrderStartedReactor), messageId),
            EShopConsumerInbox.ForReactor(typeof(ClearBasketWhenOrderStartedReactor), messageId));

        var longId = new string('m', 300);
        Assert.Equal(
            LawnDartKey("ForReactor", typeof(WhenPaymentSucceededReactor), longId),
            EShopConsumerInbox.ForReactor(typeof(WhenPaymentSucceededReactor), longId));
    }

    [Fact]
    public void Processor_inbox_key_matches_lawndart()
    {
        var messageId = "processor-1";
        Assert.Equal(
            LawnDartKey("ForProcessor", typeof(ScopeProbeReactor), messageId),
            EShopConsumerInbox.ForProcessor(typeof(ScopeProbeReactor), messageId));
    }

    private static string LawnDartKey(string method, Type consumer, string messageId)
    {
        var inbox = typeof(MessagingExtensions).Assembly
            .GetType("LawnDart.Messaging.Hosting.InboxConsumerKey", throwOnError: true);
        var found = inbox!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(found);
        return Assert.IsType<string>(found.Invoke(null, [consumer, messageId]));
    }
}

public class EShopMessageScopeTests
{
    private const string SqlConnectionString =
        "Server=localhost;Database=eshop-wave1;User Id=sa;Password=placeholder;TrustServerCertificate=True";

    [Fact]
    public async Task Reactor_is_resolved_inside_a_scope()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        }));
        builder.Logging.ClearProviders();

        var services = builder.Services;
        services.AddLawnDart(options => options.RequireTenantId = false);
        services.AddInMemoryMessaging();
        services.AddSingleton<ScopeProbe>();
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.WithEventTypes(typeof(ScopeProbeEvent));
        context.UseSqlServer(options =>
        {
            options.ConnectionString = SqlConnectionString;
            options.RequireTenantId = false;
            options.EnableOutbox = false;
            options.SchemaName = EShopLawnDartHost.SchemaName;
        });
        services.AddLawnDartHttpCommands(typeof(ScopeProbeEvent).Assembly);
        services.AddTransient<ScopeProbeReactor>();
        services.AddHostedService<EShopReactorHost<ScopeProbeReactor, ScopeProbeEvent>>();

        using var host = builder.Build();
        _ = host.Services.GetServices<IHostedService>().ToArray();
        await host.StartAsync();
        try
        {
            var transport = (InMemoryMessageTransport)host.Services.GetRequiredService<IMessageTransport>();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (transport.SubscriberCount<ScopeProbeEvent>() == 0)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Scope probe subscription did not start.");
                await Task.Delay(20);
            }

            var happened = new ScopeProbeEvent(Guid.NewGuid(), DateTime.UtcNow);
            await transport.PublishAsync(happened, new MessageContext
            {
                MessageId = happened.Id.ToString("D"),
                CorrelationId = happened.Id.ToString("D"),
                EnqueuedAt = DateTimeOffset.UtcNow
            });

            Assert.True(host.Services.GetRequiredService<ScopeProbe>().SawScope);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}

[EventTypeName("scope-probe")]
public sealed record ScopeProbeEvent(Guid Id, DateTime Timestamp) : IEvent;

public sealed class ScopeProbe
{
    public bool SawScope { get; set; }
}

public sealed class ScopeProbeReactor : IReactor<ScopeProbeEvent>
{
    public ScopeProbeReactor(IAggregateRepository repository, IServiceProvider provider, ScopeProbe probe)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(probe);
        probe.SawScope = provider is IServiceScope;
    }

    public Task<IEnumerable<ICommand>> ReactAsync(
        ScopeProbeEvent @event,
        MessageContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<ICommand>>([]);
}
