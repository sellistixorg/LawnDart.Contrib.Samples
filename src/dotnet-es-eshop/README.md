# dotnet-es-eshop

Event-sourced rebuild of [dotnet/eShop](https://github.com/dotnet/eShop) on [LawnDart](https://www.nuget.org/packages/LawnDart), .NET 10, and .NET Aspire.

| Pin | Version |
|---|---|
| LawnDart family | 0.7.0-alpha.5 |
| Aspire (AppHost SDK and hosting) | 13.6.1 |
| .NET SDK | 10.0.401 (`global.json`, roll forward to the latest feature band) |

## Prerequisites

- .NET 10 SDK.
- A container runtime (Docker or Podman). The AppHost starts one database, Redis, and RabbitMQ. SQL Server (`mcr.microsoft.com/mssql/server:2025-latest`) is the default. Postgres (`postgres:15.15`) is the other choice.
- Aspire 13.6.1. `dotnet run --project AppHost` uses the AppHost SDK pin. `aspire run` needs an Aspire CLI on that same version.

## Run

From this folder, start SQL Server on HTTP (Hypertext Transfer Protocol):

```bash
dotnet run --project AppHost --launch-profile http
```

Start Postgres on HTTP:

```bash
dotnet run --project AppHost --launch-profile http-postgres
```

The dashboard is at http://localhost:18848. Sign in as `alice` or `bob` (password `Pass123$`).

`AppHost/Properties/launchSettings.json` defines four profiles.

| Profile | Addresses | Database |
|---|---|---|
| `http` | http://localhost:18848 | SQL Server |
| `http-postgres` | http://localhost:18848 | Postgres |
| `https` | https://localhost:19888 and http://localhost:18848 | SQL Server |
| `https-postgres` | https://localhost:19888 and http://localhost:18848 | Postgres |

`http-postgres` and `https-postgres` set `EShop__Database` to `Postgres`.

`http` and `http-postgres` set `ESHOP_USE_HTTP_ENDPOINTS` to `1`. The AppHost then uses the `http` profile on each service that has one. That address is `http://localhost:0`. The dashboard telemetry endpoint (`DOTNET_DASHBOARD_OTLP_ENDPOINT_URL`, OpenTelemetry Protocol) is http://localhost:18075. The resource service endpoint is http://localhost:19075. These profiles stay on HTTP and skip the development certificate.

`https` and `https-postgres` leave `ESHOP_USE_HTTP_ENDPOINTS` unset, so those services use their `https` profile (`https://localhost:0` and `http://localhost:0`). The telemetry endpoint is https://localhost:18076. The resource service endpoint is https://localhost:19076. Those profiles use HTTPS (Hypertext Transfer Protocol Secure). Trust the development certificate before you use them:

```bash
dotnet dev-certs https --trust
```

A run with no `--launch-profile` uses `https`, the first profile in the file.

Basket, Catalog, Identity, Ordering, PaymentProcessor, WebApp, WebhookClient, and Webhooks each have those two profiles. OrderProcessor has no launch settings file.

## Database

`EShop:Database` chooses the database. `SqlServer` is the default. `Postgres` is the other value. Any other value stops startup and names those two.

```bash
dotnet run --project AppHost -- --database postgres
```

With no `--launch-profile`, that command uses the `https` profile. Pass `--launch-profile http`, or use `http-postgres`, to stay on HTTP.

`EShop__Database=Postgres` sets the same choice. The launch profiles `https-postgres` and `http-postgres` set that variable. `--database` wins when both the setting and the launch option are present.

The AppHost passes the choice to every service. A service started on its own reads the same setting and also defaults to SQL Server.

Each database container keeps its own data volume. Switching from one database to the other does not move data.

The AppHost runs `--setup-schema` once for each database before the services start. That command creates the tables and exits. `orderingdb` has one setup command, and both Ordering and Order Processor wait for it. A service started on its own takes the same argument. Identity's command also creates the login tables. Startup then checks the tables and stops when they are missing. Startup does not create tables. The setup command does not seed shoppers or catalog items.

## Containers left running

Ctrl+C stops the AppHost process. The SQL Server container (`sql`), the Postgres container (`postgres`), and the RabbitMQ container (`eventbus`) keep running. `AppHost.cs` sets `ContainerLifetime.Persistent` on those three. Only the database you chose is started. Redis uses the session lifetime, so that container stops with the AppHost.

`redis`, `eventbus`, `sql`, and `postgres` each call `WithDataVolume()`. Those volumes remain after the containers stop.

The container name is the resource name, a hyphen, and an 8-character hash of the AppHost path. The label `com.microsoft.developer.usvc-dev.name` holds that name. Persistent containers also carry `com.microsoft.developer.usvc-dev.persistent=true`.

Stop the AppHost, then remove the persistent containers:

```bash
for id in $(docker ps -aq --filter label=com.microsoft.developer.usvc-dev.persistent=true); do
  name=$(docker inspect --format '{{index .Config.Labels "com.microsoft.developer.usvc-dev.name"}}' "$id")
  case "$name" in
    sql-*|postgres-*|eventbus-*) docker rm -f "$id" ;;
  esac
done
```

A volume name is `lawndart.samples.eshop.apphost-<10-character path hash>-<resource>-data`. The first part is the AppHost assembly name in lowercase. The resource part is `redis`, `eventbus`, `sql`, or `postgres`. Remove the volumes after the containers are gone:

```bash
docker volume ls -q --filter name=lawndart.samples.eshop.apphost- | while read -r volume; do
  docker volume rm "$volume"
done
```

An empty list removes nothing. Podman takes the same arguments in place of `docker`.

## RabbitMQ management

The AppHost calls `WithManagementPlugin()` on `eventbus`. On the Aspire dashboard, open the `eventbus` resource and follow the `management` endpoint. That page is the RabbitMQ management UI.

Sign in as `guest`. Aspire does not take a user name for this resource, so the user stays `guest`. The password is the generated parameter `eventbus-password`. The dashboard shows that parameter on the `eventbus` resource. The same password is in the `eventbus` connection string.

A message that fails all 5 attempts is dead-lettered. The exchange is `{subscription}.dlx`. The queue is `{subscription}.dlq`. In the UI, open Queues and find that name. Catalog's dead-letter queue is `catalog.dlq`.

Upgrading an existing broker: delete the old `eshop.*` queues, delete `eshop.integration` if that exchange is a topic, and delete the per-event queues and exchanges (`order-paid.catalog`, `order-paid.dlx`, and the rest). Or wipe the RabbitMQ volume and start again. RabbitMQ will not change an existing exchange from topic to direct.

Seeded shoppers:

| User | Password |
|---|---|
| `alice` | `Pass123$` |
| `bob` | `Pass123$` |

The seed also copies the eShop demo card (masked number, security code 123 for alice and 456 for bob). Payments are simulated. Checkout still posts that stand-in card.

After checkout the order stays Submitted for the grace period, then moves on. The grace period is 1 minute (`BackgroundTaskOptions:GracePeriodTime` in `OrderProcessor/appsettings.json`). Order Processor checks every 30 seconds (`CheckUpdateTime`). The first check after that minute moves the order on. Waiting about one minute is expected.

## Services

```
AppHost/              Aspire host: the database, Redis, RabbitMQ, services
ServiceDefaults/      OpenTelemetry, health, service discovery, resilience
Infrastructure/       LawnDart SQL host kit and RabbitMQ transport registration
Shared/               Events shared across services
Identity.API/         Shopper accounts (ASP.NET Identity) and sign-in tokens
Catalog.API/          Items, pictures, stock
Basket.API/           Shopper basket. Views are SQL. Redis view store is deferred
Ordering.API/         Orders
OrderProcessor/       Grace period after an order is placed
PaymentProcessor/     Simulated payment
Webhooks.API/         Scaffold
WebhookClient/        Scaffold
WebApp/               Blazor storefront (interactive server)
tests/                Per-service tests
.slices/index.json    Slice name to folder map
```

LawnDart product stores here are InMemory (tests) and the eShop database. That database is SQL Server unless you pick Postgres. Services that own a database call `AddEShopLawnDart` in `Infrastructure/`. Hosts with an Aspire `eventbus` reference use `LawnDart.Messaging.RabbitMQ`. The AppHost starts Redis. Basket keeps its views in the database.

## Slices

One folder per vertical slice, under `<Service>/Slices/<SliceName>/`. An aggregate used by more than one slice lives in `<Service>.API/Aggregates/<AggregateName>/`. The slice adds a partial file next to its command. It does not edit the shared aggregate core or `Program.cs`.

`.slices/index.json` maps each slice name to its folder. View slices expose `GET /views/{slice-name}` on the owning service. The WebApp calls those routes.

The event model is `docs/models/eshop/` (repository `docs/` folder). The JSON files are a read-only export.

## Tests

From this folder:

```bash
dotnet test
```

That runs every project under `tests/`. Default unit tests do not open a database or start the AppHost. `tests/Database.Tests` does open one. Set `ESHOP_DATABASE` to `SqlServer` or `Postgres`. Set `ESHOP_REQUIRE_DATABASE=1` to fail when that database cannot start. Without `ESHOP_DATABASE`, those tests skip.

`ESHOP_REQUIRE_RABBITMQ=1` makes these tests fail when RabbitMQ is down:

- `Outbox_publish_reaches_two_service_queues`
- `Competing_consumers_on_one_queue_deliver_once`
- `Reactor_inbox_drops_a_replayed_outbox_message`
- `Failed_handler_dead_letters_after_five_attempts`

`Outbox_publisher_delivers_to_in_memory_subscribers` stays in memory and runs with the rest.

Pull requests to `main` and pushes to `main` run `.github/workflows/eshop.yml`. One job restores, builds, and tests `LawnDart.Samples.EShop.sln` on `ubuntu-latest`. The SDK comes from `global.json`. Restore uses `nuget.config` in this folder, which lists nuget.org only. That job starts a RabbitMQ container and sets `ESHOP_REQUIRE_RABBITMQ=1`. A second job runs `tests/Database.Tests` for SQL Server and for Postgres.
