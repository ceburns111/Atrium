# Atrium.Portal

## What it is
The Blazor Server host shell — the single web front end. It discovers self-contained UI modules by reflection, renders the app shell and navigation, handles OIDC login against Keycloak, and captures the access token for downstream calls.

## Role in the topology
**Portal / host.** References `Atrium.Abstractions`, `Atrium.Design`, and every `Atrium.Modules.*` project, but hard-codes none of them. It only knows the **gateway** address (Aspire service discovery); it never addresses Catalog or Storefront directly.

## Key types
- `Modularity/ModuleLoader.cs`, `Modularity/ModuleCatalog.cs` — find and register `IModule` implementations at startup.
- `Components/Layout/MainLayout.razor` — copies the access token from the `ClaimsPrincipal` into the scoped `AccessTokenHolder`.
- `Components/Layout/SessionErrorBoundary.razor` — prompts re-login on a `SessionExpiredException` instead of crashing the circuit.
- `Components/Routes.razor` (`Router` with `AdditionalAssemblies`), `Program.cs` (OIDC + `AddAdditionalAssemblies`).

## Run / test
The main entry point: `cd src/Atrium.AppHost && aspire run` starts it alongside the gateway, services, Keycloak, and SQL Server. No standalone launch. Portal-adjacent logic is covered by `tests/Atrium.UnitTests/SessionExpiredTests.cs`.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — topology and the authenticated request flow.
- [ADR-0001](../../docs/adr/0001-modular-monolith.md) · [ADR-0004](../../docs/adr/0004-token-propagation-and-option-b.md) · [ADR-0008](../../docs/adr/0008-graceful-session-expiry-handling.md).
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md).
