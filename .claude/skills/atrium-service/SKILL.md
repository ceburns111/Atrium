---
name: atrium-service
description: >-
  Use whenever building or editing an Atrium.Services.* backend service — a core service (owns a
  capability's database, e.g. Catalog) or an app vertical (owns its own database and composes core
  services over HTTP, e.g. Storefront). Enforces the backend guardrails: feature-folder layout,
  endpoints via Map*Endpoints on a route group with .WithTags and RequireAuthorization, Dapper +
  stored procedures + DbUp + Mapperly (never EF), co-located repository interfaces with an integration
  test, and correct Program.cs DI/auth/DB-init wiring. Trigger this even for "add an endpoint", "write
  the repository", "add a sproc/migration", or any .cs work under src/Atrium.Services.* — the shape is
  load-bearing and drifts silently.
---

# Atrium service — backend guardrails

The always-loaded rules for a backend service. For the full walkthrough follow
**[docs/guides/wire-up-a-new-app.md](../../../docs/guides/wire-up-a-new-app.md) §1** (and §5–§7 for
Aspire/auth/tests) — this skill is the checklist that keeps a service faithful to the reference
implementations `Atrium.Services.Catalog` (core) and `Atrium.Services.Storefront` (app vertical).
Don't restate the guide; open it alongside this.

## Pick the shape first

[ADR-0005](../../../docs/adr/0005-slice-calls-core.md): a **core service** owns its data and calls no
one (Catalog); an **app vertical** owns its data **and** relays the caller's bearer to a core service
over HTTP (Storefront → Catalog). Decide before you start — the vertical is the superset.

## Rules

- **Feature folders** ([ADR-0007](../../../docs/adr/0007-feature-folders-and-repository-testing.md)).
  One folder per slice holds its endpoints, repository, mapper, and row type; namespaces mirror
  folders. The **repository interface lives in the same file, directly above** its class (see
  `Catalog/CatalogRepository.cs`) — never a separate `IWidgetRepository.cs`.
- **Endpoints** = a static `Map*Endpoints` extension with handlers as **named static methods** (no
  inline lambdas), returning `TypedResults`. Map a route group with `.WithTags("…")` for OpenAPI
  grouping. Route nesting per
  [ADR-0009](../../../docs/adr/0009-service-root-route-nesting.md): the **service-root group is
  declared once in `Program.cs` with `RequireAuthorization()`**, and features map **relative**
  subtrees onto it. (A single-feature core service is the degenerate case — its one group *is* the
  service root, as in `CatalogEndpoints.cs`.)
- **Data = Dapper + sprocs + DbUp + Mapperly**, never EF
  ([ADR-0002](../../../docs/adr/0002-dapper-sprocs-dbup.md)). All SQL lives in stored procedures;
  Dapper executes them via `CommandDefinition(..., commandType: CommandType.StoredProcedure)`;
  Mapperly (`[Mapper] partial`) maps the row type → the `Atrium.Contracts` DTO at compile time.
  Migrations (run-once, numbered) under `Data/Scripts/Migrations/`; programmability (run-always,
  `CREATE OR ALTER`, one sproc per file) under `Data/Scripts/Programmability/`. Copy
  `Data/DatabaseInitializer.cs` verbatim (intentionally duplicated per service) and keep the embedded-SQL
  glob in the `.csproj` so scripts ship in the assembly.
- **Every repository gets an integration test** against a real SQL Server via Testcontainers, not a
  mock ([ADR-0007](../../../docs/adr/0007-feature-folders-and-repository-testing.md)) — add a
  `*RepositoryTests` in the shared `SqlServerFixture` collection. Extract branching business logic into
  pure functions and unit-test those directly.
- **`Program.cs` wiring** (model on `Atrium.Services.Storefront/Program.cs`): `AddSqlServerClient("<db>")`,
  register the repository, `AddKeycloakJwtBearer("keycloak", realm: "atrium")` with `Audience = "atrium"`,
  run `DatabaseInitializer.Initialize(...)` before serving, then `UseAuthentication`/`UseAuthorization`,
  `MapHealthChecks("/health")`, and the service-root `MapGroup("/<name>").RequireAuthorization()`.
  **Public reads:** a specific endpoint can opt back out with `.AllowAnonymous()` (its metadata overrides
  the group policy) while writes stay gated — e.g. Catalog's `GET /catalog/products|categories` are
  anonymous so the storefront browses signed-out, but the `admin` `POST`/`PUT` writes are not. Default to
  gated; open a read only when it's genuinely public.
  **Gotcha:** to gate on a role you MUST set both `MapInboundClaims = false` and
  `RoleClaimType = "role"` or every caller 403s — the worked example (with comments) is
  `Atrium.Services.Catalog/Program.cs`, which role-gates admin writes; Storefront relays a bearer and
  does not role-gate (see [ADR-0003](../../../docs/adr/0003-yarp-keycloak-auth.md)).
- **App-vertical bearer relay** ([ADR-0005](../../../docs/adr/0005-slice-calls-core.md)): when calling a
  core service, add `AddHttpContextAccessor()`, an `AddHttpClient<…>` pointed at `https+http://<core>`,
  and forward the incoming `Authorization` header — reference
  `Atrium.Services.Storefront/Catalog/StorefrontCatalogClient.cs`. A core service does none of this.

## After the work

Run the gate from the repo root: `dotnet csharpier format . && dotnet build Atrium.slnx -v q`
(0W/0E), then `dotnet test Atrium.slnx` (Docker up for the integration lane).
