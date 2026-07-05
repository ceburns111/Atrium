# Atrium.ServiceDefaults

## What it is
The shared deployment-infrastructure defaults for every Atrium backend host. It owns the cross-host wiring that must be identical across services — settings where per-host drift is a bug, not legitimate evolution. See [ADR-0012](../../docs/adr/0012-shared-deployment-infrastructure.md) for the rationale and the boundary rule.

## Role in the topology
**Platform defaults.** Referenced by `Atrium.Services.Catalog`, `Atrium.Services.Storefront`, `Atrium.Gateway`, and `Atrium.Portal`. Contains no domain types, no DTOs, no SQL — only the four extension points below.

## Key extensions

| Extension | What it centralizes |
|---|---|
| `AddAtriumTelemetry(instrumentSqlClient)` / `UseAtriumRequestLogging()` | Serilog structured logging + OTel tracing/metrics, OTLP export to the Aspire dashboard (`TelemetryExtensions.cs`) |
| `AddAtriumJwtAuth()` | Keycloak JWT bearer for the shared `atrium` realm/audience, the load-bearing claim-mapping settings (`MapInboundClaims = false`, `RoleClaimType = "role"`), and the `admin` policy; returns `AuthorizationBuilder` so a host chains service-specific policies (`AuthExtensions.cs`) |
| `MapAtriumApiDocs(title)` | The Development-only Redoc viewer at `/docs` over the host's `/openapi/v1.json` (`ApiDocsExtensions.cs`) |
| `DatabaseInitializer.Initialize(connectionString, scriptsAssembly, logger)` | Two-lane DbUp runner: run-once Migrations, run-always Programmability; each service passes `typeof(Program).Assembly` so the runner finds that service's embedded SQL (`DatabaseInitializer.cs`) |

`Aspire.Keycloak.Authentication` and `dbup-sqlserver` are consumed here and flow **transitively** to service hosts — don't reference them directly in service `.csproj` files.

## Run / test
Not run on its own; compiled into every host. Its behavior is exercised whenever the app runs or the test suites run (JWT wiring is exercised by `Atrium.IntegrationTests`; DB init by both integration and evals).

## See also
- [ADR-0012](../../docs/adr/0012-shared-deployment-infrastructure.md) — why deployment infra is shared and the litmus test for what belongs here.
- [ADR-0003](../../docs/adr/0003-yarp-keycloak-auth.md) — Keycloak JWT auth model.
- [ADR-0002](../../docs/adr/0002-dapper-sprocs-dbup.md) — Dapper + sprocs + DbUp.
