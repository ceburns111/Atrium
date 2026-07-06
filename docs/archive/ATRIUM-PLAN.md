# Atrium — modular Blazor Server platform (rebuild plan)

> Rebuild of this demo as a **modular-monolith Blazor Server portal** ("Atrium") that loads
> self-contained UI modules via a discovery contract, fronted by a YARP gateway with one backend
> vertical built end-to-end. This file is the source of truth for the rebuild.

## What we're demonstrating

1. **Modular UI host** — a Blazor Server shell ("the Atrium") with a homepage that links to each app.
   New UIs are dropped in as **referenced Razor Class Libraries** auto-discovered via an `IModule`
   contract. Adding a module = one project reference + zero host code changes.
2. **Flexible backend for autonomous teams** — each UI can be paired with its own app API (a BFF)
   that owns an app-specific DB and calls shared **core services** for shared domain data. Built for
   real for **one vertical** (Storefront); the rest is documented.
3. **Gateway pattern** — a YARP gateway is the single ingress the portal's server-side calls hit;
   it routes to the app/core services.
4. **Clean, non-default UI** — MudBlazor dropped in favour of a small hand-rolled design-system RCL
   (CSS tokens, app shell, ~7 primitives) that reads as deliberately designed, not framework-default.

## Decisions (locked)

| Question | Decision |
|---|---|
| Drop-in mechanism | Referenced RCLs auto-discovered via `IModule` contract (not runtime folder-drop) |
| Example modules | E-commerce split: **Storefront / Admin / Reports**, built **fresh** |
| Backend scope | **One vertical built end-to-end + gateway**; the rest documented |
| Auth | **Keycloak** stays — the shared identity core service every slice trusts |
| UI | Drop MudBlazor; hand-rolled design system, ROI-scoped |
| Naming | **Atrium** brand; functional app names; old `Cozen*` names retired |

## Target topology

```
browser ──SignalR──▶ Atrium.Portal (Blazor Server shell)
                       ├─ discovers IModule RCLs, builds homepage + nav
                       ├─ server-side OIDC (Keycloak)  ← BFF dissolves into the server
                       └─ module services ──HTTP(+bearer)──▶ Atrium.Gateway (YARP)
                                                              ├─▶ Atrium.Services.Storefront ─▶ Storefront DB   (built vertical)
                                                              └─▶ Atrium.Services.Catalog (core) ─▶ atriumdb
                                  Services.Storefront ──HTTP──▶ Services.Catalog (slice calls core)
Keycloak = shared identity core · Atrium.AppHost (Aspire) orchestrates all
```

### Old → new mapping

| Today | Becomes |
|---|---|
| CozenWeb (WASM) | retired → `Atrium.Portal` (Blazor Server) + module RCLs |
| CozenBff | split: OIDC → Portal · YARP proxy → `Atrium.Gateway` |
| CozenApi | `Atrium.Services.Catalog` (+ Orders) core service (shared) |
| — (new) | `Atrium.Services.Storefront` — app vertical, own DB, calls core |
| CozenShared | `Atrium.Contracts` (docs note: would be versioned NuGet per consumer) |
| CozenHost (Aspire) | `Atrium.AppHost` |
| MudBlazor | dropped → `Atrium.Design` |
| CozenApiTests | `Atrium.Services.Catalog.Tests` |

### Solution layout

```
src/
  Atrium.Portal             Blazor Server shell (the host)
  Atrium.Abstractions       IModule contract + nav model
  Atrium.Design             design-system RCL (tokens, app shell, primitives)
  Atrium.Gateway            YARP gateway
  Atrium.AppHost            Aspire orchestrator
  Atrium.Contracts          DTOs
  Atrium.Modules.Storefront / .Admin / .Reports     UI RCLs
  Atrium.Services.Catalog   shared core service
  Atrium.Services.Storefront   built vertical app API (+ own DB)
tests/
  Atrium.Services.Catalog.Tests
```

### The module contract

```csharp
public interface IModule {
    string Name { get; }
    string BasePath { get; }                  // "/storefront"
    IEnumerable<NavItem> NavItems { get; }     // feeds homepage + nav
    void RegisterServices(IServiceCollection services, IConfiguration cfg);
}
```

