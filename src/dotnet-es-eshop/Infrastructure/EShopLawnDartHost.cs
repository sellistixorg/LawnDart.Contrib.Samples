using System.Text.RegularExpressions;
using LawnDart;
using LawnDart.EventSourcing.SqlServer;
using LawnDart.EventSourcing.SqlServer.EventStore;
using LawnDart.EventStore;
using LawnDart.Messaging;
using LawnDart.Messaging.InMemory;
using LawnDart.Messaging.Outbox;
using LawnDart.Messaging.RabbitMQ;
using Microsoft.Extensions.Hosting;
using LawnDart.Messaging.SqlServer;
using LawnDart.Outbox;
using LawnDart.Projections.Lightweight;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Shared LawnDart SQL registration for eShop service hosts.
/// </summary>
public static class EShopLawnDartHost
{
    /// <summary>
    /// Bounded context name for a service process. One process, one database, one store.
    /// </summary>
    public const string ContextName = "default";

    /// <summary>
    /// SQL schema for the event log, outbox, views, checkpoints, and inbox.
    /// </summary>
    public const string SchemaName = "dbo";

    /// <summary>
    /// Aspire connection name from <c>AddRabbitMQ("eventbus")</c>.
    /// The value is the broker URI.
    /// </summary>
    public const string EventBusConnectionName = "eventbus";

    /// <summary>
    /// Direct exchange shared by eShop services.
    /// </summary>
    public const string IntegrationExchangeName = "eshop.integration";

    private const string LogCategory = "LawnDart.Samples.EShop.Infrastructure";

