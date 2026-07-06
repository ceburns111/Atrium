# Interview study — Backend services & data

> Scope: the two backend services and their data layer — `Atrium.Services.Catalog` (a **core**
> service that owns its data) and `Atrium.Services.Storefront` (an **app vertical** that owns its own
> data and composes Catalog over HTTP). The stack is deliberately **Dapper + stored procedures + DbUp
> + Mapperly, no EF**. Everything below is grounded in code; file refs are inline.

---

## The 90-second explanation

Each service owns exactly one database and exposes it through a minimal-API HTTP surface — no other
service reaches into that database. Catalog owns `catalogdb` (products, categories); Storefront owns
`storefrontdb` (orders, order items). When Storefront needs product data it doesn't own — to price an
order or bucket sales by category — it **calls Catalog over HTTP** and relays the caller's bearer
token, never a cross-database join (ADR-0005).

Inside a service, code is organized by **feature folder** (vertical slice), not by technical layer:
`Orders/`, `Reports/`, `Catalog/` each hold their endpoint, repository, and row types side by side
(ADR-0007). Endpoints are registered as `Map*Endpoints` extension methods onto a route group — the
service root group carries the shared `RequireAuthorization()`, each feature maps a relative subtree
with its own `.WithTags(...)` (ADR-0009).

Data access is explicit SQL: **every read and write goes through a stored procedure**, executed with
**Dapper** (thin, no change tracker). Rows map to wire DTOs with **Mapperly**, a compile-time source
generator (no reflection). Schema is managed by **DbUp in two lanes** — `Migrations/` run once and are
journaled, `Programmability/` (the sprocs, written `CREATE OR ALTER`) run on every startup — with the
SQL shipped as embedded resources and applied by `DatabaseInitializer` at boot (ADR-0002). We chose
this over EF precisely to make the SQL the source of truth and reviewable, which is the stack this
portfolio is meant to demonstrate.

Two properties I'd lead with: **order creation is idempotent** (a client idempotency key dedupes
retries, inside one transaction), and **prices are taken from the authoritative Catalog, never trusted
from the client**. The repositories are **integration-tested against a real SQL Server** via
Testcontainers, because a mocked repository would prove nothing about the SQL.

---

## How it actually works

### An endpoint → repository → sproc → Mapperly → DTO round-trip (Catalog products)

1. **Endpoint.** `MapCatalogEndpoints` (`src/Atrium.Services.Catalog/Catalog/CatalogEndpoints.cs`)
   creates one group: `app.MapGroup("/catalog").WithTags("Catalog").RequireAuthorization()`. Reads opt
   back out with `.AllowAnonymous()` (so the storefront browses signed-out); writes add
   `.RequireAuthorization("admin")`. Handlers are **named static methods** returning `TypedResults`
   (e.g. `Ok<IReadOnlyList<ProductDto>>`), not inline lambdas — testable and compiler-checked.
2. **Repository.** `GetProducts` injects `ICatalogRepository`. `CatalogRepository`
   (`Catalog/CatalogRepository.cs`) is a `sealed` class taking the Aspire-injected `SqlConnection`
   ("catalogdb") and calls Dapper's `QueryAsync<ProductRow>` with `CommandType.StoredProcedure`
   against `dbo.usp_Product_GetList`. No inline SQL anywhere in C#.
3. **Sproc.** `usp_Product_GetList.sql` joins `Products` to `Categories` and projects the category as
   `CategoryName`, optionally filtered by category (`@CategoryName IS NULL OR c.Name = @CategoryName`).
4. **Mapperly.** `ProductRow` (with `CategoryName`) → `ProductDto` (with `Category`) via
   `CatalogMapper.ToDtos(...)` (`Catalog/CatalogMapper.cs`), a `[Mapper] partial` class. The
   `[MapProperty(CategoryName → Category)]` rename means it's a *real*, non-identity generated mapping,
   not a passthrough. Generated at compile time — no runtime reflection, no AutoMapper.
5. **DTO.** Returns `ProductDto` (`src/Atrium.Contracts/ProductDto.cs`), a `sealed record` in the
   shared contracts project both sides reference.

Write path is symmetric: `usp_Product_Create` inserts and then **`SELECT`s the created row back**
(joined to its category name) in the same round trip, so the app gets persisted state without a second
query. `CatalogRepository.WriteProductAsync` maps that row with `CatalogMapper.ToDto`. An unknown
category makes the sproc `THROW 50001`; the repository logs and rethrows the `SqlException`.

### DbUp two-lane initialization

