# Atrium — architecture reference

The consolidated "how it fits together" doc. For the build history and how to run, see
[HANDOFF.md](HANDOFF.md); for *why* each choice was made, see the [ADRs](adr/); for what was
deliberately scoped out, see [BEYOND-THE-DEMO.md](BEYOND-THE-DEMO.md).

## One-paragraph summary

Atrium is a **modular-monolith Blazor Server portal**. A single host shell (`Atrium.Portal`)
discovers self-contained UI **modules** by reflection through an `IModule` contract — the host
references the modules but names none of them. Behind the UI, a **YARP gateway** fronts a set of
backend services split along the Self-Contained-Systems grain: a **core service** owns a capability's
data (Catalog owns products), and an **app vertical** owns its own database and composes core services
for everything else (Storefront owns orders, calls Catalog to price them). Identity is **Keycloak**
(OIDC for the Portal, JWT bearer for the services). Data access is **Dapper + stored procedures +
DbUp + Mapperly** — no EF. The whole thing is orchestrated for local dev by a single-file **Aspire**
AppHost.

## Topology

```
                                    ┌─────────────┐
                          OIDC      │  Keycloak   │   JWT (audience: atrium)
                    ┌───────────────│  :8080      │───────────────┐
                    │               └─────────────┘               │
                    ▼                                              ▼
            ┌───────────────┐   bearer   ┌─────────────┐   route   ┌──────────────────┐
   browser  │ Atrium.Portal │──────────▶ │ Atrium.     │──────────▶│ Catalog (core)   │
  ─────────▶│ Blazor Server │            │ Gateway     │  /catalog │  owns catalogdb  │
            │  host + modules│           │ (YARP)      │           └──────────────────┘
            └───────────────┘            │             │           ┌──────────────────┐
                                         │             │──────────▶│ Storefront (app) │
                                         └─────────────┘/storefront│  owns storefrontdb│
                                                                   │  ── calls Catalog │
                                                                   └────────┬─────────┘
                                                                            │ bearer relay
                                                                            ▼
                                                                   (Catalog /catalog/products)
```

- **Ingress is the gateway.** The Portal only knows the gateway address (`https+http://gateway` via
  Aspire service discovery); it never addresses Catalog or Storefront directly. YARP matches
  `/catalog/{**catch-all}` and `/storefront/{**catch-all}` to the two clusters
  (`src/Atrium.Gateway/appsettings.json`).
- **Two service shapes.** *Core* (Catalog) = owns a domain's data, no cross-service calls. *App
  vertical* (Storefront) = owns its own DB **and** composes core services. See
  [ADR-0005](adr/0005-slice-calls-core.md).
- **One database per service.** `catalogdb` and `storefrontdb` are separate databases on the shared
  SQL Server instance — no cross-database joins; Storefront gets product data over HTTP, not SQL.

## Solution layout (`src/`)

| Project | Role |
|---|---|
| `Atrium.Portal` | Blazor Server host: module discovery, app shell, OIDC login, token capture. |
| `Atrium.Abstractions` | The `IModule` + `NavItem` contract. The *only* thing the host and modules share by type. |
| `Atrium.Design` | Design-system RCL: `tokens.css` + `atrium.css`, primitives (Button/Card/Badge/PageHeader/Field/ToastHost), `AccessTokenHolder`. |
| `Atrium.Contracts` | DTOs crossing the wire (Product/Category/Order/Report). |
| `Atrium.Modules.Storefront` | Storefront UI module — Shop, Cart, Orders. Amber accent. |
| `Atrium.Modules.Admin` | Back-office products table, inline edit + create. Indigo accent. Writes are admin-gated server-side. |
| `Atrium.Modules.Reports` | Sales analytics — stat cards + CSS bar chart. Violet accent. |
| `Atrium.Services.Catalog` | **Core** service: products via Dapper/sprocs/DbUp/Mapperly, JWT-secured. |
| `Atrium.Services.Storefront` | **App vertical**: own DB (orders + reports), calls Catalog, JWT-secured. |
| `Atrium.Gateway` | YARP reverse proxy + Aspire service discovery. |
| `Atrium.AppHost` | Single-file Aspire (`apphost.cs`), run with `aspire run`. |