Host scans referenced assemblies for `IModule`, calls `RegisterServices`, collects `NavItems`, and
feeds the module assemblies into the Router's `AdditionalAssemblies`. Module pages declare
`@page "/storefront/..."` and `[Authorize]` as needed.

## UI / design strategy (ROI-scoped)

Signal to send: *hand-rolled, not a library; understands tokens, box model, flex/grid, states, motion.*

- **`Atrium.Design` RCL** owns everything (also reinforces the shared-shell architecture point).
- **tokens.css** — CSS custom properties: restrained palette (neutrals + one accent + semantic
  success/danger), 8px spacing scale, radius/shadow scale, type scale, transition timings. One
  cohesive theme, no theming engine.
- **App shell** via CSS grid (sidebar + topbar + content), responsive collapse — highest-visibility surface.
- **~7 primitives**, only what the modules render: `Button`, `Card`, `DataTable`, `Field`, `Badge`,
  `PageHeader`, `Toast`. Polished `:hover` / `:focus-visible` / `:active` / `:disabled` states,
  120–160ms transitions for snap.
- System font stack + skeleton/loading states for perceived speed.
- **Reports charts = CSS-drawn bars** (flex/grid), no charting dependency.

Scope discipline: small set, one theme, polish concentrated on shell + per-page components.

## Phases (each ends in a runnable checkpoint)

- **Phase 0 — Skeleton & plan doc.** Scaffold `Atrium.*` projects, drop MudBlazor, retire `Cozen*`,
  write this doc. _Checkpoint: solution builds empty._
- **Phase 1 — Platform.** `IModule` contract + host discovery + nav model; proven with a throwaway
  "hello" module. _Checkpoint: host renders a discovered module's page._
- **Phase 2 — Design system + portal homepage.** Tokens, reset, app shell, primitives; homepage of
  module cards. _Checkpoint: portal looks custom & snappy at its root URL._
- **Phase 3 — Storefront UI (mock data)** wired via `IModule`, using the design system.
  _Checkpoint: `/storefront` clean and consistent._
- **Phase 4 — Backend vertical + gateway + auth.** CozenApi→`Services.Catalog` (+Orders) core; new
  `Services.Storefront` (own DB) calling core; `Atrium.Gateway` (YARP); Portal server-side OIDC vs
  Keycloak; wire Storefront to real data; update Aspire. _Checkpoint: logged-in, end-to-end real data
  through the gateway via the Aspire dashboard._
- **Phase 5 — Admin + Reports UI modules** (against core/mock), consistent styling; homepage links to
  all three. _Checkpoint: 3 apps, one host._
- **Phase 6 — Docs ("the rest").** Topology, other verticals, polyrepo + contract-NuGet, prod service
  discovery, independent-UI-deploy options, short ADRs.
- **Phase 7 — Tests + polish.** Adapt tests; responsive/focus/loading pass.

## "The rest" — documented, not built (Phase 6 detail)

1. The other two backend verticals (Admin API + DB, Reports API + DB) — more-of-the-same; doc shows
   how each grows its own app API + DB like Storefront did.
2. Additional core services (e.g. promote Orders to its own core service when a 2nd slice needs it).
3. Repo & deploy mechanics: polyrepo split (one repo per vertical), contracts as versioned NuGet
   instead of a shared project, independent CI/CD per vertical.
4. Per-team gateway route self-registration (config-driven YARP / service discovery).
5. Production service discovery (DNS/K8s/registry + config-driven route table).
6. True independent UI-module deploy: UI-module-as-versioned-NuGet compromise; runtime folder-drop /
   micro-frontends as the heavier path if ever required.

## Patterns / terms referenced

Self-Contained Systems (SCS), Backend-for-Frontend (BFF) per module, vertical slice architecture,
modular monolith, micro-frontends, contract versioning via packages, "extract when it hurts".

## Working agreement

Per-repo convention: Claude writes the code, explains each step, and checkpoints per step with full
click-ready URLs at every run point. Favor idiomatic/clean Blazor over clever.
