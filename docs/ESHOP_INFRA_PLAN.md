# eShop infrastructure

Hosting design for `src/dotnet-es-eshop`.

`dotnet run --project AppHost` starts Identity, Catalog, Basket, Ordering, OrderProcessor, PaymentProcessor, Webhooks, WebhookClient, and WebApp. Each API that owns a database registers LawnDart against its Aspire SQL Server database. Cross-service messages travel RabbitMQ through `LawnDart.Messaging.RabbitMQ`, with a SQL outbox and a SQL inbox.

`aspire run` needs an Aspire CLI that matches AppHost 13.6.1. `dotnet run --project AppHost` uses the AppHost SDK pin (`AspireUseCliBundle=true`).

## What the host runs

- Aspire AppHost and ServiceDefaults (Aspire 13.6.1).
- SQL Server (`mcr.microsoft.com/mssql/server:2025-latest`), Redis, and RabbitMQ in the AppHost.
- LawnDart packages pinned to `0.7.0-alpha.3`.
- One registration helper, `AddEShopLawnDart`, used by Catalog, Basket, Ordering, Identity, OrderProcessor, PaymentProcessor, and Webhooks.
- SQL schema init on startup. `InitializeEShopLawnDartSchemaAsync` calls the package `InitializeSchemaAsync`. LawnDart has no versioned migrator. Re-runs are idempotent DDL.
- Per-service bounded-context hooks (`AddCatalogContext`, and the rest). The service `Program.cs` calls the hook. An assembly scan runs once there.
- ServiceDefaults health, OpenTelemetry, discovery, and resilience as the outer host shell.

WebApp and WebhookClient do not call the host kit. Neither has a SQL database.

Empty hooks do not call `WithEventTypes`. LawnDart resolves the SQL event store only after a catalog exists. The host registers `EShopHostCatalogEvent` in that case so the process can start and create tables. A hook that calls `WithEventTypes` supplies the catalog.

OrderProcessor shares `orderingdb` with Ordering.API.

## Cross-process bus

Original eShop uses RabbitMQ as the integration bus. This sample uses `LawnDart.Messaging.RabbitMQ` (`AddRabbitMqTransport`).

1. Registration lives in `AddEShopLawnDart` and `AddEShopMessageTransport`.
2. Publisher and consumer hosts register it from the Aspire `eventbus` connection: Basket, Catalog, Ordering, OrderProcessor, PaymentProcessor, and Webhooks. Identity has no eventbus reference and keeps the in-memory transport. WebApp registers the transport with subscription name `webapp` (`AddOrderStatusNotifications` calls `AddEShopMessageTransport(EShopServiceNames.WebApp)`). It subscribes to order status events only and has no SQL outbox or inbox.
3. Publish goes through the SQL outbox. Consume goes through the SQL inbox.
4. Subscription names are `basket`, `catalog`, `ordering`, `order-processor`, `payment-processor`, `webhooks`, and `webapp`. Every event type uses the direct exchange `eshop.integration`. The routing key is the event type name. The queue name is the subscription name. `SetupTopology` is true, so host startup declares that queue and binds each recorded event type. Publish and subscribe still declare the same objects on first use. The default layout is one queue per subscription.
5. A failed delivery waits on `{subscription}.retry` (default 5 seconds) and returns through `{subscription}.delay`, up to 5 attempts. `DeadLetterFailedMessages` is true, so the last failure is dead-lettered. The dead-letter exchange is `{subscription}.dlx`. The dead-letter queue is `{subscription}.dlq`. Catalog's dead-letter queue is `catalog.dlq`.
6. `RabbitMqTransportSmokeTests` covers outbox publish through the package transport to a second subscription, reactor inbox dedupe, competing consumers on one queue, and a handler that fails every attempt until the message is dead-lettered. The test runs when RabbitMQ is reachable (`ESHOP_RABBITMQ`, or `amqp://guest:guest@127.0.0.1:5672`). Registration tests run without a broker. Set `ESHOP_REQUIRE_RABBITMQ=1` to fail that smoke test when the broker is down.

## Redis

Deferred. Basket keeps the SQL view store from the host kit. AppHost still provisions Redis. Redis is not a LawnDart product store.

The open choice is whether Basket stays on SQL views or later uses a Redis view store for the original Redis-as-basket shape. The current choice is SQL views.

## Identity and WebApp

Identity signs a shopper in with ASP.NET Identity and returns an access token. WebApp stores that token in an HttpOnly cookie and sends `Authorization: Bearer` on Basket, Ordering, and Identity calls. The Identity host still maps `POST /identity/demo-token`.

Sign-in returns an access token. Catalog serves item pictures at `GET /api/catalog/items/{id}/pic`. WebApp forwards `GET /product-images/{id}` to that route.

Callback URLs come from the AppHost (`BasketApiClient`, `OrderingApiClient`, `WebhooksApiClient`, `WebhooksWebClient`, `WebAppClient`).

WebApp uses Aspire service-discovery HTTP clients for Catalog, Basket, and Ordering (`https+http://catalog-api`, `basket-api`, `ordering-api`). `IdentityUrl` and `CallBackUrl` come from the AppHost. The storefront is Blazor interactive server. Storefront pages call `GET /views/{slice-name}` on the owning service. Screen rules: `.cursor/rules/eshop-webapp.mdc`.

OrderProcessor runs the grace-period timer and publishes `GracePeriodConfirmed`. PaymentProcessor charges through a simulated gateway. Both hosts still register slice reactors on the context hook.

Webhooks and WebhookClient stay scaffolds. Webhooks binds `Identity:Url` because the AppHost already injects it.

## Slice boundary

A slice adds or changes that slice folder, its tests, and shared event types that slice owns. A slice does not reshape AppHost, ServiceDefaults, or host kit registration.

Model order is Identity, then Catalog, Basket, Ordering, and Payment, plus the OrderProcessor grace period. Webhooks stay later.

## Out of scope for the host

- Changing LawnDart product packages beyond the public `0.7.0-alpha.3` APIs.
- Treating Redis as a LawnDart product store. Postgres is used only through the `EShop:Database` setting on the service hosts. Azure Service Bus is not used here.

## Known gaps

1. The SQL outbox publishes every appended event. `OrderPlaced` goes on the bus with the ship-to address, the cardholder name, the last four digits, and the expiry.
2. A queue binds to its event type when that service starts, or when its subscriber first runs. A message published before that bind is not queued.
3. The inbox row is written after the command returns, not in the same transaction as the command.
4. `GracePeriodConfirmed` is sent straight to the transport. There is no outbox row for it.
5. Four catalog views each read a stored row with their own helper.

## References

- Sample root: `src/dotnet-es-eshop/`
- Event model: `docs/models/eshop/`
- LawnDart packages: [nuget.org/packages/LawnDart](https://www.nuget.org/packages/LawnDart) at `0.7.0-alpha.3`
- Upstream shape: [dotnet/eShop](https://github.com/dotnet/eShop)
- WebApp screen rule: `.cursor/rules/eshop-webapp.mdc`
