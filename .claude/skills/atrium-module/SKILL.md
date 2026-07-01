---
name: atrium-module
description: >-
  Use whenever building or editing an Atrium.Modules.* UI module — a self-contained Razor Class Library
  the portal shell discovers by reflection (Storefront / Admin / Reports). Enforces the module
  guardrails: implement IModule (Name/Description/BasePath/NavItems/RegisterServices); a typed HTTP
  client pointed at the gateway that attaches the signed-in user's access token and calls
  ThrowIfSessionExpired() BEFORE EnsureSuccessStatusCode(); @page routes that resolve automatically; and
  a project reference from the Portal. Trigger this for "add a module", "wire a client", "add a page to
  the module", or any .cs/.razor work under src/Atrium.Modules.*. Defer ALL visual/design concerns to
  the atrium-ui skill.
---

# Atrium module — UI-module guardrails

The always-loaded rules for a UI module. For the full walkthrough follow
**[docs/guides/wire-up-a-new-app.md](../../../docs/guides/wire-up-a-new-app.md) §3** — this skill is the
checklist that keeps a module faithful to the reference implementation
`src/Atrium.Modules.Storefront`. Don't restate the guide; open it alongside this.

A module is a **Razor Class Library** (`Microsoft.NET.Sdk.Razor`) implementing one `IModule`
([ADR-0001](../../../docs/adr/0001-modular-monolith.md)). The host discovers it by reflection — **no
host edits** beyond one project reference.

## Rules

- **Implement `IModule`** (`src/Atrium.Abstractions/IModule.cs`) — copy the shape of
  `StorefrontModule.cs`: `Name`, `Description`, `BasePath` (the route prefix the pages own, e.g.
  `/widget`), an optional `Accent` hex, an optional `RequiredRole` (set it — e.g. `"admin"` — to role-gate
  the module's home card **and** nav link behind an `<AuthorizeView>`; leave null for all users, as
  Storefront does), `NavItems`, and `RegisterServices(...)`. In `RegisterServices`
  register the module's typed HTTP client(s) pointed at the **gateway**, never a service directly:
  `services.AddHttpClient<WidgetClient>(c => c.BaseAddress = new Uri("https+http://gateway"))`.
- **Typed HTTP client** (model on `Storefront/Catalog/CatalogClient.cs`): take `HttpClient` +
  `AccessTokenHolder` (from `Atrium.Design`), attach the signed-in user's token as a `Bearer` header,
  and — importantly — call **`response.ThrowIfSessionExpired()` BEFORE `EnsureSuccessStatusCode()`** so
  a 401 becomes a typed `SessionExpiredException` the shell's `SessionErrorBoundary` turns into a
  "sign in again" panel ([ADR-0008](../../../docs/adr/0008-graceful-session-expiry-handling.md)). Use
  `AccessTokenHolder`, not a `DelegatingHandler` — a Blazor circuit has no `HttpContext`
  ([ADR-0004](../../../docs/adr/0004-token-propagation-and-option-b.md)).
- **Pages + routes.** Put routable components under `Pages/` with `@page "/…"` under the module's
  `BasePath`. Routes resolve **automatically** — the host registers every discovered
  `Atrium.Modules.*` assembly in both `Routes.razor` and `Program.cs`, so no host route edit is needed.
- **Portal reference is the one wiring edit.** Add the module `.csproj` as a `<ProjectReference>` in
  `src/Atrium.Portal/Atrium.Portal.csproj` so its DLL lands where `ModuleLoader` scans. It names no
  types.
- **Consume `Atrium.Contracts` DTOs** for wire payloads — see the **atrium-contracts** skill.

## Design — defer to atrium-ui

**All visual/design concerns belong to the `atrium-ui` skill — invoke it for any Razor/component/CSS
work.** Do not hand-roll CSS, hard-code colors/spacing, or restate design tokens here; pull primitives
and tokens from `Atrium.Design` as that skill directs. This skill covers module *plumbing*; atrium-ui
covers how it *looks*.

## After the work

Run the gate from the repo root: `dotnet csharpier format . && dotnet build Atrium.slnx -v q`
(0W/0E), then `dotnet test Atrium.slnx`. Screenshot the running page per the atrium-ui skill.