`DatabaseInitializer.Initialize(...)` (`Catalog/Data/DatabaseInitializer.cs`, byte-identical copy in
Storefront) runs at startup **before serving traffic**, called from `Program.cs`:

- `EnsureDatabase.For.SqlDatabase(...)` creates the DB if absent.
- **Migrations lane** — `WithScriptsEmbeddedInAssembly(asm, n => n.Contains(".Migrations."))`,
  journaled (default journal table). Runs each script **at most once, ever**, in order. Schema + seed
  (`0001_Schema.sql`, `0002_Seed.sql`).
- **Programmability lane** — same but filtered on `.Programmability.`, with `.JournalTo(new
  NullJournal())` so it is **not** journaled. Scripts are `CREATE OR ALTER PROCEDURE ...`, so
  re-running is idempotent — **every startup redeploys every sproc to its latest definition**. A proc
  edit ships without a new migration.
- Both lanes use `.WithTransactionPerScript()` and `.LogToConsole()` (DbUp 7.x — not
  `LogToAutodetectedLog()`, a version pin called out in ADR-0002).

The result: **the DB schema lives in the repo, versioned and diffable in review** — no "what did the
ORM generate?" surprise.

### The checkout write path (idempotent order creation)

Traced in `docs/diagrams/checkout-flow.md`. The service side:

1. **Endpoint.** `POST /storefront/orders` → `CreateOrder` (`Orders/OrdersEndpoints.cs`). First guard:
   reject `IdempotencyKey == Guid.Empty` (an empty key would collide on the unique index across
   unrelated orders).
2. **Re-price from Catalog.** `catalog.GetProductsAsync(ct)` fetches the authoritative product list
   over HTTP (`StorefrontCatalogClient`, bearer relayed). `OrderPricing.PriceOrder` (`Orders/
   OrderPricing.cs`, a **pure function**) builds each line using the **catalog's** name and price —
   the request only supplies `ProductId` and `Quantity`. The client's price is never read.
