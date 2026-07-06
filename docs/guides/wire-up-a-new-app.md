# Guide — wire up a new Atrium app (vertical)

How to add a new **app vertical** to Atrium end to end: a backend service, its wire contracts, a UI
module, a gateway route, Aspire wiring, auth, and tests. This is a checklist you can follow top to
bottom.

The guide narrates the **real, existing** Storefront + Catalog vertical as its worked example — it is
the reference implementation, not a toy. Every path, class, route, and config key below exists in the
repo; open them alongside this doc.

Read these first for the *why* behind each step:
- [ARCHITECTURE.md](../ARCHITECTURE.md) — how the pieces fit.
- [ADR-0001](../adr/0001-modular-monolith.md) modular monolith · [ADR-0002](../adr/0002-dapper-sprocs-dbup.md)
  Dapper/sprocs/DbUp/Mapperly · [ADR-0003](../adr/0003-yarp-keycloak-auth.md) YARP + Keycloak ·
  [ADR-0004](../adr/0004-token-propagation-and-option-b.md) token into the circuit ·
  [ADR-0005](../adr/0005-slice-calls-core.md) slice calls core · [ADR-0006](../adr/0006-shared-contracts-then-nuget.md)
  shared contracts · [ADR-0007](../adr/0007-feature-folders-and-repository-testing.md) feature folders ·
  [ADR-0009](../adr/0009-service-root-route-nesting.md) service-root route nesting.

## Two service shapes — pick one first

Atrium services come in two shapes ([ADR-0005](../adr/0005-slice-calls-core.md)). Decide which you are
building before you start:

- **Core service** — owns a capability's data in its own database, exposes it over its API, calls no
  other service. Reference: `Atrium.Services.Catalog` (owns `catalogdb`, serves products/categories).
- **App vertical** — owns *its own* database **and** composes core services over HTTP for anything it
  doesn't own. Reference: `Atrium.Services.Storefront` (owns `storefrontdb` for orders; calls Catalog
  for prices/categories via a bearer relay).

The steps below build a full **app vertical** (the superset). If you're building a core service, skip
the "calls a core service" sub-steps (2.5 client, the `IHttpContextAccessor` relay, and the extra
`WithReference`).

Throughout, replace `Widget` / `widget` with your vertical's name, and `widgetdb` with its database.

---

## 1. The backend service (`src/Atrium.Services.Widget`)

A service is an ASP.NET minimal-API project (`Microsoft.NET.Sdk.Web`) organized by **feature folder**
([ADR-0007](../adr/0007-feature-folders-and-repository-testing.md)), with Dapper + stored procedures +
DbUp for data ([ADR-0002](../adr/0002-dapper-sprocs-dbup.md)).

### 1.1 Create the project and reference contracts

Model the `.csproj` on `src/Atrium.Services.Storefront/Atrium.Services.Storefront.csproj`. It needs
`net10.0`, project references to `Atrium.Contracts` and `Atrium.ServiceDefaults`, the package set
(`Aspire.Microsoft.Data.SqlClient`, `Dapper`, `Microsoft.AspNetCore.OpenApi`,
`Microsoft.Extensions.ServiceDiscovery`, `Riok.Mapperly`), and — critically — the embedded-SQL glob so
DbUp scripts ship inside the assembly:

```xml
<ItemGroup>
  <EmbeddedResource Include="Data\Scripts\**\*.sql" />
</ItemGroup>
```

`Aspire.Keycloak.Authentication` and `dbup-sqlserver` come **transitively through `Atrium.ServiceDefaults`** — don't add them directly.

Add the project to the solution: `src/Atrium.Services.Widget/Atrium.Services.Widget.csproj` in
`Atrium.slnx` (under the `/src/` folder).

### 1.2 Lay out the feature folder

Follow the Storefront layout ([ADR-0007](../adr/0007-feature-folders-and-repository-testing.md)) — a
folder per slice holds its endpoint, handler logic, repository (+ its interface, co-located above the
class), and row type; namespaces mirror folders:

