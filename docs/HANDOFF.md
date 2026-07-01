# Atrium — handoff / resume point

**Read this first, then `docs/ARCHITECTURE.md` for how it fits together, `docs/adr/` for why, and
`docs/ATRIUM-PLAN.md` for the original design.** This file is the "where we are and how to pick up"
note. Last updated after **Phase 7 + a service reorg** (2026-07-01).

## ▶ Start here (this session)

**Service reorg (`6b59938`) is now browser-verified** — the smoke flows below were driven by hand on
2026-07-01 and looked good (storefront add→cart→order, Reports composed data, Admin inline-edit render,
narrow-screen responsive). Nothing is pending; the build is at a clean stopping point.

Candidate next work (all optional — pick per available time):
- **Token-store option "B"** — remove the access token from the auth cookie via a small server-side
  session-keyed store, surfaced to the circuit by a scoped service (see Known limitations / ADR-0004).
- **`docs/BEYOND-THE-DEMO.md` items** — other verticals, Orders→core, polyrepo + contract-NuGet,
  gateway route self-registration, prod service discovery, independent-UI deploy.

<details><summary>Original smoke-test steps (kept for reference; already done by hand)</summary>

1. **Start the stack** (Docker must be running): `cd src/Atrium.AppHost && aspire run`. Wait ~1–2 min,
   then find the Portal's **https** port: `lsof -iTCP -sTCP:LISTEN -P -n | grep Atrium.Po` and probe
   with `curl -k` (dynamic ports change every run).
2. **Log out first, then log in** — an old cookie carries a dead token after a restart and 500s the
   module pages (see Known limitations). Navigate to `/account/logout`, then to `/admin`, and sign in
   as `admin` / `password` at Keycloak (`https://localhost:8080`).
3. **Walk the flows** and confirm no 500s / correct render:
   - Storefront → **add 2 items** → click the in-app **Cart** link (NOT `page.goto` — the cart is
     circuit-scoped, so a full navigation starts a fresh empty circuit) → **Place order** → lands on
     Orders with the order → **Reports** shows composed category data → **Admin**, click **Edit** on a
     row and confirm Save/Cancel don't overlap the Blurb cell (that was a bug fixed in `0a5c75e`).
   - Resize to ~420px: sidebar collapses to a "Menu" drawer; wide tables scroll **within** their
     container (no page-level horizontal scroll).

</details>

## TL;DR

Atrium is a modular-monolith **Blazor Server portal** (rebuild of CozenDemo, which still lives at
`/Users/ted/code/CozenDemo` as reference only). A host shell discovers self-contained UI **modules**
(RCLs) via an `IModule` contract, fronted by a **YARP gateway** with a **Catalog core service** and a
**Storefront app vertical** (its own DB), authenticated by **Keycloak**. Backend is **Dapper + stored
procedures + DbUp + Mapperly** (no EF), orchestrated by **Aspire**.

## Status: Phases 0–7 done + service reorg, committed & browser-verified. No work pending (see ▶ above).

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
| 6 Docs (ARCHITECTURE + 6 ADRs + BEYOND-THE-DEMO) | ✅ | `653911d` |
| 7 Tests (curated 3-unit + 2-integration suite) | ✅ | `b0c1035` |
| 7 Polish (responsive/focus/loading + 2 bug fixes) | ✅ | `0a5c75e` |
| Service reorg (feature folders + ADR-0007) | ✅ (browser-verified 2026-07-01) | `6b59938` |

(The TaskList tool is session-scoped — it starts empty each session; recreate tasks for the phase you pick up.)

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

**UI-only quick look** (the Home shell renders without a backend; module pages — Storefront/Admin/Reports —
require the full stack and will redirect to login / 401 without it):
```
cd /Users/ted/code/Atrium/src/Atrium.Portal && dotnet run --launch-profile http   # http://localhost:5035
```

**Build / format** (csharpier check runs on build, so format first):
```
cd /Users/ted/code/Atrium && dotnet csharpier format . && dotnet build Atrium.slnx -v q
```

**Test.** Unit tests need nothing; integration tests spin a real SQL Server via Testcontainers, so
**Docker must be running** for them.
```
dotnet test tests/Atrium.UnitTests/Atrium.UnitTests.csproj          # 16 fast tests, no Docker
dotnet test tests/Atrium.IntegrationTests/Atrium.IntegrationTests.csproj   # 4 tests, needs Docker (~11s)
dotnet test Atrium.slnx                                              # everything (20 total)
```