3. **Transactional write.** `OrderRepository.CreateAsync` (`Orders/OrderRepository.cs`) opens the
   connection, begins a transaction, and calls `usp_Order_Create` with `{UserName, Total,
   IdempotencyKey}`. The sproc's replay check is **user-scoped**: if this key was already committed
   *by this user*, it returns the original `OrderId` with `IsNew = 0`; otherwise it inserts inside a
   TRY/CATCH — a concurrent double-submit that loses the race on the unique index is re-read and
   replayed (`IsNew = 0`), while a key belonging to a *different* user raises error 50002, which the
   endpoint maps to **409 Conflict** (never another user's order id).
4. **Conditional line insert.** Only if `IsNew` does the repo loop `usp_OrderItem_Add` per line — so a
   replay never re-adds items. Then `transaction.CommitAsync`. The unique filtered index
   `UX_Orders_IdempotencyKey` (`Migrations/0002_OrderIdempotencyKey.sql`) is the integrity backstop
   against a concurrent double-submit.
5. **Faithful response.** The endpoint returns the order **read back** via `GetByIdAsync` (the
   user-scoped sproc), so on a replay the client sees the stored total/lines/timestamp — never a
   re-priced reconstruction.

Read path (`GetOrdersAsync` / `GetByIdAsync`): the read sprocs return **flat header×line rows**; the
repo regroups them with LINQ `GroupBy` into one `OrderDto` per header with its `Lines`.

---

## Why it's built this way

### Dapper + sprocs + Mapperly vs EF — the real trade-off (ADR-0002)

The honest position, not "ORMs are bad":

- **The SQL is the point.** This project deliberately demonstrates the explicit-SQL stack a lot of
  teams run in production — hand-written sprocs, a migration runner you control, no change-tracker in
  the hot path. EF would *hide* the very thing being showcased.
- **Schema as source of truth.** Sprocs and migrations are versioned files, diffable in code review.
  No "what did the ORM generate?" step.
- **Cheaper mapping.** Mapperly is a compile-time source generator — no reflection, and a mapping
  break fails the **build**, not a runtime request. Strictly cheaper than AutoMapper.
- **Accepted cost: boilerplate.** Each new query is a sproc + a Dapper call + (if shaped) a Mapperly
  mapping. That's the tax for explicitness, and I took it deliberately.
- **No lazy loading / change tracking / LINQ provider.** Composition across aggregates is explicit —
  which we wanted anyway, because the data lives in separate databases.

### DbUp vs EF migrations

EF's migration model would compete with the two-lane split. DbUp's split maps cleanly onto reality:
**schema changes are irreversible and versioned** (run-once, journaled) while the **programmable
surface is declarative and always current** (`CREATE OR ALTER`, run-always). Proc edits are a one-file
change — which is exactly why Admin's create/update endpoints landed by adding two run-always sprocs
with no migration.

### One database per service — no shared DB (ADR-0005)

Storefront needs product names and prices, but Catalog **owns** them in `catalogdb`. The tempting
shortcut — a cross-DB join or a second connection string into `catalogdb` — quietly couples the two
services at the schema level and destroys the "each service owns its data" property the whole topology
rests on. Instead Storefront calls Catalog at its discovery address (`https+http://catalog`) and
**relays the caller's bearer token** (`StorefrontCatalogClient`), so it gets product data the same way
any client would — over the API, authorized as the same user. The shared `atrium` audience is what
lets Catalog accept the relayed token. Cost: an extra network hop and a partial-failure surface (if
Catalog is down, Storefront degrades) — the honest price of real isolation.

### Contracts as a shared project now, NuGet later (ADR-0006)

`Atrium.Contracts` holds the wire DTOs (`ProductDto`, `OrderDto`, `CreateOrderRequest`, …) as
**DTO-only `sealed record`s**, referenced by both producer (service) and consumer (UI module + typed
client). One repo, one build → zero version skew and **a breaking DTO change can't slip past the
compiler; it fails both sides at once**. This is deliberately temporary: at the polyrepo split it
becomes a versioned NuGet package with SemVer discipline. Guardrail kept even now: no behavior in
contracts, so the eventual package surface stays small and stable.

### Feature folders + co-located, integration-tested repositories (ADR-0007)

Organized by feature (vertical slice), not by technical layer — you read `Orders/` top to bottom in
one folder instead of hopping between `Endpoints/` and `Repositories/` trees. Each single-implementation
interface (`ICatalogRepository`, `IOrderRepository`, `IReportRepository`) is co-located **directly
above** its implementing class, not in a separate file. The interface is kept as the **DIP seam** (a
decorator or a hand-rolled fake stays cheap) — *not* for mockability, which is the weak justification
here. `DatabaseInitializer.cs` is duplicated byte-for-byte in both services on purpose: a shared data
library would couple two independently-deployable services to save ~40 lines.

---

## What's impressive here / talking points

- **Idempotent order creation done properly.** A client-generated idempotency key per checkout
  attempt, a filtered unique index as the DB-level backstop, and an `IsNew` flag from the sproc so a
  replay returns the original order id and **skips re-inserting lines** — header + lines committed in
  one transaction owned by the repository. Retries after an ambiguous failure (timeout after commit)
  are safe. Directly tested (`OrderRepositoryTests.Create_is_idempotent_for_a_repeated_key`).
- **Prices from the authoritative core, never the client.** The order request carries only product id
  and quantity; `OrderPricing` looks up name and price from Catalog. A tampered client price is
  structurally impossible — this is a *security* property, not just correctness, and it's a pure
  function so it's unit-tested (`OrderPricingTests`).
- **The authorization boundary is in the sproc.** `usp_Order_GetById` filters
  `WHERE o.Id = @OrderId AND o.UserName = @UserName`. "Not yours" and "doesn't exist" both collapse to
  zero rows → `null`. The ownership check can't be forgotten at the app layer because it's in the WHERE
  clause. Tested from both sides
  (`GetById_returns_null_when_the_order_belongs_to_another_user`).
- **Compile-time mapping.** Mapperly generates `ProductRow → ProductDto` at build; a shape mismatch is
  a build error, and there's zero reflection cost at runtime.
- **Testcontainers integration tests over a real SQL Server.** `SqlServerFixture` boots one MSSQL 2022
  container for the run; each test class provisions its **own** database on it via the real
  `DatabaseInitializer`, exercising real DbUp + real sprocs + real Dapper + real Mapperly. It even
  asserts the sproc's own error path (`THROW 50001` surfaces as `SqlException.Number == 50001`).
  `EndpointAuthorizationTests` boots each service in-process with `WebApplicationFactory` and asserts
  the anonymous-browse / gated-checkout posture at the real HTTP boundary.

---

## Likely interview questions → strong answers

**Q: Why Dapper + sprocs instead of EF Core?**
Deliberate: this codebase demonstrates the explicit-SQL stack a lot of teams run — hand-written sprocs
as the source of truth, a migration runner I control, no change-tracker in the hot path. EF is a fine
default but hides the SQL that's the whole point here, and its migration model competes with DbUp's
two-lane split. The accepted cost is boilerplate; I chose it consciously (ADR-0002).

**Q: How do migrations work?**
DbUp, two lanes, embedded SQL, at startup. `Migrations/` scripts are journaled and run **once, in
order** (schema + seed). `Programmability/` scripts are `CREATE OR ALTER` sprocs with a `NullJournal`,
so they run **every startup** and idempotently redeploy the procedures. `DatabaseInitializer` applies
both before the app serves traffic. A proc change is a one-file edit with no new migration.

**Q: How do you keep services decoupled without a shared database?**
One database per service, and services compose **over HTTP**, never over the DB. Storefront doesn't
own product data, so it calls Catalog at its service-discovery address and relays the caller's bearer
token. No cross-DB join, no second connection string into someone else's DB — that would couple
schemas and break data ownership (ADR-0005).

**Q: How do you make order creation safe under retries?**
A client-generated idempotency key per checkout attempt. `usp_Order_Create` checks for that key
**scoped to the user**: if this user already committed it, the original id comes back with
`IsNew = 0`; a concurrent double-submit is settled by TRY/CATCH on the unique index (loser re-reads
and replays); another user's key is refused with error 50002 → 409. The repo only adds line items when
`IsNew`, all inside one transaction, and the endpoint returns the order **read back from the DB**, so
a replay response is the stored truth, not a re-price. An empty key is rejected at the endpoint so
`Guid.Empty` can't collide across unrelated orders.

**Q: Where's the authorization boundary for reading an order?**
In the sproc's WHERE clause: `usp_Order_GetById` filters on both `@OrderId` **and** `@UserName`, so a
user can only ever read their own order — a wrong owner returns zero rows, indistinguishable from "not
found." The security check lives in the data layer where it can't be bypassed by an app-layer mistake.
(HTTP-layer gating is separate: the `/storefront` group's `RequireAuthorization()` blocks anonymous
callers before the handler.)

**Q: Why not just trust the price the client sends?**
Because the client is untrusted. The order request carries product id and quantity only;
`OrderPricing` prices every line from the authoritative Catalog. It's a pure function, extracted out of
the handler so it's unit-tested with no HTTP or DB.

**Q: How do you test the data layer?**
Testcontainers — a real SQL Server in a throwaway Docker container. The real repository runs real
sprocs through Dapper against a real DB, including the sproc error paths. I don't mock the repository,
because a `Mock<IOrderRepository>` proves my handler calls the repo — it proves nothing about whether
the sproc, the transaction, or the flat-rows→DTO regrouping is correct, which is the repository's
entire job (ADR-0007).

**Q: Then why keep the repository interfaces at all?**
Dependency inversion and convention — handlers depend on an abstraction, not on Dapper, which keeps
the door open to a decorator (caching/logging) or a hand-rolled fake at near-zero cost. Explicitly
**not** for mockability; I co-located each single-implementation interface above its class to remove
the only real cost (the extra file).

**Q: Why Mapperly instead of AutoMapper — or hand-mapping?**
Mapperly is a compile-time source generator: no runtime reflection, and a mapping break fails the
build. AutoMapper pays reflection at runtime for the same job. Hand-mapping is fine too, but the
generator gives the compile-time safety for free and the `CategoryName → Category` rename shows it's a
real mapping, not a passthrough.

**Q: How are endpoints organized?**
Minimal API, one `MapGroup` per feature via a `Map*Endpoints` extension method, handlers as named
static methods returning `TypedResults`. The service-root group states the boundary and shared
`RequireAuthorization()` once; each feature maps a **relative** subtree with its own `WithTags`
(ADR-0009). No auto-registration package — routing lives in the feature folder.

**Q: How does Storefront authenticate to Catalog on the server-to-server hop?**
It reads the incoming `Authorization` header off `IHttpContextAccessor` and forwards the same bearer
on the outbound call. That works because a normal API request *has* an `HttpContext` (unlike a Blazor
circuit). Catalog validates it against the shared `atrium` audience — same user, same token.

**Q: Why does a write sproc SELECT the row back?**
So the app gets the persisted state (server-assigned id, joined category name) in **one round trip**
instead of insert-then-reselect. `usp_Product_Create` returns the created row; the repo maps it
straight to a DTO.

**Q: What happens if Catalog is down when someone checks out?**
Storefront degrades — `StorefrontCatalogClient` calls `EnsureSuccessStatusCode()`, so pricing fails
and the order isn't placed. That's the honest cost of real service isolation; caching/Polly resilience
is a production concern I'd add, not a demo one (ADR-0005).

**Q: How is the admin-only reporting surface protected?**
`ReportsEndpoints` adds `.RequireAuthorization("admin")` on the `/reports` subgroup, on top of the
parent group's authentication — matching the admin-gated Reports nav in the portal, so a plain
customer can't reach the analytics API even by calling it directly.

---

## Gotchas & things that could trip you up

- **Programmability runs on *every* startup.** Every sproc is redeployed via `CREATE OR ALTER` each
  boot — that's the design (proc edits ship without a migration), but it means a sproc file is the live
  definition, not a historical record. Schema-changing DDL must go in a **Migrations** script (run-once,
  journaled), never in a run-always file.
- **SQL is embedded resources, matched by name substring.** DbUp filters on
  `name.Contains(".Migrations.")` / `.Programmability.`. A script placed in the wrong folder, or a
  csproj that doesn't embed it, silently doesn't run. Migrations also run **in name order**, so the
  `0001_/0002_` prefixes are load-bearing.
- **The transaction boundary is owned by the repository, not the sproc.** `OrderRepository.CreateAsync`
  opens the connection, `BeginTransactionAsync`, threads the same `transaction` into every
  `CommandDefinition`, and commits. `usp_Order_Create` and `usp_OrderItem_Add` are separate calls —
  atomicity comes from the C# transaction, so every command in that path **must** carry the
  transaction or it'd run outside it.
- **`GO` batching in migrations.** `0002_OrderIdempotencyKey.sql` uses `GO` to separate the
  `ALTER TABLE ADD` from the `CREATE INDEX` that references the new column — DbUp splits on `GO`.
- **The create response IS a faithful read-back** (fixed 2026-07-03 — it used to be a re-priced
  reconstruction with a fresh `DateTime.UtcNow`). `CreateOrder` re-reads the committed order through
  `GetByIdAsync` before returning, so replays return the stored total/lines/`PlacedAtUtc`. The
  cross-user and concurrent replay paths are integration-tested.
- **`GetByIdAsync` has no public route.** `OrdersEndpoints` maps only `POST /` and `GET /` (list) —
  there is no `GET /orders/{id}`. The method IS exercised over HTTP indirectly: the create endpoint's
  read-back and the Support agent's `GetOrderStatus` tool both go through it (and through its
  user-scoping WHERE clause). Don't claim a single-order REST endpoint exists.