```
Atrium.Services.Widget/
  Program.cs
  Widget/    WidgetEndpoints.cs, WidgetRepository.cs (+ IWidgetRepository), WidgetMapper.cs, WidgetRow.cs
  Data/      Scripts/Migrations/*.sql, Scripts/Programmability/*.sql
```

The interface lives **in the same file, directly above** its implementing class — see
`src/Atrium.Services.Catalog/Catalog/CatalogRepository.cs` (`ICatalogRepository` + `CatalogRepository`
in one file). Don't create a separate `IWidgetRepository.cs`.

### 1.3 Endpoints — `Map*Endpoints`, route group, `.WithTags`

Write endpoints as a static extension method with handlers as **named static methods** (testable, no
inline lambdas), using `TypedResults`. Reference: `src/Atrium.Services.Catalog/Catalog/CatalogEndpoints.cs`
and `src/Atrium.Services.Storefront/Orders/OrdersEndpoints.cs`.

Route structure follows [ADR-0009](../adr/0009-service-root-route-nesting.md): the **service-root group
is declared once in `Program.cs`** (with the shared `RequireAuthorization()`), and each feature maps a
**relative** subtree onto it. So a feature endpoint takes the parent group:

```csharp
// src/Atrium.Services.Widget/Widget/WidgetEndpoints.cs  (pattern from OrdersEndpoints.cs)
public static class WidgetEndpoints
{
    public static void MapWidgetEndpoints(this IEndpointRouteBuilder widget)
    {
        var widgets = widget.MapGroup("/widgets").WithTags("Widgets");   // relative → /widget/widgets
        widgets.MapGet("/", GetWidgets);
        widgets.MapPost("/", CreateWidget).RequireAuthorization("admin"); // per-endpoint policy, optional
    }
    // ...named static handlers returning TypedResults...
}
```

- `.WithTags("Widgets")` stays **per feature** — it controls OpenAPI grouping in the `/openapi/v1.json`
  document both services expose in Development (see §1.5).
- A **core** single-feature service is the degenerate case: its one group *is* the service root, mapped
  in the endpoints file itself — see `CatalogEndpoints.cs`, where `app.MapGroup("/catalog")...
  .RequireAuthorization()` is both the service prefix and the feature.

### 1.4 Data — Dapper + sprocs + DbUp migrations

Per [ADR-0002](../adr/0002-dapper-sprocs-dbup.md), **all SQL lives in stored procedures**; Dapper just
executes them; Mapperly maps rows → DTOs at compile time.

1. **Migrations (run-once, journaled)** — schema + seed under `Data/Scripts/Migrations/`, numbered
   (`0001_Schema.sql`, `0002_Seed.sql`). Reference:
   `src/Atrium.Services.Catalog/Data/Scripts/Migrations/0001_Schema.sql`.
2. **Programmability (run-always, `CREATE OR ALTER`)** — one sproc per file under
   `Data/Scripts/Programmability/` (e.g. `usp_Widget_GetList.sql`, `usp_Widget_Create.sql`). These
   redeploy on every start, so a proc change needs **no new migration**. Reference:
   `src/Atrium.Services.Catalog/Data/Scripts/Programmability/usp_Product_GetList.sql` (read) and
   `usp_Product_Create.sql` (a write that `SELECT`s the affected row back and `THROW`s on bad input).
3. **`DatabaseInitializer`** — shared in `Atrium.ServiceDefaults` (see [ADR-0012](../adr/0012-shared-deployment-infrastructure.md));
   **do not copy** it into your service. Call it from `Program.cs`:
   `DatabaseInitializer.Initialize(connectionString, typeof(Program).Assembly, app.Logger)`.
   Pass `typeof(Program).Assembly` so it finds your embedded SQL scripts. Your `Data/` folder holds
   only the `Scripts/` tree.
4. **Row type** (`WidgetRow.cs`) is the internal shape returned by the sproc; **Mapperly**
   (`WidgetMapper.cs`, `[Mapper]` + `partial`) maps it to the public DTO. Use
   `[MapProperty(...)]` for any renamed column — see `CatalogMapper.cs` renaming `CategoryName` →
   `Category`.
