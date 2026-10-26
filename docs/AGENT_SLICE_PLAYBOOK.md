# Agent slice playbook (dotnet-es-eshop)

How to implement a Planned slice once it is Ready for the eShop sample.

## Scope of a Ready slice

1. Look up the slice in `src/dotnet-es-eshop/.slices/index.json`.
2. Implement that slice under the mapped service. Put the command, handler, and slice-specific events in `Slices/<SliceName>/`. Put that slice's aggregate behavior in the same folder (Shared aggregates).
3. Add Given-When-Then tests under the matching `tests/<Service>.Tests/Slices/<SliceName>/` (create the folder if missing).
4. Put cross-service event contracts in `Shared/Events/` only when the Ready slice requires them. Describe a new shared event in its XML comments. Do not add a paragraph to `Shared/Events/README.md`.
5. Do **not** reshape AppHost, ServiceDefaults, or Shared contracts unless the task explicitly asks.
6. Do not edit `Program.cs`, the context hooks, `.slices/index.json`, or the solution file. Reactors and view routes are discovered automatically (Host wiring).

## Shared aggregates

An aggregate used by more than one slice lives in `<Service>.API/Aggregates/<AggregateName>/`.

A slice adds `Slices/<SliceName>/<Aggregate>.<SliceName>.cs` with a partial aggregate part (its `Handle` methods and its own error constants) and a partial state part (`internal void Apply(TEvent)` for each event it appends, and any property only it reads). The file declares the aggregate's namespace. Keep that namespace. Changing it splits the partial class. Do not edit `Aggregates/<Name>/<Name>.cs` or `<Name>State.cs`. Status values are already declared there. Slice-only helpers go in a `file static class`.

Copying the aggregate, its state, or an event with the same event type name makes the event catalog throw "Duplicate event type token".

The slice command, handler, and slice-specific events stay in `Slices/<SliceName>/`.

Before you declare an event or an aggregate, search the service for an existing type with the same name or the same event type name. Reuse that type.

Example: `Ordering.API/Slices/MarkOrderStockConfirmed/Order.MarkOrderStockConfirmed.cs` uses namespace `LawnDart.Samples.EShop.Ordering.API.Aggregates.Order`.

## Infrastructure rules (already scaffolded)

- Consume LawnDart NuGet **0.7.0-alpha.3** (see `Directory.Packages.props`). Do not vendor LawnDart source.
- Durable store: **SQL Server** via Aspire (`UseSqlServer` when wiring a context). InMemory is fine for unit GWT tests.
- Aspire AppHost already provisions SQL Server (`mcr.microsoft.com/mssql/server:2025-latest`), Redis, and RabbitMQ and wires `WithReference` like original eShop.
- The RabbitMQ transport is `LawnDart.Messaging.RabbitMQ`, registered from `AddEShopLawnDart`. Redis `IViewStore` is deferred. Slice work must not invent another transport or view-store adapter.
- Add only published LawnDart packages and the packages this sample already uses.

## Host wiring

Service hosts call `AddEShopLawnDart` and `InitializeEShopLawnDartSqlAsync`. RabbitMQ topology setup runs when the host starts (`SetupTopology`). Leave the Aspire graph as it is.

`WithEventTypes` and `WithCommandHandlers` scan an assembly. `AddLawnDartHttpCommands` does too. Each service calls each of those once, from that service's `Program.cs`, or from the context hook `Program.cs` calls. Pass the service assembly. Every slice in that assembly is included.

The context hooks live in `Infrastructure/Contexts/`. That project does not reference the service projects, so a hook cannot name slice types. The service passes its assembly in. Catalog.API does this with one `AddCatalogContext(catalogAssembly)` call inside `AddEShopLawnDart`, then one `AddLawnDartHttpCommands(catalogAssembly)` call. `catalogAssembly` is the Catalog.API assembly.

A slice does not call `WithEventTypes`, `WithCommandHandlers`, `WithProjections`, or `AddLawnDartHttpCommands`. `AddCatalogContext(Assembly)` throws when the catalog is already set.

`MapLawnDartCommands` stays once in `Program.cs`. Catalog.API maps `POST /api/catalog/define-catalog-brand` through that call.

The service hooks already call `WithProjections` after `WithEventTypes`. Ordering and Payment do this before any view projection exists. When a family has historical types, `WithUpcasters` belongs in that same hook, after `WithEventTypes`. A slice does not add that call.

Reactors and event processors are found by `AddEShopSlices(serviceAssembly)`. The hosted service opens one scope per message and resolves the reactor or processor in that scope. A slice does not edit `Program.cs`. A slice that needs other services or a view route adds an `IEShopSliceModule` class in its own folder. The module's `MapEndpoints` is the only extra route a slice may add, and that route is `GET /views/{slice-name}`. Commands still use the one `AddLawnDartHttpCommands` scan per service.

A reactor registration test calls `AddEShopReactors(serviceAssembly)` and asserts one hosted service for its own reactor and event.

The payment gateway contract lives in `PaymentProcessor/Gateway`. Charge Order Payment registers the implementation. A slice does not add another gateway interface. PaymentProcessor maps view routes. It does not map HTTP command routes.

## Models

The model JSON under `docs/models/eshop/` is the source of truth for commands, events, and swimlanes.

## XML documentation

Every public type and public member a slice adds gets an XML doc comment. Cover the command, event, aggregate or entity, state, handler, read model or view, error constants, and nested records. Write a `<summary>`. Add `<param>` and `<returns>` when the member has parameters or a return value. Put a field description on that field: `<param>` for a constructor or method parameter, `<summary>` for a property.

Use the model description when the model has one for that element or field. Read it from the model slice detail, including its shared context items, or from `docs/models/eshop/*.json`. Use that text verbatim, or edit it lightly for grammar and to fit a code comment. Keep the modeler's terms. Do not replace a model description with different wording.

When the model has no description, write a short accurate comment from what the code does.

Put each business rule and error message from the model on the member that enforces it. Use `<remarks>` on that member, or the `<summary>` of the guard or error constant.

Comments describe behavior, not the HTTP route. A command comment may add one short route note.

## Maintenance and command slices

Maintenance and command slices are API only. The command endpoint is the HTTP surface. Do not add HTML pages or admin forms in a service. Admin UI is a later WebApp admin area.

View slices keep `GET /views/{kebab-case-slice-name}` as written under WebApp screens.

## WebApp screens

Storefront screens follow `.cursor/rules/eshop-webapp.mdc`.

One page component per view, under `WebApp/Components/Pages/`. The owning service exposes the view at `GET /views/{kebab-case-slice-name}` and returns `{SliceName}View`. WebApp calls that route with the Aspire HTTP client (`catalog`, `basket`, or `ordering`). Replacing the in-memory placeholder with that call is part of the view slice's definition of done.

## Honesty

README and commits must not claim Redis as a LawnDart product store. RabbitMQ in this sample is `LawnDart.Messaging.RabbitMQ`. Redis `IViewStore` is deferred.

## Open questions

Add an Open questions section to SLICE.md only for a choice about how this slice is built that the model, eShop, and this playbook do not answer, and that a person must decide. Do not list model drift, eShop differences you already handled, infrastructure behavior, wiring that the assembly scan already provides, or questions copied from the board. State what the code does in the body instead. Do not copy model question text into SLICE.md.
