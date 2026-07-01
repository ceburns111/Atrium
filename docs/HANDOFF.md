# Atrium — handoff / resume point

**Read this first, then `docs/ATRIUM-PLAN.md` for the full design.** This file is the "where we are and
how to pick up" note. Last updated after **Phase 4** (2026-07-01).

## TL;DR

Atrium is a modular-monolith **Blazor Server portal** (rebuild of CozenDemo, which still lives at
`/Users/ted/code/CozenDemo` as reference only). A host shell discovers self-contained UI **modules**
(RCLs) via an `IModule` contract, fronted by a **YARP gateway** with a **Catalog core service** and a
**Storefront app vertical** (its own DB), authenticated by **Keycloak**. Backend is **Dapper + stored
procedures + DbUp + Mapperly** (no EF), orchestrated by **Aspire**.

## Status: Phases 0–4 done, committed. Next: Phase 5.

| Phase | State | Commit |
|---|---|---|
| 0 Skeleton | ✅ | `25eb940` |
| 1 IModule platform | ✅ | `b5a6ca4` |
| 2 Design system + homepage | ✅ | `03e780f` |
| 3 Storefront UI (mock) | ✅ | `27745eb` |
| 4a Catalog + gateway + Aspire | ✅ | `5617961` |
| 4b Keycloak OIDC + secured catalog + token propagation | ✅ | `c1e73d6` |
| 4c Storefront vertical (own DB) + slice→core | ✅ | `cb0f5c4` |
| 5 Admin + Reports modules (admin-role writes, real reports) | ✅ | `3d40061` |
| 6 Docs ("the rest") | ▢ next | — |
| 7 Tests + polish | ▢ | — |

Progress is also tracked in the task list (TaskList tool): Phase 5 = task #6, Phase 6 = #7, Phase 7 = #8.

## How to run

**Full stack (needed for Storefront/auth)** — requires Docker running:
```
cd /Users/ted/code/Atrium/src/Atrium.AppHost && aspire run
```
Aspire assigns **dynamic ports each run**. Find the Portal:
```
lsof -iTCP -sTCP:LISTEN -P -n | grep Atrium.Po    # the https port is the Portal
```
Open `https://localhost:<portal-port>/`. Keycloak is at `https://localhost:8080` (fixed).
**Login:** `testuser` / `password` (customer) or `admin` / `password` (admin). The Aspire dashboard URL
prints in the `aspire run` output.

**UI-only quick look** (Home + Hello render without a backend; Storefront will 401 without the stack):
```
cd /Users/ted/code/Atrium/src/Atrium.Portal && dotnet run --launch-profile http   # http://localhost:5035
```

**Build / format** (csharpier check runs on build, so format first):
```
cd /Users/ted/code/Atrium && dotnet csharpier format . && dotnet build Atrium.slnx -v q
```

## Solution layout (all under `src/`)

- `Atrium.Portal` — Blazor Server host: module discovery, shell, OIDC login, token capture.
- `Atrium.Abstractions` — `IModule` + `NavItem` contract.
- `Atrium.Design` — design-system RCL: `tokens.css` + `atrium.css`, primitives (Button/Card/Badge/
  PageHeader/Field/ToastHost), `AccessTokenHolder`.
- `Atrium.Contracts` — DTOs (Product/Category/Order).
- `Atrium.Modules.Storefront` — Storefront UI module (Shop, Cart, Orders; CatalogClient, OrdersClient,
  CartService). Amber accent.
- `Atrium.Modules.Admin` — back-office products table with inline edit + create (AdminCatalogClient →
  Catalog writes). Indigo accent. Writes are admin-role gated server-side; the page is view-any/write-admin.
- `Atrium.Modules.Reports` — sales analytics: stat cards + CSS-drawn bar chart (ReportsClient →
  Storefront `/reports/sales`). Violet accent. (Hello module removed — three real modules now prove discovery.)
- `Atrium.Services.Catalog` — core service: Dapper/sprocs/DbUp/Mapperly, JWT-secured. Product reads for
  all; `POST`/`PUT /catalog/products` gated on the `admin` policy.
- `Atrium.Services.Storefront` — app vertical: own DB (orders), calls Catalog, JWT-secured. Adds a
  `/storefront/reports/sales` aggregate that composes Catalog for the product→category map.
- `Atrium.Gateway` — YARP reverse proxy + service discovery.
- `Atrium.AppHost` — single-file Aspire (`apphost.cs`, run with `aspire run`).

## How the tricky bits work (so you don't relearn them)

- **Module discovery:** `Atrium.Portal/Modularity/ModuleLoader` scans `Atrium.Modules.*` assemblies for
  `IModule`. The host references modules but names none. Routing needs assemblies in **two** places:
  `<Router AdditionalAssemblies>` (Routes.razor) AND `MapRazorComponents().AddAdditionalAssemblies()`
  (Program.cs) — the second is what makes deep-links / SSR resolve module pages.
- **Blazor Server token propagation:** the shell (`MainLayout`) reads the access token from the cascading
  auth state and stashes it in a **scoped** `AccessTokenHolder`. Typed clients (`CatalogClient`,
  `OrdersClient`) read the holder and attach `Bearer`. Do **not** use a `DelegatingHandler` for this —
  `IHttpClientFactory` resolves handlers in a **separate scope**, so the holder would be empty (this bit
  us). The access token is stored as a claim via OIDC `OnTokenValidated`.
- **Slice→core:** `Atrium.Services.Storefront` calls Catalog service-to-service (`https+http://catalog`)
  and **relays the caller's bearer** via `IHttpContextAccessor` (valid in a normal API, unlike Blazor).