5. **Repository** implements the co-located interface, takes the Aspire-injected `SqlConnection` by
   constructor, and calls sprocs via `CommandDefinition(..., commandType: CommandType.StoredProcedure)`.
   Reference: `src/Atrium.Services.Catalog/Catalog/CatalogRepository.cs`.

### 1.5 `Program.cs` — DI, auth, DB init, routing

Model on `src/Atrium.Services.Storefront/Program.cs` (app vertical) or `src/Atrium.Services.Catalog/Program.cs`
(core service). In order:

```csharp
using Atrium.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddAtriumTelemetry(instrumentSqlClient: true);  // Serilog + OTel → Aspire dashboard

builder.AddSqlServerClient("widgetdb");                 // Aspire-injected scoped SqlConnection
builder.Services.AddScoped<IWidgetRepository, WidgetRepository>();
builder.Services.AddHttpContextAccessor();              // ONLY if this vertical relays a bearer to a core
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();                          // document at /openapi/v1.json

builder.Services.AddServiceDiscovery();                 // resolve https+http://<name>
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

// ONLY if this vertical composes a core service (app-vertical shape):
builder.Services.AddHttpClient<WidgetCatalogClient>(c => c.BaseAddress = new Uri("https+http://catalog"));

// JWT validation (shared "atrium" realm + audience), load-bearing claim mapping, and the "admin" policy:
builder.AddAtriumJwtAuth();
// If this host adds service-specific policies, chain them: builder.AddAtriumJwtAuth().AddPolicy(...)

var app = builder.Build();

var connectionString = app.Configuration.GetConnectionString("widgetdb")
    ?? throw new InvalidOperationException("Connection string 'widgetdb' was not configured.");
DatabaseInitializer.Initialize(connectionString, typeof(Program).Assembly, app.Logger);

app.UseAtriumRequestLogging();   // one structured log line per request; call before handlers
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// API docs: both the JSON document and the Redoc viewer, exposed in Development only and anonymous
// (mapped outside the bearer group so a curl check can reach them without a token):
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();               // /openapi/v1.json
    app.MapAtriumApiDocs("Atrium Widget API");        // /docs Redoc viewer
}

var widget = app.MapGroup("/widget").RequireAuthorization();   // service-root group, ADR-0009
widget.MapWidgetEndpoints();

app.Run();
```

Key points, all matching the real services:
- `AddAtriumTelemetry` / `AddAtriumJwtAuth` / `DatabaseInitializer.Initialize` / `MapAtriumApiDocs` all
  live in `Atrium.ServiceDefaults` ([ADR-0012](../adr/0012-shared-deployment-infrastructure.md)).
- `AddAtriumJwtAuth()` wires Keycloak JWT bearer for the shared `atrium` realm/audience, sets the
  load-bearing claim-mapping options (`MapInboundClaims = false`, `RoleClaimType = "role"`) and
  registers the `admin` policy — no manual `AddAuthentication` or `AddAuthorization` needed.
- `AddSqlServerClient("widgetdb")` binds to the connection string named by the Aspire database resource
  (step 5).
- The **service-root `MapGroup("/widget")`** with `RequireAuthorization()` is stated once; features map
  relative children ([ADR-0009](../adr/0009-service-root-route-nesting.md)).
- `AddHealthChecks()` + `MapHealthChecks("/health")` back the AppHost's `.WithHttpHealthCheck("/health")`.

---

## 2. The contracts (`src/Atrium.Contracts`)

Wire DTOs shared by the service (producer) and the module + typed client (consumer) live in the single
`Atrium.Contracts` project ([ADR-0006](../adr/0006-shared-contracts-then-nuget.md)) — **DTO-only, no
behavior**. Add your records there (e.g. `WidgetContracts.cs` / `WidgetDto.cs`), following the shape of
`src/Atrium.Contracts/ProductContracts.cs` (`CreateProductRequest`/`UpdateProductRequest`) and
`ProductDto.cs`. Both the service and the module reference this project, so a breaking DTO change fails
the build on both sides.

---

## 3. The UI module (`src/Atrium.Modules.Widget`)

