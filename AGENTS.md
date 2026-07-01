# AGENTS.md — orientation for working in Atrium

Start here, then follow the links. This is a **hub**, not a manual: it points at the authoritative
docs and skills rather than restating them.

## What Atrium is

A **modular-monolith Blazor Server platform** ([ADR-0001](docs/adr/0001-modular-monolith.md)). A single
host shell (`Atrium.Portal`) discovers self-contained UI **modules** by reflection through an `IModule`
contract — the host references the modules but names none of them. Behind the UI a **YARP gateway**
fronts backend services split along the Self-Contained-Systems grain: a **core service** owns a
capability's data (Catalog owns products); an **app vertical** owns its own database and composes core
services over HTTP for everything else (Storefront owns orders, calls Catalog to price them). Identity
is **Keycloak** (OIDC for the Portal, JWT bearer for the services); data access is **Dapper + stored
procedures + DbUp + Mapperly**; local dev is orchestrated by a single-file **Aspire** AppHost.

Full picture: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Project topology (`src/`)

| Project | Role |
|---|---|
| `src/Atrium.Portal` | Blazor Server host shell — OIDC login, module discovery (`Modularity/ModuleLoader.cs`), routing |
| `src/Atrium.Abstractions` | The `IModule` / `NavItem` contract the host and modules share |
| `src/Atrium.Design` | Shared design-system RCL — tokens + primitives; also `AccessTokenHolder` / session-expiry helpers |
| `src/Atrium.Modules.Storefront` · `.Admin` · `.Reports` | UI modules (RCLs implementing `IModule`) |
| `src/Atrium.Contracts` | DTO-only wire contracts shared by services and modules |
| `src/Atrium.Services.Catalog` | Core service — owns `catalogdb` |
| `src/Atrium.Services.Storefront` | App vertical — owns `storefrontdb`, relays a bearer to Catalog |
| `src/Atrium.Gateway` | YARP reverse proxy (config-driven, no code) |
| `src/Atrium.AppHost` | Single-file Aspire orchestration (`apphost.cs`) |

## Where the authoritative docs live

- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — how the pieces fit (the reference model).
- **[docs/adr/README.md](docs/adr/README.md)** — the ADR index; the *why* behind every choice (0001–0010).
- **[docs/guides/wire-up-a-new-app.md](docs/guides/wire-up-a-new-app.md)** — the end-to-end **how-to**:
  add a full vertical (service → contracts → module → gateway → Aspire → auth → tests), narrating the
  real Storefront + Catalog implementation. This is the source of truth for procedure; the skills below
  are its always-loaded guardrails.
- **[docs/agentic-workflow.md](docs/agentic-workflow.md)** — how this codebase is built with an LLM under a
  disciplined harness (independent gate, adversarial review, revertible per-item commits, live browser
  verification). The run system that harness uses lives in **[docs/runs/](docs/runs/)**.

## The build/test gate

From the repo root (`/Users/ted/code/Atrium`). This must be clean before any work is considered done:

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q   # expect 0 warnings / 0 errors
dotnet test Atrium.slnx                                       # Docker required for the integration lane
```

Test detail (unit vs. Testcontainers integration) is in the guide, §7.

## The design-system rule

Never hand-roll UI. All visual work — a page, a table, styling, a new component — **defers to the
`atrium-ui` skill**, which enforces reuse of `Atrium.Design` tokens and primitives over ad-hoc CSS.
Invoke it for any Razor/component/CSS change.

## Which skill for what

| Working on… | Skill |
|---|---|
| An `Atrium.Services.*` backend service (endpoints, Dapper/sprocs, repo) | **atrium-service** |
| An `Atrium.Modules.*` UI module (`IModule`, typed HTTP client, pages) | **atrium-module** |
| Shared `Atrium.Contracts` DTOs | **atrium-contracts** |
| Any UI / visual / CSS work | **atrium-ui** |

Each skill is a tight guardrail that points back into the guide + ADRs for the full walkthrough.