The host references every `Atrium.Modules.*` project but hard-codes none of them; a new module is a
project reference plus one `IModule` implementation. See [ADR-0001](adr/0001-modular-monolith.md).

## Request flow (an authenticated read)

1. Browser hits a module page (`/storefront`). The Portal is a confidential OIDC client; unauthenticated
   requests redirect to Keycloak, come back with an auth cookie.
2. `MainLayout` copies the access token out of the `ClaimsPrincipal` (where OIDC parked it) into a
   **scoped** `AccessTokenHolder`. See [ADR-0004](adr/0004-token-propagation-and-option-b.md).
3. A typed client (`CatalogClient` / `OrdersClient` / `ReportsClient`) reads the holder, attaches
   `Authorization: Bearer …`, and calls the **gateway**.
4. YARP routes to the target service. The service validates the JWT (Keycloak issuer, shared `atrium`
   audience) and authorizes by policy (`admin` for writes).
5. For an app vertical composing a core (Storefront pricing / reports), the service **relays the
   caller's bearer** to Catalog via `IHttpContextAccessor` — valid here because a normal API request
   *has* an `HttpContext` (a Blazor circuit does not). See [ADR-0005](adr/0005-slice-calls-core.md).

## Data

Both services use the same recipe (see [ADR-0002](adr/0002-dapper-sprocs-dbup.md)):

- **DbUp, two lanes.** `Data/Scripts/Migrations/*` run **once** (schema + seed);
  `Data/Scripts/Programmability/*` run **always** as `CREATE OR ALTER` (stored procedures). SQL files
  are embedded resources; `DatabaseInitializer` runs them at service startup.
- **Dapper** executes the sprocs; **Mapperly** maps rows → DTOs at compile time.
- **Catalog** (`catalogdb`): `usp_Product_GetList/Create/Update`, `usp_Category_GetList`.
- **Storefront** (`storefrontdb`): `usp_Order_Create/GetList`, `usp_OrderItem_Add`,
  `usp_Report_SalesByProduct`, `usp_Report_OrderCount`. Reports compose Catalog for the
  product→category map, then bucket sales by category.

## Auth model

- **Portal → Keycloak: OIDC** (confidential client `atrium-portal`, secret injected by the AppHost as
  `Keycloak__PortalSecret`). Browse/checkout require login.
- **Services → Keycloak: JWT bearer.** Every service requires a valid token with the shared `atrium`
  audience (a realm custom-audience mapper adds it). Reads are open to any authenticated user; writes
  (Catalog `POST`/`PUT /catalog/products`) require the `admin` policy.
- **Roles are a flat `role` claim.** Both Portal and Catalog set `MapInboundClaims = false` and
  `RoleClaimType = "role"` so `RequireRole("admin")` matches — see [ADR-0003](adr/0003-yarp-keycloak-auth.md)
  and the "403 for everyone" gotcha in HANDOFF.

## Where the bodies are buried

The non-obvious mechanics, each with a home:

- **Module routing needs assemblies in two places** — `<Router AdditionalAssemblies>` *and*
  `MapRazorComponents().AddAdditionalAssemblies()`. → [ADR-0001](adr/0001-modular-monolith.md).
- **No `DelegatingHandler` for the bearer token** — `IHttpClientFactory` resolves handlers in a
  separate scope, so the scoped holder reads empty. → [ADR-0004](adr/0004-token-propagation-and-option-b.md).
- **The access token rides in the auth cookie** as a custom claim — a deliberate demo shortcut, with a
  documented replacement (option B). → [ADR-0004](adr/0004-token-propagation-and-option-b.md).
- **Known limitations** (no token refresh, stale-cookie-after-restart, realm re-import needs a volume
  reset) live in [HANDOFF.md](HANDOFF.md) under "Known limitations".