## Solution layout (all under `src/`)

- `Atrium.Portal` — Blazor Server host: module discovery, shell, OIDC login, token capture.
- `Atrium.Abstractions` — `IModule` + `NavItem` contract.
- `Atrium.Design` — design-system RCL: `tokens.css` + `atrium.css`, primitives (Button/Card/Badge/
  PageHeader/Field/ToastHost/**Dialog**), `AccessTokenHolder`, `SessionExpiredException`. `Dialog` is a
  native-`<dialog>`/`showModal()` modal (scoped CSS + `wwwroot/js/dialog.js`; two-way `Open`).
- `Atrium.Contracts` — DTOs (Product/Category/Order).
- `Atrium.Modules.Storefront` — Storefront UI module (Shop, Cart, Orders; CatalogClient, OrdersClient,
  CartService). Amber accent.
- `Atrium.Modules.Admin` — back-office products table; create + edit happen in a shared modal `Dialog`
  (AdminCatalogClient → Catalog writes). Indigo accent. Writes are admin-role gated server-side; the page
  is view-any/write-admin.
- `Atrium.Modules.Reports` — sales analytics: stat cards + CSS-drawn bar chart (ReportsClient →
  Storefront `/reports/sales`). Violet accent. (Hello module removed — three real modules now prove discovery.)
- `Atrium.Services.Catalog` — core service: Dapper/sprocs/DbUp/Mapperly, JWT-secured. Product reads for
  all; `POST`/`PUT /catalog/products` gated on the `admin` policy. Internals in a `Catalog/` feature
  folder (namespace `…Catalog.Catalog`); DbUp under `Data/`.
- `Atrium.Services.Storefront` — app vertical: own DB (orders), calls Catalog, JWT-secured. Adds a
  `/storefront/reports/sales` aggregate that composes Catalog for the product→category map. Internals
  organized by feature — `Orders/`, `Reports/`, `Catalog/` (the shared slice→core client), `Data/` —
  with namespaces nested per folder. **See ADR-0007** for the reorg + repository-testing rationale.
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
  catalog returns 401. This is now handled **gracefully**: clients map the 401 to a typed
  `SessionExpiredException` and the shell's `SessionErrorBoundary` shows a "session expired — sign in
  again" panel instead of crashing the circuit (was: unhandled-exception / 500). Expiry itself is still
  unfixed — prod fix: `Duende.AccessTokenManagement`.
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

## Phase 6 done — what landed

The "the rest" docs from `ATRIUM-PLAN.md` §"The rest", written as three deliverables:
- **`docs/ARCHITECTURE.md`** — consolidated topology: request flow, solution layout, data recipe, auth
  model, and a "where the bodies are buried" index pointing at the ADRs.
- **`docs/adr/`** — six short ADRs recording decisions already made: 0001 modular monolith,
  0002 Dapper/sprocs/DbUp, 0003 YARP + Keycloak, **0004 token-in-claim + the token-store option "B"**,
  0005 slice-calls-core, 0006 shared-contracts-then-NuGet. (README.md indexes them.)
- **`docs/BEYOND-THE-DEMO.md`** — the six deliberately-not-built items (other verticals, Orders→core,
  polyrepo + contract-NuGet, gateway route self-registration, prod service discovery, independent-UI
  deploy), each shown growing additively out of what exists.
- Also removed stale `src/Atrium.Modules.Hello/` build cruft (source was already gone).

## Phase 7 tests done — what landed

A deliberately **curated** suite (not a port of CozenDemo's SqlKata tests, which don't apply — Atrium
uses sprocs). **Five tests, each a distinct concept**, split fast-unit vs slow-integration:

- **`tests/Atrium.UnitTests`** (15 cases, no Docker):
  - **U1 `CartServiceTests`** — the circuit-scoped cart as an in-memory state machine (add/increment,
    set-qty-0 removes, Total/Count math).
  - **U2 `SalesReportBuilderTests`** — pure sales aggregation: category bucketing, `"Other"` fallback,
    revenue-desc ordering, rolled-up totals.
  - **U3 `OrderPricingTests`** — server-authoritative pricing (price comes from the catalog, never the
    request) + guards (empty / unknown product / non-positive qty).
- **`tests/Atrium.IntegrationTests`** (4 cases, **needs Docker** — Testcontainers SQL Server):
  - **I1 `CatalogRepositoryTests`** — real DB path: DbUp provisions schema+seed+sprocs, `CatalogRepository`
    runs the sprocs via Dapper, Mapperly maps the row back; also asserts the `THROW 50001` category
    error path surfaces as a `SqlException`.
  - **I2 `OrderRepositoryTests`** — the multi-sproc write **transaction** (header + N lines committed
    together) and the read that regroups flat header×line rows back into one `OrderDto`.
- **Refactor for testability:** extracted two pure functions from minimal-API endpoints —
  `SalesReportBuilder` (from `ReportsEndpoints`) and `OrderPricing` (from `OrdersEndpoints`) — behavior
  unchanged, now unit-testable without HTTP/DB. Shared container via a collection fixture
  (`SqlServerFixture`); each integration class provisions its own database on it.
- **Verified:** `dotnet test Atrium.slnx` → 19/19 pass (unit 15, integration 4 in ~11s); full
  `dotnet build` clean, 0 warnings.

## Phase 7 polish done — what landed (`0a5c75e`)

Responsive / focus / loading pass across the three modules, all via shared design-system tokens, plus
two **pre-existing** bugs the Playwright smoke surfaced:
- **Loading consistency:** Orders + Admin now show skeletons (shared `.skeleton` + `.skeleton-line`)
  instead of "Loading…" text, matching Shop/Reports. Cart/Admin tables wrapped in `.table-scroll` so
  they scroll within their container on narrow screens; admin create form collapses to 1 column <620px.
- **"Place order" double-submit guard** (`_placing` in-flight state).
- **Bug: admin inline-edit row overlap** — the `width:1%` actions column collapsed and the Save/Cancel
  buttons overflowed onto the Blurb cell. Fixed with `min-width:max-content` on `.admin-table__actions`.
- **Bug: Reports 500 on duplicate product names** — `/storefront/reports/sales` built
  `ToDictionary(p => p.Name)` which threw when two products shared a name (names aren't unique; orders
  reference products by name). Fixed via a deduping `SalesReportBuilder.CategoryByProductName` helper
  (first wins) + a regression unit test. Found because the admin created a duplicate "Walnut Monitor
  Shelf" during the smoke.

## Service reorg done — what landed (`6b59938`)

Both backend services were flat (all files in the project root). Reorganized internals **by feature**
(vertical slices), namespaces nested per folder — Storefront `Orders/` · `Reports/` · `Catalog/`
(shared slice→core client) · `Data/`; Catalog `Catalog/` · `Data/`. Repository interfaces **kept**
(DIP + convention + optionality); `DatabaseInitializer` left duplicated over a shared lib. Pure move —
git renames, 20/20 tests green. Reasoning in **ADR-0007** (incl. the "why integration-test repos
instead of mocking" writeup). **Not yet driven in a browser** → that's the ▶ Start-here task above.

## Graceful session-expiry handling — what landed

An idle circuit outlived its ~5-min access token, and the next action (e.g. an Admin **Save**) hit a
**401** that fell through to the generic "An unhandled error has occurred" overlay and killed the
circuit. Fix (no refresh — just graceful handling of expiry; see ADR-0004):
- New `SessionExpiredException` + `HttpResponseMessage.ThrowIfSessionExpired()` in `Atrium.Design`.
- All four typed clients (`CatalogClient`, `OrdersClient`, `ReportsClient`, `AdminCatalogClient`) map a
  401 to that typed signal before `EnsureSuccessStatusCode`. 403 (wrong role) still shows an inline toast.
- New shell-level `SessionErrorBoundary` (custom `ErrorBoundary`) around `@Body` in `MainLayout` renders
  a "session expired — sign in again" panel for it, a generic card + server-side log for anything else,
  and `Recover()`s on navigation.
- Tests: `SessionExpiredTests` (U4) — 401→`SessionExpiredException`, 500→still `HttpRequestException`.
  Unit 18/18 green (2 new), build 0 warnings. **Browser-verify pending:** the panel render on a live 401 needs a
  stack restart on the new build + a forced/waited expiry (unit tests cover the client mapping only).

Workflow reminder (from prior phases): write code → `dotnet build` → run via `aspire run` → verify with
Playwright → `code-simplifier`/`/code-review` pass → commit per phase (Co-Authored-By trailer).
Aesthetic direction via the `frontend-design` skill; consistency via the `.claude/skills/atrium-ui` skill.
