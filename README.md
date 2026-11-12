# LawnDart.Contrib.Samples

Samples that use [LawnDart](https://www.nuget.org/packages/LawnDart). This repository is MIT licensed. See `LICENSE`.

The sample under `src/dotnet-es-eshop/` is an event-sourced rebuild of [dotnet/eShop](https://github.com/dotnet/eShop) on LawnDart, .NET 10, and .NET Aspire. LawnDart packages are pinned to `0.7.0-alpha.5`.

## Book code

`src/facts-before-features/` is the code for Facts Before Features, set in Harbor Street Books. You need the .NET 10 SDK. Those samples keep state in memory. The guide is [`src/facts-before-features/README.md`](src/facts-before-features/README.md).

## Prerequisites

- .NET 10 SDK. Run from `src/dotnet-es-eshop` so `global.json` applies. It pins `10.0.401` and rolls forward to the latest feature band.
- A container runtime (Docker or Podman) for the database, Redis, and RabbitMQ.
- Aspire 13.6.5. Run from `src/dotnet-es-eshop` so `aspire.config.json` applies. `dotnet run --project AppHost` uses the AppHost SDK pin.

## Run the AppHost

From `src/dotnet-es-eshop`, start SQL Server on HTTP (Hypertext Transfer Protocol):

```bash
cd src/dotnet-es-eshop
dotnet run --project AppHost --launch-profile http
```

Start Postgres on HTTP:

```bash
dotnet run --project AppHost --launch-profile http-postgres
```

The dashboard is at http://localhost:18848.

`AppHost/Properties/launchSettings.json` defines four profiles.

| Profile | Addresses | Database |
|---|---|---|
| `http` | http://localhost:18848 | SQL Server |
| `http-postgres` | http://localhost:18848 | Postgres |
| `https` | https://localhost:19888 and http://localhost:18848 | SQL Server |
| `https-postgres` | https://localhost:19888 and http://localhost:18848 | Postgres |

`http-postgres` and `https-postgres` set `EShop__Database` to `Postgres`.

`http` and `http-postgres` set `ESHOP_USE_HTTP_ENDPOINTS` to `1`. The AppHost then uses the `http` profile on each service that has one. That address is `http://localhost:0`. The dashboard telemetry endpoint (`DOTNET_DASHBOARD_OTLP_ENDPOINT_URL`, OpenTelemetry Protocol) is http://localhost:18075. The resource service endpoint is http://localhost:19075. These profiles stay on HTTP and skip the development certificate. They also set `ASPIRE_ALLOW_UNSECURED_TRANSPORT` to `true`. Aspire refuses a dashboard address that is plain HTTP unless that variable is true. `aspire run --launch-profile http` and `aspire run --launch-profile http-postgres` use the same profiles.

`https` and `https-postgres` leave `ESHOP_USE_HTTP_ENDPOINTS` unset, so those services use their `https` profile (`https://localhost:0` and `http://localhost:0`). The telemetry endpoint is https://localhost:18076. The resource service endpoint is https://localhost:19076. Those profiles use HTTPS (Hypertext Transfer Protocol Secure). Trust the development certificate before you use them:

```bash
dotnet dev-certs https --trust
```

A run with no `--launch-profile` uses `https`, the first profile in the file.

Basket, Catalog, Identity, Ordering, PaymentProcessor, WebApp, WebhookClient, and Webhooks each have those two profiles. OrderProcessor has no launch settings file.

The AppHost starts one database, Redis, and RabbitMQ, then the services.

## Database

`EShop:Database` chooses the database. `SqlServer` is the default. `Postgres` is the other value. Any other value stops startup and names those two.

Pick Postgres in any of these ways:

```bash
dotnet run --project AppHost -- --database postgres
```

```bash
EShop__Database=Postgres dotnet run --project AppHost
```

With no `--launch-profile`, those commands use the `https` profile. Pass `--launch-profile http` on the first command, or use `http-postgres`, to stay on HTTP.

The AppHost launch profiles `https-postgres` and `http-postgres` set `EShop__Database` to `Postgres`. `--database` sets the same value as `EShop:Database` and wins when both are present.

The AppHost passes that choice to every service as `EShop__Database`. A service started on its own reads the same setting and also defaults to SQL Server.

SQL Server uses the image `mcr.microsoft.com/mssql/server:2025-latest`. Postgres uses the image `postgres:15.15`. Each container keeps its own data volume. Switching from one database to the other does not move data.

The AppHost runs a setup command once for each database before the services start. That command creates the tables and exits. `orderingdb` has one setup command. Ordering and Order Processor both wait for it. A service started on its own uses the same command:

```bash
dotnet run --project Catalog.API -- --setup-schema
```

Identity's setup command also creates the login tables. After that, startup checks the tables and stops when they are missing. Startup does not create tables. The setup command does not seed shoppers or catalog items.

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

On the Aspire dashboard, open the `eventbus` resource and follow the `management` endpoint. Sign in as `guest`. The password is the generated `eventbus-password` parameter on that resource. The same password is in the `eventbus` connection string.

Dead-lettered messages use the exchange `{subscription}.dlx` and the queue `{subscription}.dlq`. Open Queues in the management UI and find that queue. Catalog's dead-letter queue is `catalog.dlq`.

Upgrading an existing broker: delete the old `eshop.*` queues, delete `eshop.integration` if that exchange is a topic, and delete the per-event queues and exchanges. Or wipe the RabbitMQ volume and start again.

Seeded shoppers are `alice` and `bob`. The password is `Pass123$`. Those users, and the masked demo card, come from the eShop seed. Payments are simulated. Checkout still posts that stand-in card.

After checkout the order stays Submitted for the grace period, then moves on. The grace period is 1 minute (`BackgroundTaskOptions:GracePeriodTime` in `OrderProcessor/appsettings.json`). Order Processor checks every 30 seconds (`CheckUpdateTime`). The first check after that minute moves the order on. Waiting about one minute is expected.

## Services

| Project | Role |
|---|---|
| `AppHost` | Aspire host for the database, Redis, RabbitMQ, and the services |
| `Identity.API` | Shopper accounts on ASP.NET Identity. Sign-in returns an access token |
| `Catalog.API` | Catalog items, pictures, and stock |
| `Basket.API` | Shopper basket. Views are SQL |
| `Ordering.API` | Orders |
| `OrderProcessor` | Grace period after an order is placed |
| `PaymentProcessor` | Simulated payment |
| `WebApp` | Blazor storefront |
| `Webhooks.API`, `WebhookClient` | Scaffolds |
| `Infrastructure` | LawnDart SQL host kit and RabbitMQ transport registration |
| `Shared` | Events shared across services |

LawnDart product stores in this sample are InMemory (tests) and the eShop database. That database is SQL Server unless you pick Postgres, as described above. Hosts with an Aspire `eventbus` reference use `LawnDart.Messaging.RabbitMQ`. The AppHost starts Redis. Basket keeps its views in the database.

## Slices

Each service keeps one folder per vertical slice under `Slices/`. An aggregate used by more than one slice lives in `<Service>.API/Aggregates/<AggregateName>/`. `src/dotnet-es-eshop/.slices/index.json` maps slice names to folders. A view is `GET /views/{slice-name}` on the owning service.

The event model is the JSON under `docs/models/eshop/`. Those files are a read-only export. The plan next to them is `Dotnet_LawnDart_eShop_Model_Plan.md`.

Hosting detail is in `docs/ESHOP_INFRA_PLAN.md`. How to run tests, and a closer look at the services, is in `src/dotnet-es-eshop/README.md`.

## Tests

```bash
dotnet test src/dotnet-es-eshop/LawnDart.Samples.EShop.sln
```

Default unit tests do not need Docker. With `ESHOP_REQUIRE_RABBITMQ=1`, the broker tests connect to RabbitMQ and fail when it is down. `.github/workflows/eshop.yml` starts RabbitMQ for that run.

`tests/Database.Tests` opens SQL Server or Postgres. Set `ESHOP_DATABASE` to `SqlServer` or `Postgres`. Set `ESHOP_REQUIRE_DATABASE=1` to fail when that database cannot start. Without `ESHOP_DATABASE`, those tests skip. The workflow runs them on every pull request, once for each database.

## Caveats

This sample is a demo. Payments are simulated. `OrderPlaced` on the bus carries the ship-to address, the cardholder name, the last four digits, and the expiry. The Payment Attempt Status view is anonymous. Shopper tokens last one hour and have no refresh. `POST /identity/demo-token` is still mapped. It returns a stand-in token and does not authenticate anyone.

## LawnDart

Install the packages from [nuget.org](https://www.nuget.org/packages/LawnDart). Product source is [sellistixorg/LawnDart](https://github.com/sellistixorg/LawnDart), at the tag that matches `0.7.0-alpha.5`.

Storefront files copied from dotnet/eShop are listed in `src/dotnet-es-eshop/WebApp/THIRD-PARTY-NOTICES.md`. Catalog seed pictures are covered by `src/dotnet-es-eshop/Catalog.API/Seed/NOTICE`.