A module is a **Razor Class Library** (`Microsoft.NET.Sdk.Razor`) that implements one `IModule`
([ADR-0001](../adr/0001-modular-monolith.md)). The host discovers it by reflection — **no host edits**.

### 3.1 Project + references

Model on `src/Atrium.Modules.Storefront/Atrium.Modules.Storefront.csproj`: `net10.0`, references to
`Atrium.Abstractions`, `Atrium.Design`, `Atrium.Contracts`, and the component/http packages. Add it to
`Atrium.slnx`.

### 3.2 The `IModule` implementation

Copy the shape of `src/Atrium.Modules.Storefront/StorefrontModule.cs`: `Name`, `Description`,
`BasePath` (the route prefix the module's pages own, e.g. `/widget`), an optional `Accent` hex,
`NavItems`, and `RegisterServices(...)` — which registers the module's typed HTTP client(s) pointed at
the **gateway** (never a service directly):

```csharp
services.AddHttpClient<WidgetClient>(client => client.BaseAddress = new Uri("https+http://gateway"));
```

`IModule` is defined in `src/Atrium.Abstractions/IModule.cs`; discovery is
`src/Atrium.Portal/Modularity/ModuleLoader.cs` (scans `Atrium.Modules.*.dll`). The host references the
module project but names nothing in it.

### 3.3 The typed HTTP client — token attach + graceful expiry

Model on `src/Atrium.Modules.Storefront/Catalog/CatalogClient.cs` (or `Orders/OrdersClient.cs`). The
client takes `HttpClient`, `AccessTokenHolder` (from `Atrium.Design`), and `ILogger<T>`, then calls
the shared `http.SendForJsonAsync<T>` extension (`src/Atrium.Design/HttpClientExtensions.cs`), which
attaches the token, logs non-2xx responses, and — critically — calls `ThrowIfSessionExpired()` **before**
`EnsureSuccessStatusCode()`:

```csharp
public sealed class WidgetClient(HttpClient http, AccessTokenHolder tokens, ILogger<WidgetClient> logger)
{
    public Task<IReadOnlyList<WidgetDto>> GetWidgetsAsync(CancellationToken ct = default) =>
        http.SendForJsonAsync<IReadOnlyList<WidgetDto>>(HttpMethod.Get, "widget/widgets", tokens, logger, ct: ct);

    public Task<WidgetDto> CreateAsync(CreateWidgetRequest request, CancellationToken ct = default) =>
        http.SendForJsonAsync<WidgetDto>(HttpMethod.Post, "widget/widgets", tokens, logger, request, ct);
}
```

Why `AccessTokenHolder` and not a factory-registered `DelegatingHandler`: `IHttpClientFactory` builds
handler chains in a **separate DI scope**, so a factory-registered handler reading the scoped holder
always sees an empty token — see [ADR-0004](../adr/0004-token-propagation-and-option-b.md).
(Exception: the AG-UI chat client can't call `request.Authorize` inline — its `HttpClient` is internal.
`BearerTokenHandler` is composed in circuit scope instead, bypassing the factory scope problem —
[ADR-0011](../adr/0011-circuit-scoped-bearer-handler.md).)
`SessionExpiredException` / `ThrowIfSessionExpired()` live in `Atrium.Design`; the shell's
`SessionErrorBoundary` turns them into a "sign in again" panel
([ADR-0008](../adr/0008-graceful-session-expiry-handling.md)).

### 3.4 Pages and UI reuse

Put routable components under `Pages/` (e.g. `Pages/Widgets.razor` with `@page "/widget"`). **Reuse the
design system** — pull primitives from `Atrium.Design` (`Button`, `Badge`, `PageHeader`,
`Field`, `Notice`, `ToastHost`, `Dialog`) and the tokens in `src/Atrium.Design/wwwroot/css/tokens.css`; do not hand-roll
CSS or hard-code colors. Reference pages: `src/Atrium.Modules.Storefront/Pages/Shop.razor`,
`CartPage.razor`, `OrdersPage.razor`. (The **atrium-ui** skill enforces this — invoke it for any UI
work.)

### 3.5 Route registration is automatic

Because the module assembly is discovered and its assemblies are registered in **both**
`Routes.razor` (`AdditionalAssemblies="Catalog.Assemblies"`) and `Program.cs`
(`.AddAdditionalAssemblies([.. moduleCatalog.Assemblies])`), your `@page` routes resolve with no host
change. This two-places rule is the gotcha in [ADR-0001](../adr/0001-modular-monolith.md) — the host
already does it generically, so you get it for free by being an `Atrium.Modules.*` project reference on
the Portal.

### 3.6 Reference the module from the Portal

The Portal must have a **project reference** to your module so its DLL lands in the app directory for
`ModuleLoader` to find. Add `src/Atrium.Modules.Widget/Atrium.Modules.Widget.csproj` as a
`<ProjectReference>` in `src/Atrium.Portal/Atrium.Portal.csproj` (this is the one "wiring" edit the host
needs, and it names no types).

---

## 4. The gateway route (`src/Atrium.Gateway/appsettings.json`)

YARP fronts every service ([ADR-0003](../adr/0003-yarp-keycloak-auth.md)). Add a route + cluster under
`ReverseProxy` in `src/Atrium.Gateway/appsettings.json`, mirroring the existing `catalog` / `storefront`
entries. The destination address is the **logical Aspire service name**, resolved by service discovery
(no ports):

```jsonc
"Routes": {
  "widget": { "ClusterId": "widget", "Match": { "Path": "/widget/{**catch-all}" } }
},
"Clusters": {
  "widget": { "Destinations": { "widget": { "Address": "https+http://widget" } } }
}
```

The gateway itself (`src/Atrium.Gateway/Program.cs`) is pure config-driven YARP — **no code change**. It
forwards the `Authorization` header untouched; auth is enforced by the target service (step 1.5), not
the gateway.

---

## 5. Aspire wiring (`src/Atrium.AppHost/apphost.cs`)

Register the new service, its database, and its references in the single-file AppHost. Follow the
existing `catalog` / `storefront` blocks in `src/Atrium.AppHost/apphost.cs`:

1. **Add the project reference** at the top: `#:project ../Atrium.Services.Widget/Atrium.Services.Widget.csproj`.
2. **Add the database** on the shared SQL Server: `var widgetDb = sql.AddDatabase("widgetdb");` (the name
   must match `AddSqlServerClient("widgetdb")` in step 1.5).
3. **Register the service** with its DB, Keycloak, waits, and health check:

```csharp
var widget = builder.AddProject<Projects.Atrium_Services_Widget>("widget")
    .WithReference(widgetDb)
    .WithReference(keycloak)
    .WaitFor(widgetDb)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/health");
```

   If this vertical **calls a core** (e.g. Catalog), add `.WithReference(catalog)` so
   `https+http://catalog` resolves — exactly what the Storefront block does.
4. **Wire it into the gateway**: add `.WithReference(widget)` and `.WaitFor(widget)` to the `gateway`
   project block, so the gateway can discover the destination from step 4.

The Portal already references the gateway and Keycloak; because your **module** is a project reference on
the Portal (step 3.6), no Portal block change is needed here.

---

## 6. Auth — Keycloak realm, roles, token propagation

Identity is Keycloak ([ADR-0003](../adr/0003-yarp-keycloak-auth.md)), imported from
`src/Atrium.AppHost/realms/realm-export.json` on startup (fixed port 8080). What a new vertical must do:

- **Validate the shared `atrium` audience.** `AddAtriumJwtAuth()` (step 1.5) sets `Audience = "atrium"`
  and the load-bearing claim-mapping options (`MapInboundClaims = false`, `RoleClaimType = "role"`)
  that prevent the "403-for-everyone" trap (see [ADR-0003](../adr/0003-yarp-keycloak-auth.md)). Because
  these settings live in `Atrium.ServiceDefaults` they can't drift per host. The realm's `atrium-audience`
  mapper stamps that audience on every access token, making one token acceptable by every service and the
  cross-service **bearer relay** work ([ADR-0005](../adr/0005-slice-calls-core.md)).
- **Roles:** `AddAtriumJwtAuth()` also registers the `admin` policy (requires the `admin` realm role).
  Roles/users (`admin`, `user`, `customer`) live in `realm-export.json`; add a new role there if your
  vertical needs one. **Note:** `WithRealmImport` only *creates* missing resources, so editing the realm
  requires wiping the Keycloak data volume to re-import.
- **Token propagation** ([ADR-0004](../adr/0004-token-propagation-and-option-b.md)): the Portal captures
  the access token in `OnTokenValidated` as a claim (`src/Atrium.Portal/Program.cs`); `MainLayout`
  copies it into the scoped `AccessTokenHolder` (`src/Atrium.Portal/Components/Layout/MainLayout.razor`);
  your typed client reads it (step 3.3). For a **slice→core** call, the service instead relays the
  caller's bearer from `IHttpContextAccessor` — reference
  `src/Atrium.Services.Storefront/Catalog/StorefrontCatalogClient.cs` (reads the incoming
  `Authorization` header, forwards it, then `EnsureSuccessStatusCode()`).

---

## 7. The test gate (`tests/`)

Three suites (all must stay green):

- **Unit tests** (`tests/Atrium.UnitTests`, no Docker) — extract branching **business logic into pure
  functions** and test them directly, no repository ([ADR-0007](../adr/0007-feature-folders-and-repository-testing.md)).
  References: `OrderPricingTests.cs` (tests `Orders/OrderPricing.cs`), `SalesReportBuilderTests.cs`,
  `CartServiceTests.cs`, `SessionExpiredTests.cs`. Do the same for your vertical's pure logic.
- **Integration tests** (`tests/Atrium.IntegrationTests`, needs Docker) — test the **real repository
  against a real SQL Server** via Testcontainers, not a mock. The shared `SqlServerFixture.cs` spins up
  one container and hands out a per-test database; your test class provisions its own DB with
  `DatabaseInitializer.Initialize`, then exercises the concrete repository (real sprocs, Dapper,
  Mapperly). References: `CatalogRepositoryTests.cs`, `OrderRepositoryTests.cs`. Add a
  `WidgetRepositoryTests` in the `[Collection(SqlServerCollection.Name)]` collection.
- **Evals** (`tests/Atrium.Evals`, needs Ollama at `http://localhost:11434`) — LLM quality scores for
  the Support agent. These tests self-skip when Ollama is unreachable, so CI stays green without a GPU.

The integration project (`tests/Atrium.IntegrationTests/Atrium.IntegrationTests.csproj`) references the
service projects under test — add a `<ProjectReference>` to `Atrium.Services.Widget` there so its
embedded sprocs are available to the tests.

---

## 8. Verify it works

From the repo root (`/Users/ted/code/Atrium`):

1. **Format + build** (0 warnings expected):
   ```bash
   dotnet csharpier format . && dotnet build Atrium.slnx -v q
   ```
2. **Test** (Docker required for integration; Ollama at `http://localhost:11434` for evals):
   ```bash
   dotnet test tests/Atrium.UnitTests/Atrium.UnitTests.csproj          # fast, no external deps
   dotnet test tests/Atrium.IntegrationTests/Atrium.IntegrationTests.csproj   # Testcontainers SQL Server
   dotnet test tests/Atrium.Evals/Atrium.Evals.csproj                  # LLM evals; skip if Ollama down
   dotnet test Atrium.slnx                                             # everything
   ```
3. **Run the stack** (Docker required):
   ```bash
   cd src/Atrium.AppHost && aspire run
   ```
   `aspire run` uses **dynamic ports** — read the Portal's URL from the Aspire dashboard/console output
   (Keycloak stays fixed at `https://localhost:8080`). Open the Portal, sign in (dev user
   `admin` / `password`), and navigate to your module's `BasePath` (e.g.
   `https://localhost:<portal-port>/widget`). Confirm: the page loads, the typed client reaches your
   service through the gateway with the bearer attached, and any role-gated write is allowed for `admin`
   and rejected otherwise.

You now have a new vertical: service → contracts → module → gateway route → Aspire → auth → tests, each
mirroring the Storefront/Catalog reference implementation.