- **Auth model:** browse/checkout require login; Catalog + Storefront require a valid Keycloak JWT with
  the shared `atrium` audience (realm custom-audience mapper). Portal is a confidential OIDC client
  (`dev-portal-secret`, passed via AppHost env, matches the realm).
- **Data:** DbUp two lanes — `Data/Scripts/Migrations/*` run once (schema+seed), `Programmability/*`
  run always (`CREATE OR ALTER` sprocs). SQL is embedded (`EmbeddedResource`). Mapperly maps rows→DTOs.

## Known limitations (intentional for a demo; document in Phase 6)

- **No token refresh.** The access token is captured at login (no refresh). After it expires (~5 min) the
  catalog returns 401 and the storefront page 500s. Prod fix: `Duende.AccessTokenManagement`.
- **Stale cookie across restarts.** Cookies are per-host (not per-port); an old Portal cookie carrying a
  dead token can 500 the storefront after an Aspire restart. Workaround: hit `/account/logout` (or clear
  cookies) then log in again. Wiping the Keycloak data volume invalidates old sessions/tokens too.
- **Realm changes need a volume reset.** `WithRealmImport` only creates missing resources; to re-import a
  changed realm run: `docker volume ls -q | grep keycloak | xargs docker volume rm` (after stopping Aspire).
- **Tokens-in-the-cookie is a deliberate demo shortcut (an architecture smell we accept for now).**
  A Blazor Server *circuit* has no `HttpContext` (it only exists for the initial request that opens the
  SignalR connection), so a component can't call `GetTokenAsync` to reach a token. The workaround: park
  the raw access token as a **custom claim** (`OnTokenValidated`) so it rides inside the `ClaimsPrincipal`
  into the circuit, where `MainLayout` copies it into the scoped `AccessTokenHolder` for the typed clients.
  This means the token (a *credential*) travels in the auth **cookie** — mild size bloat, no refresh, and a
  conflation of identity with credentials. `SaveTokens = true` is **not** redundant: it's what lets the
  OIDC handler send `id_token_hint` on RP-initiated logout (Keycloak 18+ otherwise shows a "confirm logout"
  interstitial). The only true duplication is that the *access token specifically* is stored twice (once in
  the `SaveTokens` properties, once as the claim), because logout and the circuit each need it in a
  different place. **Preferred replacement if time allows (option "B"):** a small server-side token store —
  capture tokens in `OnTokenValidated` into a session-keyed store (or `ITicketStore`), keep the cookie down
  to a session id, and surface the current token to the circuit via a scoped service. That removes the
  token from the cookie without pulling in the full `Duende.AccessTokenManagement` (the eventual prod path,
  which also adds refresh — see "No token refresh" above).

## Gotchas that cost time (avoid re-hitting)

- A routable component whose class name equals an injected member triggers **CS0542**. That's why the
  order/cart pages are `CartPage.razor` / `OrdersPage.razor` (routes set by `@page`, unaffected).
- DbUp 7.x has `LogToConsole()`, not `LogToAutodetectedLog()`.
- `aspire run` uses dynamic ports; always re-discover via `lsof`. Keycloak stays on 8080.
- **Role-based auth needs `MapInboundClaims = false`.** The realm's role mapper puts a flat `role`
  claim in the access token, and Catalog sets `RoleClaimType = "role"`. But JWT-bearer's default
  `MapInboundClaims = true` renames the inbound `role` claim to the long `ClaimTypes.Role` URI, so
  `RequireRole("admin")` (which matches on `RoleClaimType`) finds nothing → **403 for everyone, admins
  included**. Fix: set `options.MapInboundClaims = false` (Catalog `Program.cs`), matching the Portal.
- Debugging that took a token dump: temporarily `Console.WriteLine(http.User.Claims…)` in an already
  authorized endpoint, hit it, and read the DCP `*_out` file under
  `$TMPDIR/aspire-dcp*/` — service stdout isn't in `~/.aspire/logs` (those are CLI logs), and default
  `Microsoft.AspNetCore=Warning` hides the authorization-failure line.

## Phase 5 done — what landed

Three real modules now prove "N apps, one host" (Storefront, Admin, Reports); Hello was removed.
- **Admin** writes go to the Catalog core: `usp_Product_Create`/`usp_Product_Update` (run-always sprocs
  that SELECT the affected row back), `ICatalogRepository.Create/UpdateProductAsync`, `POST`/`PUT
  /catalog/products` gated on the `admin` policy. UI is an editable `.atrium-table` with an inline
  create form. `AdminCatalogClient` maps a 403 to a friendly toast rather than throwing.
- **Reports** are real: `usp_Report_SalesByProduct` + `usp_Report_OrderCount` in the Storefront DB,
  aggregated into `SalesReportDto`; the endpoint composes Catalog (product→category map) to bucket sales
  by category — the same "slice calls core" relay used for pricing. UI = stat cards + CSS bar chart.
- **Verified** end-to-end via Playwright + service logs: admin read/write both `200` (edit persisted);
  testuser read `200`, write `403`; Reports rendered real composed data.

## Picking up Phase 6 (Docs — "the rest")

Write the docs called out in `ATRIUM-PLAN.md` §"The rest": other verticals (Admin/Reports APIs + DBs),
promoting Orders to its own core service, polyrepo + contract-NuGet, prod service discovery,
independent-UI-deploy options, short ADRs. Consider capturing the token-store option "B" (see Known
limitations) as one of the ADRs.

Workflow reminder (from prior phases): write code → `dotnet build` → run via `aspire run` → verify with
Playwright → `code-simplifier`/`/code-review` pass → commit per phase (Co-Authored-By trailer).
Aesthetic direction via the `frontend-design` skill; consistency via the `.claude/skills/atrium-ui` skill.