    private static readonly Regex SubscriptionNamePattern = new(
        "^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Registers LawnDart against the Aspire SQL connection named <paramref name="connectionName"/>.
    /// </summary>
    /// <param name="subscriptionClient">
    /// RabbitMQ subscription name, for example <see cref="EShopServiceNames.Catalog"/>.
    /// When set, the host uses <c>LawnDart.Messaging.RabbitMQ</c> and the Aspire
    /// <c>eventbus</c> connection. When omitted, the host uses <c>LawnDart.Messaging.InMemory</c>.
    /// </param>
    /// <param name="enableOutbox">
    /// When true, this process polls the SQL outbox and publishes it.
    /// OrderProcessor passes false. It shares <c>orderingdb</c> with Ordering.API, and Ordering.API publishes that outbox.
    /// </param>
    /// <remarks>
    /// <para>
    /// Wires the SQL event log, view store, checkpoints, transactional outbox, and durable inbox.
    /// The outbox publishes through <c>AddMessageTransportOutboxPublisher</c>.
    /// The inbox is <c>AddSqlInboxStore</c> on the consume path.
    /// </para>
    /// <para>
    /// <paramref name="configureContext"/> is the per-service hook.
    /// The service <c>Program.cs</c> uses it for one assembly scan:
    /// <c>WithEventTypes</c> and <c>WithCommandHandlers</c> on that service assembly.
    /// Call <c>WithProjections</c> once in the same hook when the service has projections,
    /// after <c>WithEventTypes</c>.
    /// Projection stores are already registered when the hook runs.
    /// </para>
    /// <para>
    /// Call <see cref="InitializeEShopLawnDartSqlAsync(IServiceProvider, CancellationToken)"/>
    /// after the host is built and before it runs.
    /// </para>
    /// </remarks>
    public static TBuilder AddEShopLawnDart<TBuilder>(
        this TBuilder builder,
        string connectionName,
        Action<BoundedContextBuilder> configureContext,
        string? subscriptionClient = null,
        bool enableOutbox = true)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var connectionString = builder.Configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionName}' is missing. " +
                "Start this service from the Aspire AppHost so the SQL database reference is injected.");
        }

        var services = builder.Services;
        services.AddLawnDart(options => options.RequireTenantId = false);
        RegisterTransport(builder, services, subscriptionClient);
        services.AddSqlInboxStore(connectionString);
        services.AddMessageTransportOutboxPublisher();
        services.AddSqlProjectionStores(ContextName, connectionString, SchemaName);

        var context = services.AddBoundedContext(ContextName);
        configureContext(context);

        // LawnDart resolves the SQL event store and outbox publisher only when a catalog exists.
        if (context.EventCatalog is null)
        {
            context.WithEventTypes(typeof(EShopHostCatalogEvent));
        }

        context.UseSqlServer(options =>
        {
            options.ConnectionString = connectionString;
            options.RequireTenantId = false;
            options.EnableOutbox = enableOutbox;
            options.SchemaName = SchemaName;
        });

        return builder;
    }

    /// <summary>
    /// Creates LawnDart SQL tables for the event log, outbox, views, checkpoints, and inbox.
    /// Idempotent. LawnDart exposes <c>InitializeSchemaAsync</c>, not a versioned migrator.
    /// </summary>
    public static Task InitializeEShopLawnDartSqlAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.Services.InitializeEShopLawnDartSqlAsync(cancellationToken);
    }

    /// <inheritdoc cref="InitializeEShopLawnDartSqlAsync(IHost, CancellationToken)"/>
    public static async Task InitializeEShopLawnDartSqlAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetService<ILoggerFactory>()?.CreateLogger(LogCategory);
        logger?.LogInformation(
            "Initializing LawnDart SQL schema for context {ContextName}.",
            ContextName);

        var store = services.GetRequiredKeyedService<IEventStore>(ContextName);
        if (store is not SqlServerEventStore sql)
        {
            throw new InvalidOperationException(
                $"Context '{ContextName}' resolved {store.GetType().Name}, not SqlServerEventStore. " +
                "Call AddEShopLawnDart before InitializeEShopLawnDartSqlAsync.");
        }

        await sql.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);
        await services.GetRequiredKeyedService<IOutboxWriter>(ContextName)
            .InitializeSchemaAsync(cancellationToken)
            .ConfigureAwait(false);
        await services.InitializeSqlProjectionStoresAsync(ContextName, cancellationToken)
            .ConfigureAwait(false);
        await services.InitializeSqlInboxStoreAsync(cancellationToken).ConfigureAwait(false);

        logger?.LogInformation(
            "LawnDart SQL schema is ready for context {ContextName}.",
            ContextName);
    }

    /// <summary>
    /// Records <paramref name="messageType"/> so RabbitMQ topology setup can declare its queue.
    /// A second record of the same type is ignored.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="messageType">CLR message type. It must declare <c>[EventTypeName]</c>.</param>
    /// <returns>The same <paramref name="services"/>.</returns>
    public static IServiceCollection AddEShopSubscription(this IServiceCollection services, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(messageType);

        var already = services.Any(descriptor =>
            descriptor.ServiceType == typeof(MessageSubscription)
            && descriptor.ImplementationInstance is MessageSubscription subscription
            && subscription.MessageType == messageType);
        if (!already)
            services.AddSingleton(new MessageSubscription(messageType));

        return services;
    }

    /// <summary>
    /// Records <typeparamref name="TMessage"/> so RabbitMQ topology setup can declare its queue.
    /// </summary>
    /// <typeparam name="TMessage">CLR message type. It must declare <c>[EventTypeName]</c>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/>.</returns>
    public static IServiceCollection AddEShopSubscription<TMessage>(this IServiceCollection services)
        where TMessage : class
        => services.AddEShopSubscription(typeof(TMessage));

    private static void RegisterTransport<TBuilder>(
        TBuilder builder,
        IServiceCollection services,
        string? subscriptionClient)
        where TBuilder : IHostApplicationBuilder
    {
        if (string.IsNullOrWhiteSpace(subscriptionClient))
        {
            services.AddInMemoryMessaging();
            return;
        }

        if (!SubscriptionNamePattern.IsMatch(subscriptionClient))
        {
            throw new ArgumentException(
                "Subscription name must be 1 to 64 characters of lowercase letters, digits, and hyphens.",
                nameof(subscriptionClient));
        }

        var eventBus = builder.Configuration.GetConnectionString(EventBusConnectionName);
        if (string.IsNullOrWhiteSpace(eventBus))
        {
            throw new InvalidOperationException(
                $"Connection string '{EventBusConnectionName}' is missing. " +
                "Start this service from the Aspire AppHost so the RabbitMQ reference is injected.");
        }

        services.AddRabbitMqTransport(options =>
        {
            options.Uri = eventBus;
            options.SubscriptionName = subscriptionClient;
            options.TopicMapper = new SharedTopicMapper(IntegrationExchangeName);
            options.SetupTopology = true;
        });
    }

    /// <summary>
    /// Registers the message transport for a subscriber that has no SQL outbox.
    /// </summary>
    /// <param name="builder">The host.</param>
    /// <param name="subscriptionClient">Subscription name, for example <see cref="EShopServiceNames.WebApp"/>.</param>
    /// <remarks>
    /// A present <c>eventbus</c> connection string uses <c>LawnDart.Messaging.RabbitMQ</c>.
    /// A missing connection string uses <c>LawnDart.Messaging.InMemory</c>.
    /// </remarks>
    public static void AddEShopMessageTransport(this IHostApplicationBuilder builder, string subscriptionClient)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionClient);

        var eventBus = builder.Configuration.GetConnectionString(EventBusConnectionName);
        if (string.IsNullOrWhiteSpace(eventBus))
        {
            builder.Services.AddInMemoryMessaging();
            return;
        }

        RegisterTransport(builder, builder.Services, subscriptionClient);
    }
}
