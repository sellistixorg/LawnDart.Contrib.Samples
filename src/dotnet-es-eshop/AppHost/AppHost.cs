using LawnDart.Samples.EShop.Infrastructure;

var builder = DistributedApplication.CreateBuilder(args);

// Database choice. The EShop:Database setting picks SqlServer (the default) or Postgres.
// It can come from appsettings.json, the environment variable EShop__Database, or the command line.
// The launch option --database postgres sets the same value and wins over the others.
// The AppHost starts one database container for the choice and passes the same value
// to every service as EShop__Database, so the services use the stores for that database.
builder.Configuration.AddEShopDatabaseLaunchOption(args);
var database = builder.Configuration.ReadEShopDatabase();

// Infrastructure resources.
// The database holds the LawnDart event log, views, checkpoints, outbox, and inbox for each service,
// and the Identity login store.
// SQL Server uses the image mcr.microsoft.com/mssql/server:2025-latest.
// Postgres uses the image postgres:15.15. The LawnDart tables go in schema lawndart.
// Each database container keeps its own data volume. Switching from one to the other does not move data.
// Service hosts read the Aspire connection string in AddEShopLawnDart.
// Publisher and consumer hosts read eventbus as the broker URI for LawnDart.Messaging.RabbitMQ.
// Redis IViewStore is deferred. Basket keeps its views in the database.

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var rabbitMq = builder.AddRabbitMQ("eventbus")
    .WithDataVolume()
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var addDatabase = AddDatabaseServer(builder, database);

// identitydb is the LawnDart event log. identitylogindb is the ASP.NET Core Identity login store.
// The names are the same on SQL Server and Postgres.
var identityDb = addDatabase(EShopDatabases.Identity);
var identityLoginDb = addDatabase(EShopDatabases.IdentityLogin);
var catalogDb = addDatabase(EShopDatabases.Catalog);
var basketDb = addDatabase(EShopDatabases.Basket);
var orderingDb = addDatabase(EShopDatabases.Ordering);
var paymentDb = addDatabase(EShopDatabases.Payment);
var webhooksDb = addDatabase(EShopDatabases.Webhooks);

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

// Service projects. Each API hosts its slices under Slices/.
var identityApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Identity_API>("identity-api")
    .WithExternalHttpEndpoints()
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(identityLoginDb).WaitFor(identityLoginDb)
    .WithHttpHealthCheck("/health");

var identityEndpoint = identityApi.GetEndpoint(launchProfileName);

var basketApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Basket_API>("basket-api")
    .WithReference(redis) // Redis IViewStore is deferred. Basket keeps its views in the database.
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(basketDb).WaitFor(basketDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);
redis.WithParentRelationship(basketApi);

var catalogApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Catalog_API>("catalog-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

var orderingApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Ordering_API>("ordering-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderingDb).WaitFor(orderingDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);

builder.AddProject<Projects.LawnDart_Samples_EShop_OrderProcessor>("order-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderingDb).WaitFor(orderingDb)
    .WaitFor(orderingApi);

builder.AddProject<Projects.LawnDart_Samples_EShop_PaymentProcessor>("payment-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(paymentDb).WaitFor(paymentDb)
    .WithHttpHealthCheck("/health");

var webHooksApi = builder.AddProject<Projects.LawnDart_Samples_EShop_Webhooks_API>("webhooks-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(webhooksDb).WaitFor(webhooksDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);

// Optional: Yarp mobile-bff can be added later to match upstream eShop mobile routes.
// builder.AddYarp("mobile-bff")...

var webhooksClient = builder.AddProject<Projects.LawnDart_Samples_EShop_WebhookClient>("webhooksclient")
    .WithReference(webHooksApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

var webApp = builder.AddProject<Projects.LawnDart_Samples_EShop_WebApp>("webapp")
    .WithExternalHttpEndpoints()
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithReference(rabbitMq).WaitFor(rabbitMq) // order status messages for the orders page
    .WaitFor(identityApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

// Callback URLs (self-referencing), matching original eShop AppHost wiring.
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));
webhooksClient.WithEnvironment("CallBackUrl", webhooksClient.GetEndpoint(launchProfileName));

// Identity callback URLs. The demo auth stand-in reads these. Account slices replace that stand-in later.
identityApi.WithEnvironment("BasketApiClient", basketApi.GetEndpoint("http"))
           .WithEnvironment("OrderingApiClient", orderingApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksApiClient", webHooksApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksWebClient", webhooksClient.GetEndpoint(launchProfileName))
           .WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName));

// Every service gets the same database choice as the container started above.
foreach (var project in builder.Resources.OfType<ProjectResource>())
{
    builder.CreateResourceBuilder(project)
        .WithEnvironment(EShopDatabaseEnvironment.VariableName, database.ToString());
}

builder.Build().Run();

static Func<string, IResourceBuilder<IResourceWithConnectionString>> AddDatabaseServer(
    IDistributedApplicationBuilder builder,
    EShopDatabase database)
{
    if (database == EShopDatabase.Postgres)
    {
        var postgres = builder.AddPostgres(EShopDatabaseEnvironment.PostgresResourceName)
            .WithImageTag(EShopDatabaseEnvironment.PostgresImageTag)
            .WithDataVolume()
            .WithLifetime(ContainerLifetime.Persistent);
        return name => postgres.AddDatabase(name);
    }

    var sql = builder.AddSqlServer(EShopDatabaseEnvironment.SqlServerResourceName)
        .WithImageTag("2025-latest")
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent);
    return name => sql.AddDatabase(name);
}

static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);
    return int.TryParse(envValue, out int result) && result == 1;
}
