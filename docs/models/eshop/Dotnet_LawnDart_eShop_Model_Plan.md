# Dotnet LawnDart eShop model plan

**Workspace name prefix (locked):** `dotnet LawnDart eShop`  
**Reference code:** [dotnet/eShop](https://github.com/dotnet/eShop) at commit `dc7ea49`  
**JSON shape:** structured mode (`sequenceLayout.mode: "structured"`, slice kinds, owned widgets and typed arrays, `connections: []`)  
**Authoring stance:** LawnDart-style **business process** rewrite of eShop. It is not a 1:1 controller map. CRUD-shaped gaps are called out.  
**Plan status:** Complete v1. Mirror events sit on dedicated Inbound Signals holder slices. The Ordering Payment Failed path was added (2026-10-06).
**Last updated:** 2026-10-08 (Catalog picture read and storefront `/product-images/{id}`; Identity seeded demo cards in the description only; BuyerId and OrderTotal required on Stock Confirmed; code checks added; Deduct takes what's left; five decisions written in)

---

## Status-tracked task list

| # | Task | Status | Notes |
|---|------|--------|-------|
| 0 | Read dotnet/eShop at commit `dc7ea49` | **Done** | [dotnet/eShop](https://github.com/dotnet/eShop) |
| 1 | Capture structured JSON rules and the sample shape | **Done** | Structured mode, slice kinds, owned widgets |
| 2 | Write this plan file | **Done** | This document |
| 3 | Identity workspace JSON | **Done** | `Dotnet_LawnDart_eShop_Identity.json`: 6 slices (5 state-change, 1 state-view), structured, validated 2026-10-07 |
| 4 | Catalog workspace JSON (backoffice populate, then shopper browse) | **Done** | `Dotnet_LawnDart_eShop_Catalog.json`: 16 slices (9 state-change incl. 1 mirror holder, 5 state-view, 2 automation), structured, validated 2026-10-07 |
| 5 | Basket workspace JSON | **Done** | `Dotnet_LawnDart_eShop_Basket.json`: 7 slices (5 state-change incl. 1 mirror holder, 1 state-view, 1 automation), structured, validated 2026-10-07 |
| 6 | Ordering workspace JSON | **Done** | `Dotnet_LawnDart_eShop_Ordering.json`: 15 slices (8 state-change incl. 1 mirror holder, 2 state-view, 5 automation); validated 2026-10-07; aligned evt-order-started / awaiting-validation / paid |
| 7 | Payment workspace JSON | **Done** | `Dotnet_LawnDart_eShop_Payment.json`: 8 slices (4 state-change incl. 1 mirror holder, 1 state-view, 1 automation, 2 translation); validated 2026-10-07; emits evt-payment-succeeded/failed; sub stock-confirmed |
| 8 | Cross-workspace automation / translation wiring check | **Done** (ids aligned) | started/awaiting-validation/paid/stock-confirmed/payment-succeeded|failed |
| 9 | Workspace JSON files in this folder | **Done** | Identity, Catalog, Basket, Ordering, Payment |

**Slice totals (from JSON, 2026-10-07):** 52 slices = 31 state-change (4 are mirror holders) + 10 state-view + 9 automation + 2 translation.

**Progress legend:** Planned · In progress · Done · Blocked · Deferred · CRUD-gap

---

## Locked workspace order

1. **Identity**: auth / account **before** shopping  
2. **Catalog**: **backoffice populate FIRST** (API only, no admin pages), then shopper browse  
3. **Basket**  
4. **Ordering**  
5. **Payment**

Every workspace `name` starts with: `dotnet LawnDart eShop ...`

### Proposed workspace filenames

| Order | Workspace display name | File |
|------:|------------------------|------|
| 1 | `dotnet LawnDart eShop Identity` | `Dotnet_LawnDart_eShop_Identity.json` |
| 2 | `dotnet LawnDart eShop Catalog` | `Dotnet_LawnDart_eShop_Catalog.json` |
| 3 | `dotnet LawnDart eShop Basket` | `Dotnet_LawnDart_eShop_Basket.json` |
| 4 | `dotnet LawnDart eShop Ordering` | `Dotnet_LawnDart_eShop_Ordering.json` | 
| 5 | `dotnet LawnDart eShop Payment` | `Dotnet_LawnDart_eShop_Payment.json` |

Optional later (not this wave): Webhooks / OrderProcessor plumbing can stay **inside Ordering** as automation/scheduled slices rather than a sixth workspace.

---

## eShop map (what we reverse-engineered)

| Area | eShop project(s) | Real tech / flows |
|------|------------------|-------------------|
| Identity | `Identity.API` | **Duende IdentityServer** + ASP.NET Identity. Login / Logout / Consent / Grants. Demo users **seeded** (`alice`, `bob`). `RegisterViewModel` exists; **no public Register action** on AccountController: registration is a **demo gap**. Profile fields (card + address) live on `ApplicationUser`. LawnDart build: ASP.NET Identity only, no Duende; Register added; profile is address only (2026-10-08). |
| Catalog | `Catalog.API` | Items CRUD (create/update/delete), list/filter/facets, plain name-prefix search. Brands/types are **read-only lists** in API (seeded): **no brand/type write API** (CRUD gap for LawnDart backoffice). Price change → `ProductPriceChangedIntegrationEvent`. Stock check on `AwaitingValidation`; stock decrement on `Paid`. |
| Basket | `Basket.API` | gRPC: Get / Update (replace whole basket) / Delete. Redis. Clears on `OrderStartedIntegrationEvent`. |
| Ordering | `Ordering.API` + Domain + Infra | CQRS: CreateOrder, CreateOrderDraft, Cancel, Ship + query list/detail. Status: Submitted → AwaitingValidation → StockConfirmed → Paid → Shipped (or Cancelled). Domain + integration events throughout. |
| OrderProcessor | `OrderProcessor` | Background **grace period** → `GracePeriodConfirmedIntegrationEvent` → moves Submitted → AwaitingValidation. LawnDart build publishes `GracePeriodConfirmed`, modeled as Grace Period Confirmed (2026-10-08). |
| Payment | `PaymentProcessor` | Listens `OrderStatusChangedToStockConfirmed`; publishes Succeeded or Failed from config flag (simulated gateway). |

Shopper UI (context only): Blazor `WebApp`: Catalog, Basket, Checkout, Login, Orders. No admin or back-office pages are modeled: maintenance commands are API only (see "API-only maintenance commands" below).

---

## Event Modeling rules (apply to every vertical)

Structured mode only (`sequenceLayout.mode: "structured"`).

| Kind | Completed vertical shape (Ready gate) |
|------|----------------------------------------|
| **state-change** | Trigger (screen **or** process, ≤1) → **1 command** → ≥1 event → ≥1 GWT; optional views/display, errors, questions. Roles/entities are workspace references (no ownership `sliceId`). |
| **state-view** | ≥1 view bound via `view.events` to producer event(s); optional display screen. No owned command/event/GWT/process. |
| **automation** | 1 process with `subscribedEventIds` + `issuesCommandId` targeting another slice's command. |
| **translation** | Inbound external + adapter process + `issuesCommandId`. No `subscribedEventIds`. |

Authoring defaults for Apply: status stays **`Planned`**; stub `x`/`y`; leave `connections: []`. Do not set Ready in generated JSON unless intentionally marking a finished sample. For this eShop model, keep **Planned** until reviewed.

Chronological order inside each workspace = slice `order` ascending (100, 200, 300...).

---

## Workspace / slice inventory (chronological)

### 1) Identity: `dotnet LawnDart eShop Identity`

**File:** `Dotnet_LawnDart_eShop_Identity.json`: **Done** (structured, validated 2026-10-06 CT).

**Roles:** Shopper.  
**Entities:** Shopper Account (AggregateRoot).

| Order | Slice | Kind | Trigger / actors | Command / process | Events | View / GWT | Source in eShop | Status |
|------:|-------|------|------------------|-------------------|--------|------------|-----------------|--------|
| 100 | Register Shopper Account | state-change | Screen: Register | Register Shopper Account | Shopper Account Registered | GWT happy + duplicate email, duplicate user name, invalid user name and weak password errors | eShop only seeds users (`RegisterViewModel` unused); LawnDart builds Register on ASP.NET Identity (login user + `PasswordHasher`), no Duende (2026-10-08) | **Done** |
| 200 | Shopper Account Profile | state-view | none | none | consumes Registered + Profile Updated | Shopper Account Profile + display | ApplicationUser address fields; address only, no card fields (2026-10-08) | **Done** |
| 300 | Sign In Shopper | state-change | Screen: Login | Sign In Shopper | Shopper Signed In | GWT success + bad credentials | ASP.NET Identity password check with lockout; no Duende; Shopper Signed In is the business event (2026-10-08) | **Done** |
| 400 | Sign Out Shopper | state-change | Screen: Logout | Sign Out Shopper | Shopper Signed Out | GWT happy + unknown account error | `AccountController.Logout`; ASP.NET Identity only, no Duende (2026-10-08) | **Done** |
| 500 | Update Shopper Profile | state-change | Screen: Manage Account | Update Shopper Profile | Shopper Profile Updated | GWT happy + unknown account error | Manage views / ApplicationUser address fields | **Done** |
| 600 | Grant Client Consent | state-change | Screen: Consent | Grant Client Consent | Client Consent Granted | GWT happy | Thin client consent; ASP.NET Identity only, no Duende (2026-10-08) | **Deferred** (2026-10-08: not built for now; slice kept, tagged `Deferred`, status unchanged) |

**First slices authored in JSON:** all six above (Register through Consent).

**CRUD / demo gaps (Identity):**
- eShop has no self-service Register endpoint (seeded alice/bob). Resolved: the model and the build add Register on ASP.NET Identity (2026-10-08).
- Password reset / 2FA view models exist; not core shopping path: **Deferred**.
- Card fields stay off Identity (decided 2026-10-08): Shopper Account Profile is address only, and Place Order carries the masked card snapshot.

---

### 2) Catalog: `dotnet LawnDart eShop Catalog`

**File:** `Dotnet_LawnDart_eShop_Catalog.json`: **Done** (structured, validated 2026-10-06 CT). Backoffice → browse → stock reactions. Confirm/Reject collapsed into one Validate command with branching events.

**Roles:** Catalog Admin (backoffice, calls the API directly; no admin screens), Shopper (browse).  
**Entities:** CatalogItem (AR), CatalogBrand, CatalogType; Stock as part of CatalogItem.

#### A. Backoffice populate FIRST (API only, no screens)

| Order | Slice | Kind | Trigger | Command | Events | Notes | Status |
|------:|-------|------|---------|---------|--------|-------|--------|
| 100 | Define Catalog Brand | state-change | API call (no screen) | Define Catalog Brand | Catalog Brand Defined | **CRUD gap**: eShop only seeds + GET `/catalogbrands` | **Done** |
| 200 | Define Catalog Type | state-change | API call (no screen) | Define Catalog Type | Catalog Type Defined | **CRUD gap**: same for types | **Done** |
| 300 | Add Catalog Item | state-change | API call (no screen) | Add Catalog Item | Catalog Item Added | Maps to `POST /api/catalog/items` | **Done** |
| 400 | Catalog Item Detail (admin) | state-view | none | none | consumes Added/Updated/Price Changed/Restocked/Deducted/Removed | Admin read model, served by the API; no screen; keeps removed items with `Removed` set | **Done** |
| 500 | Update Catalog Item | state-change | API call (no screen) | Update Catalog Item | Catalog Item Updated; **Catalog Item Price Changed** (when price differs) | `PUT` + `ProductPriceChangedIntegrationEvent` | **Done** |
| 600 | Remove Catalog Item | state-change | API call (no screen) | Remove Catalog Item | Catalog Item Removed | `DELETE` upstream is a hard delete; LawnDart uses a final soft delete (2026-10-08) | **Done** |
| 700 | Restock Catalog Item | state-change | API call (no screen) | Restock Catalog Item | Catalog Item Restocked | Domain `AddStock`: **no dedicated public API** (CRUD/domain gap) | **Done** |

#### B. Shopper browse (after populate)

| Order | Slice | Kind | Notes | Status |
|------:|-------|------|-------|--------|
| 800 | Browse Catalog | state-view | List + facets + brand/type filters; display Catalog screen | **Done** |
| 900 | View Catalog Item | state-view | Item detail; Catalog `/api/catalog/items/{id}/pic` + WebApp `/product-images/{id}` (2026-10-08) | **Done** |
| 1000 | Search Catalog | state-view | Plain name-prefix search (item names starting with the query text) | **Done** |

#### C. Stock reactions (cross-context; live in Catalog)

| Order | Slice | Kind | Subscribes | Issues | Status |
|------:|-------|------|------------|--------|--------|
| 1100 | Validate Order Stock | automation | Order Awaiting Validation (from Ordering) | Confirm Order Stock **or** Reject Order Stock (two outcomes → prefer two automations or one command with branching events on Catalog side) | **Done** |
| 1110 | Confirm Order Stock | state-change | Process (automation) | Confirm Order Stock → Order Stock Confirmed | Catalog decides has-stock | **Done** |
| 1120 | Reject Order Stock | state-change | Process (automation) | Reject Order Stock → Order Stock Rejected | | **Done** |
| 1200 | Deduct Stock For Paid Order | automation → state-change | Order Paid | Deduct Catalog Stock → Catalog Stock Deducted | Maps to Paid handler `RemoveStock` | **Done** |

**First Catalog slices for JSON:** `100 Define Catalog Brand`, `200 Define Catalog Type`, `300 Add Catalog Item` (backoffice populate path).

**CRUD gaps (Catalog):** Brand/Type writes missing; Restock not exposed as API; eShop Delete is a hard delete (LawnDart: final soft delete, 2026-10-08).

---

### 3) Basket: `dotnet LawnDart eShop Basket`

**File:** `Dotnet_LawnDart_eShop_Basket.json`: **Done** (structured, validated 2026-10-06 CT). Checkout Place Order stays in Ordering; handoff is Clear Basket When Order Started.

**Roles:** Shopper. **Entity:** CustomerBasket (AR keyed by buyer id).

| Order | Slice | Kind | Notes | eShop map | Status |
|------:|-------|------|-------|-----------|--------|
| 100 | Add Item To Basket | state-change | Screen from catalog item | Part of gRPC `UpdateBasket` (replace-all): **model intent**, not dump | **Done** |
| 200 | Change Basket Item Quantity | state-change | | Same UpdateBasket | **Done** |
| 300 | Remove Item From Basket | state-change | | Same | **Done** |
| 400 | Basket Contents | state-view | Display basket | GetBasket | **Done** |
| 500 | Clear Basket | state-change | Explicit clear / checkout prep | DeleteBasket | **Done** |
| 600 | Clear Basket When Order Started | automation | Subscribes Order Started → issues Clear Basket | `OrderStartedIntegrationEventHandler` | **Done** |

**CRUD note:** eShop exposes only full-document update; LawnDart slices are finer-grained business commands (intentional rewrite).

---

### 4) Ordering: `dotnet LawnDart eShop Ordering`

**File:** `Dotnet_LawnDart_eShop_Ordering.json`: **Done** (structured, validated 2026-10-06 CT). Owns `evt-order-started`, `evt-order-awaiting-validation`, `evt-order-paid`. Extra automations for Catalog stock + Payment succeeded forward-ref.

**Roles:** Shopper, Store Operator (ship/cancel).  
**Entities:** Order (AR), Buyer, PaymentMethod, Address (VO), OrderItem.

| Order | Slice | Kind | Notes | Status |
|------:|-------|------|-------|--------|
| 100 | Place Order | state-change | Checkout screen; emits Order Placed / Order Started (basket clear signal) | **Done** |
| 200 | Order Summary | state-view | Shopper order detail / list | **Done** |
| 300 | Confirm Grace Period | automation (scheduled) | OrderProcessor timer publishes Grace Period Confirmed → automation issues Mark Order Awaiting Validation (2026-10-08) | **Done** |
| 400 | Mark Order Awaiting Validation | state-change | Process-triggered | **Done** |
| 500 | Mark Order Stock Confirmed | state-change | From Catalog confirm integration (translation or automation inbound) | **Done** |
| 600 | Mark Order Stock Rejected / Cancel For Stock | state-change | Stock rejected path → Cancelled | **Done** |
| 700 | Mark Order Paid | state-change | From Payment succeeded | **Done** |
| 800 | Cancel Order | state-change | Shopper/operator before Paid/Shipped (shopper screen on order detail; operator uses the API) | **Done** |
| 900 | Ship Order | state-change | Operator, API call only (no admin screen) | **Done** |
| 1000 | Orders By Shopper | state-view | List | **Done** |

Draft order (`CreateOrderDraft`): **Deferred** unless needed for checkout UX (eShop has it; business value thin for LawnDart demo).

Happy path chain: Place Order (Submitted) → Grace Period → Awaiting Validation → Catalog validates → Stock Confirmed → Payment charges → Paid → Ship.

---

### 5) Payment: `dotnet LawnDart eShop Payment`

**File:** `Dotnet_LawnDart_eShop_Payment.json`: **Done** (structured, validated 2026-10-06 CT). Subscribes `evt-order-status-stock-confirmed`; owns `evt-payment-succeeded` / `evt-payment-failed`.

**Roles:** Payment Gateway (external), System.  
**Entity:** Payment Attempt (optional AR): eShop is fire-and-forget simulation.

| Order | Slice | Kind | Notes | Status |
|------:|-------|------|-------|--------|
| 100 | Charge Order Payment | translation or state-change + outbound | Inbound: Stock Confirmed; outbound external Payment Gateway; outcomes Succeeded/Failed | **Done** |
| 200 | Record Payment Succeeded | state-change | Issues / feeds Ordering Mark Order Paid (automation in Ordering or Payment issues command) | **Done** |
| 300 | Record Payment Failed | state-change | Feeds the failed path: Ordering's When Payment Failed issues Cancel Order | **Done** |

eShop `PaymentProcessor` only flips a config boolean: model **business** charge with gateway external; mark simulation as implementation detail.

---

## Cross-workspace event wiring (cheat sheet)

```
Identity: Shopper Signed In
    → (enables) Basket / Checkout screens

Catalog: Catalog Item Added/Updated → Browse/View projections
Catalog: Price Changed → (Basket does not consume it; basket keeps add-time price snapshots, decided 2026-10-08)

Basket: items held until
Ordering: Order Placed / Order Started
    → Basket automation: Clear Basket
    → OrderProcessor: tracks Order Started, publishes Grace Period Confirmed
        → Ordering: Awaiting Validation
            → Catalog: Validate Stock → Confirmed | Rejected
                → Ordering: Stock Confirmed | Cancelled
                    → Payment: Charge
                        → Succeeded → Ordering: Paid → Catalog: Deduct Stock
                        → Failed → Ordering: Cancelled
                            → Operator (API): Ship Order
```

---

## Open questions

1. **Register in Identity:** Author full Register slices even though eShop demo only seeds users? **Recommendation: Yes** (LawnDart rewrite). **Resolved 2026-10-08:** Register and Sign In are built on ASP.NET Identity, no Duende.  
2. **Brand/Type admin:** Invent Define Brand/Type commands despite no write API? **Yes**: mark CRUD-gap; needed before Add Item in a clean model. The commands are API only, with no admin screens (2026-10-07).  
3. **Payment card data on Identity user vs Ordering Buyer/PaymentMethod:** eShop duplicates profile card fields into checkout. Keep Identity profile address/card as "defaults" and Ordering as source of truth for the order? **Resolved 2026-10-08:** the Identity profile is address only, with no card fields. The order keeps the masked card snapshot.  
4. **Price changed → Basket:** eShop publishes `ProductPriceChanged` but Basket does not consume it in this tree. Model optional automation later or skip? **Resolved 2026-10-08:** skip. The basket keeps add-time price snapshots.  
5. **Consent / Duende internals:** Include thin Consent slice or treat OIDC as infrastructure outside Event Modeling? **Deferred 2026-10-08:** Grant Client Consent stays in the model, tagged `Deferred`, and is not built for now.  
6. **Webhooks.API / WebhookClient:** Out of shopping core: omit from this wave?  
7. **Search Catalog scope:** Resolved 2026-10-07 - plain name-prefix search only.  
8. **Soft vs hard delete** for catalog items. **Resolved 2026-10-08:** final soft delete. Re-adding a removed id is rejected.  
9. **Workspace count:** Five workspaces vs merging Payment into Ordering: locked list says five; keep Payment separate.  
10. **Status in exported JSON:** Keep all slices `Planned` for Apply safety, or mark them `Completed` for teaching? **Recommendation: Planned** until the slices are reviewed.

---

## JSON authoring targets

1. **Plan file**: this document (**Done**).
2. **`Dotnet_LawnDart_eShop_Identity.json`**: **Done** (Register → Profile → Sign In → Sign Out → Update Profile → Consent).
   - Roles: Shopper  
   - Slice 100: Register Shopper Account (state-change + screen + command + event + GWT)  
   - Slice 200: Shopper Account Profile (state-view)  
   - Slice 300: Sign In Shopper  
   - Slice 400: Sign Out Shopper  
3. **`Dotnet_LawnDart_eShop_Catalog.json`**: **Done** (Brand → Type → Item → admin/shopper views → stock reactions).

The five workspace JSON files are in this folder.

---

## Conventions for upcoming JSON

- `sequenceLayout`: `{ "mode": "structured", "actorLanes": [], "eventAssignments": {}, "eventSequence": [] }`  
- `connections`: `[]`  
- `groups`: `[]`  
- Owned widgets: set `sliceId` + membership arrays  
- Roles/entities: **no** `sliceId`; link via `roles[]` / `entities[]` / `actorRoleId`  
- Slice `order`: 100-step timeline  
- `boundedContext`: e.g. `Identity`, `Catalog`, `Basket`, `Ordering`, `Payment`  
- Tags: `eShop`, `LawnDart`, area tags (`Backoffice`, `Shopper`, `CRUD-gap`, etc.)  
- No secrets, no real card numbers, no seeded passwords in model text  
- Plain-language names (business English), not controller method names  

---

## LawnDart gap fixes 2026-10-07

Each item was checked against the JSON in this folder and upstream eShop before editing.

| # | Fix | File |
|---|-----|------|
| 1 | Catalog Item Detail (admin), Catalog Browser, and Catalog Item (shopper) views now also read Catalog Stock Deducted. Search results show no stock, so that view is unchanged. (Superseded 2026-10-08: Search now also reads Restocked and Stock Deducted, and its summaries carry available stock.) | Catalog |
| 2 | Charge Order Payment keeps its outbound Payment Gateway (Orchestrate zone). New translation slices: Translate Gateway Succeeded (150) and Translate Gateway Failed (250), each with an inbound gateway external and an adapter that issues Record Payment Succeeded or Record Payment Failed. The inbound externals and old trigger processes were moved off the Record slices. Upstream the gateway is a simulated stub controlled by `PaymentOptions.PaymentSucceeded`. | Payment |
| 3 | Kept one Confirm Grace Period automation. Notes now say the timer runs in the OrderProcessor worker (GracePeriodManagerService) and Mark Order Awaiting Validation is owned by Ordering API. (Superseded 2026-10-08: the automation now subscribes to an explicit Grace Period Confirmed event.) | Ordering |
| 4 | Every "Inbound ... Signals (mirrors)" holder now says: modeling scaffolding only, no code; real delivery is the producing context's integration event over the bus. | Catalog, Basket, Ordering, Payment |
| 5 | Added slice dependencies: Add Item To Basket depends on Sign In Shopper and View Catalog Item; Place Order depends on Basket Contents and Shopper Account Profile. These dependencies point to slices in other workspaces. The structural check does not cover those cross-workspace ids. BuyerId is documented as the Identity AccountId (the "sub" claim). | Basket, Ordering |
| 9 | New state-view Catalog Brands And Types (750) with brand and type names for the filter lists. Browse facet descriptions now point to it. The picture already exists (entity PictureFileName, shopper PictureUrl). | Catalog |
| 13 | Add Catalog Item now has the error Catalog Item Already Exists and the GWT "Duplicate item id". | Catalog |
| 15 | Removed the duplicate evt-order-cancelled from the Order Summary view. | Ordering |

Facts only, no model change: (6) upstream item, brand, and type ids are `int`, while the model uses guid. (10) Upstream search is a name prefix match (`StartsWith`); the model keeps only this plain name-prefix search. (12) Upstream sign-in is a Duende IdentityServer AccountController that calls `PasswordSignInAsync` with lockout and redirects to the return URL. (14) Webhooks.API exists and subscribes to ProductPriceChanged (handler is a no-op), OrderStatusChangedToShipped, and OrderStatusChangedToPaid.

Check: all five files pass structural checks for Planned slices, with 0 blocking errors. The only warnings are missing-command and missing-gwt on the holder slices. Catalog, Basket, Ordering, and Payment changed in this pass. Identity did not.

## Aggregate root check (2026-10-07)

**Fix:** Catalog Brand and Catalog Type are now Aggregate Roots (`entityType: AggregateRoot`, `isAggregateRoot: true`, same as Catalog Item). Define Catalog Brand and Define Catalog Type now point `aggregateRootId` at them. A command with no `aggregateRootId` still needs an aggregate root. The earlier structural check did not cover that, so the files were accepted before this fix.

**Check:** command aggregate root, unresolved references, entity flag agreement, state property types, and slice status. Holder slices' missing-command and missing-gwt notes are expected and do not fail the check. Last run 2026-10-07: pass, 52 slices, 0 problems, 8 expected (2 per holder).

## API-only maintenance commands, admin screens removed (2026-10-07)

**Decision (2026-10-07):** eShop maintenance and command slices are API only. The services get no admin pages or forms. A WebApp admin area will come later as separate work. Admin and back-office screens were taken out of the models.

**What changed:** 9 screen elements were removed: 7 in Catalog, 1 in Ordering and 1 in Payment. Each removed screen's id was also taken out of its slice's `screens` list. Screens only point at commands (`events`) and views (`viewIds`), and nothing else in the files points at a screen, so no other links needed removing. The commands, events, aggregates, read models, roles and slices are all unchanged. Identity and Basket have only shopper-facing screens, so they did not change.

| Workspace | Slice | Removed screen (id) | Route | It linked to |
|-----------|-------|---------------------|-------|--------------|
| Catalog | Define Catalog Brand | Define Brand (`screen-define-catalog-brand`) | `/admin/catalog/brands/new` | command Define Catalog Brand (`cmd-define-catalog-brand`) |
| Catalog | Define Catalog Type | Define Type (`screen-define-catalog-type`) | `/admin/catalog/types/new` | command Define Catalog Type (`cmd-define-catalog-type`) |
| Catalog | Add Catalog Item | Add Catalog Item (`screen-add-catalog-item`) | `/admin/catalog/items/new` | command Add Catalog Item (`cmd-add-catalog-item`) |
| Catalog | Catalog Item Detail (admin) | Admin Item Detail (`screen-catalog-item-admin-display`) | `/admin/catalog/items/:id` | read model Catalog Item (Admin) (`view-catalog-item-admin`) |
| Catalog | Update Catalog Item | Update Catalog Item (`screen-update-catalog-item`) | `/admin/catalog/items/:id/edit` | command Update Catalog Item (`cmd-update-catalog-item`) |
| Catalog | Remove Catalog Item | Remove Catalog Item (`screen-remove-catalog-item`) | `/admin/catalog/items/:id/remove` | command Remove Catalog Item (`cmd-remove-catalog-item`) |
| Catalog | Restock Catalog Item | Restock Item (`screen-restock-catalog-item`) | `/admin/catalog/items/:id/restock` | command Restock Catalog Item (`cmd-restock-catalog-item`) |
| Ordering | Ship Order | Ship Order (`screen-ship-order`) | `/admin/orders/:id` | command Ship Order (`cmd-ship-order`) |
| Payment | Payment Attempt Status | Payment Attempt (`screen-payment-attempt-display`) | `/ops/payments/:attemptId` | read model Payment Attempt Status (`view-payment-attempt-status`) |

**Text edit:** The Catalog Brands And Types view (`view-catalog-brands-and-types`) description no longer mentions "the backoffice item form". It now ends "for the storefront filter lists and for API clients that add or update catalog items."

**Views that now have no screen:** Catalog Item Detail (admin) and Payment Attempt Status. Both are still valid state-view slices, each with its read model and event subscriptions. They are now read only through the API.

**Kept on purpose:** All shopper-facing screens. These include Cancel Order on the shopper's order detail page (`/orders/:id`) and Identity Consent (`/consent`), whose slice was deferred on 2026-10-08. The Catalog Admin and Store Operator roles stay as the API callers. Cancel Order now has both "User Action" and "System Process": the shopper screen triggers it, and the Payment Failed automation also issues it.

**Triggers:** The 7 commands that lost their admin screen (Define Catalog Brand, Define Catalog Type, Add Catalog Item, Update Catalog Item, Remove Catalog Item, Restock Catalog Item, Ship Order) now have `triggerSources: ["API Call"]` instead of `["User Action"]`.

**Check:** A state-change or state-view slice does not require a screen, so removing the screens did not fail the check. An `api-only-screen` rule fails the run if a screen has a route under `/admin/` or `/ops/`, or a `Backoffice` tag. A `dangling-ref` rule fails the run if a slice's element list, a screen's `events` or `viewIds`, or a widget's `sliceId` points at an id that is not in the file. A `user-action-trigger` rule fails the run if a command lists "User Action" but no screen in the file triggers it.

## Slice questions removed, Grant Client Consent deferred (2026-10-08)

**Rule (2026-10-08):** A slice question is only about how that one slice gets built. Model gaps, eShop parity, infrastructure or wiring, and anything already decided do not belong on a slice.

**What changed:** All 28 question widgets were removed from the five workspaces: Basket 4, Catalog 9, Identity 4, Ordering 7 and Payment 4. Each question's id was also taken out of its slice's `questions` list. Nothing else in the files pointed at a question. The slices themselves, their status and their order did not change. A later pass classified 22 of the questions as resolved and 5 as out of scope. The last one, "Keep Consent in model?", was answered when the slice was deferred.

**Grant Client Consent:** Deferred on 2026-10-08. The slice stays in the Identity model with a `Deferred` tag and a dated note at the end of its description. Its status stays Planned. No other slice in the five workspaces depends on it.

## Decisions written in, model sync applied (2026-10-08)

**Decisions (2026-10-08), now in the slice text and structure:**
1. **Basket keeps add-time price snapshots.** Add Item To Basket, Change Basket Item Quantity and Basket Contents say so. Basket does not consume Catalog Item Price Changed. Place Order uses the basket's snapshot price. Open question 4 is closed.
2. **Remove Catalog Item is a final soft delete.** Removed sets Exists false and Removed true. Add rejects a removed id with `CATALOG_ITEM_ALREADY_EXISTS`. Update, Remove and Restock reject it with `CATALOG_ITEM_NOT_FOUND`. Open question 8 is closed.
3. **Shopper Account Profile is address only, with no card fields.** The order keeps the masked card snapshot. Open question 3 is closed.
4. **Register and Sign In use ASP.NET Identity, with no Duende.** Shopper Signed In is the business event. The Identity workspace description, both slices and the Identity table were updated, and the `Duende` tag on Sign In became `ASP.NET Identity`. Open question 1 is marked resolved. Sign Out and the deferred Grant Client Consent still carry Duende wording; they were not part of this decision.
5. **Grace period uses an explicit Grace Period Confirmed signal, as built.** New event Grace Period Confirmed (`evt-grace-period-confirmed`: EventId, Timestamp, OrderId) sits on the Ordering holder slice, because OrderProcessor has no workspace. The Confirm Grace Period automation now subscribes to it instead of Order Started and still issues Mark Order Awaiting Validation. That command's trigger is now "System Process" only. The OrderProcessor timer (records Order Started, then publishes after GracePeriodTime) is described on the process and the slice.

**Stale wording fixed:** Record Payment Failed and Payment Failed no longer say Ordering "may wire Cancel Order later". When Payment Failed already issues Cancel Order.

**Model sync list (2026-10-08), applied to match merged code:**
- Error codes now put the subject first. Basket `BASKET_QUANTITY_INVALID` (renamed on Change Quantity, new on Add). Deduct `CATALOG_ITEM_OUT_OF_STOCK` (renamed; the id stays `error-insufficient-stock-on-deduct`).
- Missing errors were added: `BASKET_NOT_FOUND` and `BASKET_ITEM_NOT_FOUND` on Change Quantity and Remove Item. Mark Order Awaiting Validation, Mark Order Stock Confirmed and Cancel Order For Insufficient Stock each got `ORDER_NOT_FOUND`, `ORDER_ID_REQUIRED` and `ORDER_REQUEST_ID_REQUIRED`. Place Order got its 16 codes.
- Extra fields were added. Order Placed gained UserName, Street, State, ZipCode, the card fields and PaymentMethodId, and the Order entity gained UserName, PaymentMethodId and the card fields. Order Status Stock Confirmed gained BuyerName, on the producer and on the Payment mirror.
- View event lists changed. The admin item view reads Catalog Item Removed and has a Removed field. Search reads Restocked and Stock Deducted and lists its item summary fields.
- Storefront routes changed: Basket display, Change Quantity and Remove Item moved to `/cart`, Browse to `/`, and Item Detail to `/item/{itemId}`.
- The Validate Order Stock command field text now matches the shared OrderStockItem text.
- Left unchanged: the Add To Basket screen route (the built cart page posts Add, so it is unclear whether the screen should move off the item page), the Clear Basket screen route (the cart page has no Clear button), and the Catalog Brands And Types screen route (`/catalog`).

**Where notes disagreed with the built code, the built code was followed.**
- Place Order has one error per code.
- Deduct still rejects at zero stock. Basket prices stay at the add-time snapshot.
- Buyer and Payment Method stay. The order carries a masked card snapshot and PaymentMethodId.

**Check:** pass, 52 slices (31 state-change, 10 state-view, 9 automation, 2 translation), 0 problems, 8 expected holder notes. Slice ids, names, kinds, statuses and order are unchanged.

## Deduct takes what's left, as in the original eShop (2026-10-08)

**Decision (2026-10-08): match the original eShop, take what's left.** Checked against dotnet/eShop `main` (commit dc7ea49), using public raw URLs:
- `src/Catalog.API/Model/CatalogItem.cs` lines 54-79, `RemoveStock`: throws only when `AvailableStock == 0` ("Empty stock, product item {Name} is sold out") or `quantityDesired <= 0`. Otherwise it sets `removed = Math.Min(quantityDesired, AvailableStock)`, subtracts it and returns it. The doc comment says it "will remove whatever stock is available and return that quantity to the client".
- `src/Catalog.API/IntegrationEvents/EventHandling/OrderStatusChangedToPaidIntegrationEventHandler.cs` lines 12-20: `catalogItem?.RemoveStock(orderStockItem.Units)` runs for each line, then `SaveChangesAsync`. The return value is ignored, nothing reports requested vs removed, and a product id that is not found is skipped (`?.`).

**Deduct Catalog Stock (`slice-deduct-catalog-stock`) updated:**
- The slice, command and event text now say each line removes the smaller of the requested units and the available stock. The only rejections are zero stock (`CATALOG_ITEM_OUT_OF_STOCK`) and units of zero or less. A product id that is not in the catalog is skipped. The slice text records that decision: match the original eShop, take what's left.
- Catalog Stock Deducted keeps its shape. `Deductions` is now described as ItemId + UnitsRemoved, the units actually removed, which can be fewer than requested. No requested field was added, because the original does not report one.
- `error-insufficient-stock-on-deduct` (`CATALOG_ITEM_OUT_OF_STOCK`) now covers zero stock only. New GWT `gwt-deduct-stock-short` (Short stock): 2 available and 5 requested gives Catalog Stock Deducted with UnitsRemoved = 2.
- Units of zero or less are covered in the text only. The original throws with a message and no code, so no error code was invented.
- This settles the clamp half of audit Q4. Deduct still rejects at zero stock, as the original does.

**Built code in this sample vs this model:** take-what's-left, the zero-stock rejection, the units-of-zero-or-less rejection and the Deductions shape match. Two differences remain:
- The code rejects an unknown or removed item with `CATALOG_ITEM_NOT_FOUND`, while the model (and the original) skip it.
- The code checks units of zero or less before zero stock, while the original checks zero stock first. This only matters when both are true.

**Check:** pass, 52 slices (31 state-change, 10 state-view, 9 automation, 2 translation), 0 problems, 8 expected holder notes. Slice ids, names, kinds, statuses and order are unchanged.

## Code checks added to the model; Sign Out and Consent wording (2026-10-08)

**Decision (2026-10-08):** the model is the source of truth. The model lists the checks in the table below. The merged code already makes them, and the code keeps them. Each error description says when the error fires. Sign In (`INVALID_CREDENTIALS`) and Deduct (unknown item skipped, no `CATALOG_ITEM_NOT_FOUND`) were not touched. Those two already match the model.

| Workspace · slice | Error id | Code | GWT added |
|---|---|---|---|
| Identity · Register Shopper Account | `error-user-name-already-registered` | `USER_NAME_ALREADY_REGISTERED` | `gwt-register-duplicate-user-name` |
| Identity · Register Shopper Account | `error-user-name-invalid` | `USER_NAME_INVALID` | `gwt-register-invalid-user-name` |
| Identity · Register Shopper Account | `error-password-requirements-not-met` | `PASSWORD_REQUIREMENTS_NOT_MET` | `gwt-register-weak-password` |
| Identity · Sign Out Shopper | `error-shopper-account-not-found-on-sign-out` | `SHOPPER_ACCOUNT_NOT_FOUND` | `gwt-sign-out-unknown-account` |
| Identity · Update Shopper Profile | `error-shopper-account-not-found-on-update-profile` | `SHOPPER_ACCOUNT_NOT_FOUND` | `gwt-update-profile-unknown-account` |
| Catalog · Update Catalog Item | `error-catalog-item-not-found-on-update` | `CATALOG_ITEM_NOT_FOUND` | `gwt-update-removed-item` |
| Catalog · Remove Catalog Item | `error-catalog-item-not-found-on-remove` | `CATALOG_ITEM_NOT_FOUND` | `gwt-remove-removed-item` |
| Catalog · Restock Catalog Item | `error-catalog-item-not-found-on-restock` | `CATALOG_ITEM_NOT_FOUND` | `gwt-restock-removed-item` |
| Catalog · Validate Order Stock | `error-order-stock-command-id-required` | `ORDER_STOCK_COMMAND_ID_REQUIRED` | none |
| Catalog · Validate Order Stock | `error-order-stock-order-id-required` | `ORDER_STOCK_ORDER_ID_REQUIRED` | none |
| Catalog · Validate Order Stock | `error-order-stock-items-required` | `ORDER_STOCK_ITEMS_REQUIRED` | none |
| Catalog · Validate Order Stock | `error-order-stock-product-id-required` | `ORDER_STOCK_PRODUCT_ID_REQUIRED` | none |
| Catalog · Validate Order Stock | `error-order-stock-units-invalid` | `ORDER_STOCK_UNITS_INVALID` | none |
| Basket · Clear Basket | `error-basket-not-found-on-clear` | `BASKET_NOT_FOUND` | `gwt-clear-basket-unknown` |

The Email Already Registered text dropped "(or user name)", because a taken user name now has its own error. The descriptions come from the merged code.

**ASP.NET Identity wording (follows the no-Duende decision):**
- The Sign In UserName field said "Login name or email". It now says sign-in is by user name only.
- Sign Out: the slice and command text are written for ASP.NET Identity, LogoutId is accepted and not applied, and the slice tag `Duende` became `ASP.NET Identity`.
- Grant Client Consent: the slice, command, event and screen text no longer mention Duende or OIDC client ids, and the `Duende` tags became `ASP.NET Identity`. The `Deferred` tag and note stay.

**Order Status Stock Confirmed (finding only; the BuyerId and OrderTotal optional flags are unchanged):** In the original eShop, the Ordering event carries OrderId, OrderStatus, BuyerName and BuyerIdentityGuid, with no order total. PaymentProcessor's copy carries only OrderId. Payment uses no amount: success or failure comes from the `PaymentOptions.PaymentSucceeded` setting.

**Update 2026-10-08:** BuyerId and OrderTotal are now required on Order Status Stock Confirmed (`evt-order-status-stock-confirmed`), on both the Ordering producer and the Payment mirror. The original eShop carries the buyer id but no total. The total is our addition so Payment knows the amount. Check: pass (52 slices, 0 problems, 8 expected holder notes).

**Check:** pass, 52 slices (31 state-change, 10 state-view, 9 automation, 2 translation), 0 problems, 8 expected holder notes. Slice ids, names, kinds, statuses and order are unchanged.

## Catalog pictures and seeded Identity cards (2026-10-08)

**Decision (2026-10-08):**
1. **Catalog picture read.** Catalog serves GET `/api/catalog/items/{id}/pic` (mirrors eShop `GetItemPictureById`). It returns the file named by `PictureFileName` with the eShop image content types. It returns 404 for an unknown or removed item, a missing file, or a file name that is not a single plain name. Modeled on the existing View Catalog Item slice (no new slice; reads here have no error widgets, so the 404 cases are description only).
2. **Storefront picture link.** WebApp GET `/product-images/{id}` forwards to that Catalog route. Browse, search, the item page and the cart use it when the stored picture is a file name; an empty picture shows the example image. Noted on the Catalog browse/search/item screens and views, and on the Basket cart screen, items view and Add Item To Basket `PictureUrl` snapshot.
3. **Identity seeded demo cards (description only).** Card holder, masked number, type, expiration and security number live only on the ASP.NET Identity login record in SQL, like eShop's `ApplicationUser`, and never in events. Key-deletion encryption is a later LawnDart item. Written into the Identity workspace description and the Shopper Account Profile slice. No new fields.

Descriptions follow the public eShop `GetItemPictureById` shape.

**Check:** pass, 52 slices (31 state-change, 10 state-view, 9 automation, 2 translation), 0 problems, 8 expected holder notes. Slice ids, names, kinds, statuses and order are unchanged. No new slice.

## Outcome

- **All five workspaces are done:** Identity, Catalog, Basket, Ordering, Payment.
- The five workspace files in this folder are the model. Mirror events live on "Inbound ... Signals (mirrors)" holder slices so business Events zones stay clean.
