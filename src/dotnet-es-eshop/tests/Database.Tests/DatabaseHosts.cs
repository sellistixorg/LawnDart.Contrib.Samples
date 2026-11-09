using LawnDart.Samples.EShop.Basket.API.Slices.AddItemToBasket;
using LawnDart.Samples.EShop.Catalog.API.Slices.DefineCatalogBrand;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Seed;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Slices.WhenPaymentSucceeded;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.WhenOrderStatusStockConfirmed;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Database.Tests;

internal static class DatabaseHosts
{
    public static IHost Catalog(EShopDatabase database, string connectionString) =>
        Build(database, EShopDatabases.Catalog, connectionString, context =>
            context.AddCatalogContext(
                typeof(DefineCatalogBrandHandler).Assembly,
                typeof(OrderStockConfirmed).Assembly));

    public static IHost Basket(EShopDatabase database, string connectionString) =>
        Build(database, EShopDatabases.Basket, connectionString, context =>
            context.AddBasketContext(typeof(AddItemToBasketHandler).Assembly));

    public static IHost Ordering(EShopDatabase database, string connectionString) =>
        Build(database, EShopDatabases.Ordering, connectionString, context =>
            context.AddOrderingContext(
                typeof(WhenPaymentSucceededReactor).Assembly,
                typeof(OrderStarted).Assembly));

    public static IHost OrderProcessor(EShopDatabase database, string connectionString) =>
        Build(
            database,
            EShopDatabases.Ordering,
            connectionString,
            context => context.AddOrderProcessorContext(typeof(OrderStarted).Assembly),
            enableOutbox: false);

    public static IHost Payment(EShopDatabase database, string connectionString) =>
        Build(database, EShopDatabases.Payment, connectionString, context =>
            context.AddPaymentContext(
                typeof(WhenOrderStatusStockConfirmedReactor).Assembly,
                typeof(OrderStarted).Assembly));

    public static IHost Webhooks(EShopDatabase database, string connectionString) =>
        Build(database, EShopDatabases.Webhooks, connectionString, context => context.AddWebhooksContext());

    public static IHost IdentityEvents(EShopDatabase database, string connectionString) =>
        Build(database, EShopDatabases.Identity, connectionString, context =>
            context.AddIdentityContext(typeof(IdentitySeedStartup).Assembly));

    public static IHost IdentityLogin(EShopDatabase database, string loginConnectionString)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(Settings(
            database,
            (EShopDatabases.IdentityLogin, loginConnectionString)));
        builder.AddShopperLoginStore();
        builder.Services.AddShopperAccessTokens();
        return builder.Build();
    }

    private static IHost Build(
        EShopDatabase database,
        string connectionName,
        string connectionString,
        Action<LawnDart.EventStore.BoundedContextBuilder> configure,
        bool enableOutbox = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(Settings(database, (connectionName, connectionString)));
        builder.AddEShopLawnDart(connectionName, configure, subscriptionClient: null, enableOutbox: enableOutbox);
        return builder.Build();
    }

    private static Dictionary<string, string?> Settings(
        EShopDatabase database,
        params (string Name, string ConnectionString)[] connections)
    {
        var values = new Dictionary<string, string?>
        {
            [EShopDatabaseSetting.ConfigurationKey] = database.ToString(),
            ["Logging:LogLevel:Default"] = "Warning"
        };
        foreach (var (name, connectionString) in connections)
            values["ConnectionStrings:" + name] = connectionString;
        return values;
    }
}