- **The breaking-change-fails-both-sides rule.** Because `Atrium.Contracts` is a project reference,
  changing a DTO record recompiles producer and consumer together — a rename or a new required
  positional field breaks *both* builds at once. That's a feature (no silent drift) but means DTO edits
  are never local to one service.
- **`http.User.Identity?.Name ?? "unknown"`.** Order handlers fall back to the literal `"unknown"` if
  there's no name claim. In practice the `/storefront` group requires auth so this shouldn't hit, but
  it's a fallback, not a guarantee — the real identity comes from the `preferred_username` claim
  (`NameClaimType` in `Program.cs`).

---

## If they push deeper / how I'd evolve it

- **Outbox + events between services.** Today Storefront pulls product data synchronously from Catalog
  on every order and every report. The next step for decoupling and resilience is an outbox on the
  write side and events (e.g. `ProductPriceChanged`) so Storefront maintains its own read model instead
  of a live fan-out — trading freshness for availability and removing the partial-failure hop.
- **Read models / CQRS-lite.** The reports path already regroups flat rows and buckets in a pure
  function (`SalesReportBuilder`). A materialized read model (a denormalized sales table refreshed off
  order events) would take the Catalog call out of the hot path entirely.
- **Sproc vs inline query maintainability.** Sprocs are great for reviewable, versioned SQL and for
  keeping the security boundary in the WHERE clause, but they're more ceremony per query than an ORM.
  If a service grew a lot of ad-hoc, low-risk read queries I'd consider Dapper with parameterized inline
  SQL for those while keeping the writes and the security-sensitive reads as sprocs — a pragmatic split,
  not an all-or-nothing.
- **Extracting Orders into its own core service.** ADR-0005 calls this out as a clean seam: when a
  second slice needs orders, `Orders` graduates from "owned by the Storefront vertical" to its own core
  service, and the slice-calls-core-over-HTTP pattern is unchanged.
- **Connection-per-request lifetime & resilience.** Aspire injects a **scoped** `SqlConnection`
  (per request). If a service went chattier I'd revisit pooling behavior and add Polly retry/circuit
  breakers around the Catalog hop and the DB, plus caching for the product list (which is read-heavy and
  changes rarely).
- **Contracts → versioned NuGet.** At the polyrepo split, `Atrium.Contracts` (or per-domain slices)
  becomes a SemVer-versioned package so producer and consumer deploy independently (ADR-0006) — the
  project reference stops working the moment they don't share a build.
