# ADR-0012 — Deployment infrastructure is shared; domain code is not

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** post-audit consolidation (2026-07)

## Context

[ADR-0007](0007-feature-folders-and-repository-testing.md) accepted a byte-identical
`DatabaseInitializer` duplicated into both services, rejecting a shared library because it would
"couple two services that are meant to be independently deployable … to save ~40 lines."

The 2026-07-02 audit showed how that aged: the two copies had **already drifted** (finding B5), and
the same copy-paste pattern had spread — Keycloak JWT wiring + the `admin` policy + a 25-line Redoc
page were triplicated across the service hosts (B6), and telemetry wiring was heading the same way.
Worst of all, the duplicated code included the **load-bearing claim-mapping settings**
(`MapInboundClaims = false`, `RoleClaimType = "role"` — the "403 for everyone" gotcha from
[ADR-0003](0003-yarp-keycloak-auth.md)): exactly the code where per-host drift is a security bug, not
service autonomy.

So the question ADR-0007 answered ("share or duplicate?") was answered for the wrong category. The
distinction that matters isn't shared-vs-duplicated; it's *what kind of code* is being shared.

## Decision

**`Atrium.ServiceDefaults`** — the Aspire-idiomatic shared project — owns the cross-host
**deployment-infrastructure defaults**:

| Extension | What it centralizes |
|---|---|
| `AddAtriumTelemetry()` / `UseAtriumRequestLogging()` | Serilog structured logging + OTel tracing/metrics, OTLP export to the Aspire dashboard (`TelemetryExtensions.cs`) |
| `AddAtriumJwtAuth()` | Keycloak JWT bearer for the shared `atrium` realm/audience, the claim-mapping settings, and the `admin` policy; returns the `AuthorizationBuilder` so a host chains service-specific policies (Storefront adds `StepUpMfa`) (`AuthExtensions.cs`) |
| `MapAtriumApiDocs(title)` | The Development-only Redoc viewer over the host's `/openapi/v1.json` (`ApiDocsExtensions.cs`) |
| `DatabaseInitializer.Initialize(connectionString, scriptsAssembly, logger)` | The two-lane DbUp runner (run-once Migrations, run-always Programmability); each service passes the assembly embedding **its own** SQL (`DatabaseInitializer.cs`) |

And the sharpened rule that supersedes ADR-0007's duplication clause:

> **Domain and data code is never shared between services. Deployment-infrastructure defaults may
> be.** The independent-deployability argument protects the *domain* seam — a service's endpoints,
> repositories, sprocs, and wire behavior, where divergence is legitimate evolution. Infrastructure
> that must be identical across hosts (how a JWT is validated, how traces export, how migrations run)
> is the opposite case: there, divergence is a **bug**, and duplication is how the bug happens.

Litmus test when deciding where code goes: *if two copies diverging would be a defect, share it; if
divergence could ever be a service legitimately evolving on its own, duplicate it.* `ServiceDefaults`
therefore contains **no DTOs, no repositories, no domain services, no SQL** — the services' `Data/`
folders keep their own scripts; only the runner is shared.

### Test projects follow the deployable

The same lens answers the standing question about test organization (the suites —
`tests/Atrium.UnitTests`, `tests/Atrium.IntegrationTests`, `tests/Atrium.Evals` — cut across every
service boundary). Position: **test suites follow the deployable.** Today the deployable is the whole
demo system (one AppHost, one release cadence), so three cross-cutting suites are proportionate — a
per-project test matrix would be nine-plus projects guarding seams that never ship independently.
When a vertical is extracted into its own deployable, **its tests split with it**; because tests are
already grouped by the code under test, that split is a file move, not a rewrite.

## Consequences

- **The load-bearing settings live once.** Claim mapping, audience, OTLP export, and the DbUp lanes
  cannot drift per host anymore; a fix lands everywhere on the next build.
- **Every host takes a `ServiceDefaults` dependency** (Catalog, Storefront, Gateway, Portal — the
  Gateway and Portal use only the telemetry half). Acceptable precisely because the project contains
  no domain types and changes rarely; it versions with the platform, not with any service.
- **ADR-0007 is amended, not rewritten.** Its feature-folder and repository-testing decisions stand
  untouched; only the "keep `DatabaseInitializer` duplicated" clause is superseded.
- **The boundary needs guarding.** The failure mode of this decision is `ServiceDefaults` quietly
  fattening into `Atrium.Common`. The litmus test above is the review question for every addition.

## Alternatives rejected

- **Keep duplicating** — already failed in practice: the "byte-identical" copies drifted within weeks
  (audit B5), and the triplicated JWT config put security-relevant settings on the drift path.
- **A broad `Atrium.Common` shared library** — the thing ADR-0007 rightly feared. The rule here is
  deliberately narrow: infrastructure defaults only, nothing a service could legitimately want to do
  differently.
- **Copy-paste with a lint/diff check to detect drift** — machinery to police duplication instead of
  removing it; more moving parts than the shared project it was avoiding.
