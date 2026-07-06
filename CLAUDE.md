# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All from the repo root. The build/test gate — must be clean before any work is considered done:

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q   # expect 0 warnings / 0 errors
dotnet test Atrium.slnx                                       # Docker required for the integration lane
```

**Formatting is load-bearing:** CSharpier runs in check mode on every build (`Directory.Build.props`), so the build *fails* on unformatted code. Run `dotnet csharpier format .` before building. If the tool is missing, `dotnet tool restore`.

Tests use **xUnit v3 on Microsoft.Testing.Platform** (`global.json` sets the runner), so filters go after `--` and use MTP flags, not the legacy VSTest `--filter`:

```bash
dotnet test tests/Atrium.UnitTests                                    # fast, no external deps
dotnet test tests/Atrium.IntegrationTests                             # real SQL Server via Testcontainers (Docker)
dotnet test tests/Atrium.UnitTests -- --filter-class "*MenuTests"     # one class
dotnet test tests/Atrium.UnitTests -- --filter-method "*.MethodName"  # one test
dotnet test tests/Atrium.Evals                                        # LLM evals; skip themselves if Ollama is down
```

Evals judge via Ollama at `http://localhost:11434` with `qwen2.5:14b-instruct`; results land in `eval-results/` next to the test binary with response caching on.

Run the whole system (Keycloak, SQL Server, services, gateway, Portal, dashboard):

```bash
cd src/Atrium.AppHost && aspire run
```

## Architecture

**Read [AGENTS.md](AGENTS.md) first** — it is the orientation hub. Authoritative detail lives in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (how it fits), [docs/adr/](docs/adr/README.md) (why, 0001–0012), and [docs/guides/wire-up-a-new-app.md](docs/guides/wire-up-a-new-app.md) (the end-to-end procedure for adding a full vertical).

The one-paragraph model: Atrium is a **modular-monolith Blazor Server portal**. The host shell (`Atrium.Portal`) discovers UI **modules** (`Atrium.Modules.Storefront/.Admin/.Reports` — Razor Class Libraries) by reflection through the `IModule` contract in `Atrium.Abstractions`; the host references the module projects but names none of them. Module typed clients call a **YARP gateway** (`Atrium.Gateway`), which fronts backend services split along Self-Contained-Systems lines: a **core service** owns a capability's database (`Atrium.Services.Catalog` → `catalogdb`), an **app vertical** owns its own database and composes core services over HTTP (`Atrium.Services.Storefront` → `storefrontdb`, calls Catalog to price orders — never SQL across databases). Identity is **Keycloak** (OIDC for the Portal, JWT bearer for services, shared `atrium` audience, flat `role` claim). Data access is **Dapper + stored procedures + DbUp + Mapperly — never EF**. `Atrium.AppHost` is a single-file Aspire composition root; `Atrium.Contracts` holds the DTO-only wire types both sides share; `Atrium.Design` is the shared design-system RCL (tokens + primitives + `AccessTokenHolder` + the AG-UI chat plumbing); `Atrium.ServiceDefaults` is the shared deployment-infrastructure library (telemetry, JWT auth, api-docs page, `DatabaseInitializer` — never domain code, ADR-0012).

Cross-cutting mechanics worth knowing before touching anything (each has an ADR):

- **Token flow:** OIDC parks the access token in the auth cookie; `MainLayout` copies it into a scoped `AccessTokenHolder`; typed clients send through the shared `SendForJsonAsync` pipeline (`Atrium.Design/HttpClientExtensions.cs`), which calls `ThrowIfSessionExpired()` **before** `EnsureSuccessStatusCode()`. No *factory-registered* `DelegatingHandler` for the bearer — `IHttpClientFactory` builds handler chains in a different scope, so a scoped holder reads empty (ADR-0004, ADR-0008). The AG-UI chat client's `BearerTokenHandler` is the one sanctioned exception, composed manually inside the circuit scope (ADR-0011).
- **Module routing needs assemblies registered in two places** — `<Router AdditionalAssemblies>` *and* `MapRazorComponents().AddAdditionalAssemblies()` (ADR-0001).
- **DbUp has two lanes:** `Data/Scripts/Migrations/*` run once (schema + seed); `Data/Scripts/Programmability/*` run always as `CREATE OR ALTER` (sprocs). SQL files are embedded resources; the shared runner is `DatabaseInitializer` in `Atrium.ServiceDefaults`.
- **Services** use feature folders, `Map*Endpoints` on route groups with `.WithTags` and `RequireAuthorization`; hosts wire the shared `Atrium.ServiceDefaults` extensions (`AddAtriumTelemetry`, `AddAtriumJwtAuth()` — which also registers the `admin` policy — and `MapAtriumApiDocs(title)`). Auth matrix: Catalog reads anonymous; orders + feedback authenticated; Catalog writes and Reports reads `admin`; the agent endpoint has a step-up MFA policy.
- The AI **Support agent** lives in `src/Atrium.Services.Storefront/Support/` (Microsoft Agent Framework over Ollama; OTel → guardrail → cache decorator pipeline; the guardrail screens **all** user messages and fails closed on classifier transport errors; AG-UI SSE endpoint at `/storefront/agent`, feedback at `/storefront/agent/feedback`).

## Skills — invoke before editing

The repo ships guardrail skills; use them for any matching work, they encode the load-bearing shape:

| Working on… | Skill |
|---|---|
| `Atrium.Services.*` (endpoints, Dapper/sprocs, repos) | **atrium-service** |
| `Atrium.Modules.*` (`IModule`, typed clients, pages) | **atrium-module** |
| `Atrium.Contracts` DTOs | **atrium-contracts** |
| Any UI / Razor / CSS work — never hand-roll styling | **atrium-ui** |
