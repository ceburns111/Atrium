# ADR-0003 — YARP gateway + Keycloak (OIDC for the portal, JWT for services)

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 4a–4b

> **Amended 2026-07-03:** the claim below that the portal client secret "never lives in the repo" is
> wrong as written — `dev-portal-secret` is committed twice (the AppHost env literal and the realm
> export), a deliberate dev-only convenience for a self-contained demo. Production would inject the
> secret (user-secrets/env/vault); the mechanism is one `WithEnvironment` line. The JWT wiring this
> ADR describes now lives once, in `AddAtriumJwtAuth()` (see [ADR-0012](0012-shared-deployment-infrastructure.md)).

## Context

Multiple backend services need a single ingress and a single identity story. We don't want the Portal
to know each service's address, and we don't want every service to reinvent authentication. We also
want a realistic auth model — real OIDC, real JWT validation, roles — not a fake login.

## Decision

- **YARP as the one gateway.** `Atrium.Gateway` reverse-proxies `/catalog/{**catch-all}` →
  the catalog cluster and `/storefront/{**catch-all}` → the storefront cluster. Cluster destinations
  use Aspire **service discovery** addresses (`https+http://catalog`, `https+http://storefront`), so
  there are no hard-coded ports. The Portal only ever talks to the gateway.
- **Keycloak as the identity core**, imported from a realm file by the AppHost on a **fixed port
  (8080)** so its URL is stable across runs.
  - **Portal ↔ Keycloak: OIDC.** The Portal is a confidential client (`atrium-portal`); its secret is
    injected by the AppHost as the `Keycloak__PortalSecret` env var so it never lives in the repo.
  - **Services ↔ Keycloak: JWT bearer.** Catalog and Storefront validate the token's issuer and
    require the shared **`atrium` audience**, added by a realm custom-audience mapper. Product reads
    are open to any authenticated user; writes (`POST`/`PUT /catalog/products`) require the **`admin`**
    policy.

## Consequences

- **One ingress, discovery-addressed.** Adding a route for a new service is a gateway config entry, not
  a code change (and BEYOND-THE-DEMO.md item 4 sketches making services self-register their routes).
- **One audience, many services.** The shared `atrium` audience lets a single access token be accepted
  by every service, which is what makes the bearer-relay in [ADR-0005](0005-slice-calls-core.md) work.
- **The gotcha that cost real time — `MapInboundClaims = false`.** The realm's role mapper emits a
  **flat `role`** claim, and Catalog sets `RoleClaimType = "role"`. But JWT-bearer defaults to
  `MapInboundClaims = true`, which renames the inbound `role` to the long `ClaimTypes.Role` URI —
  so `RequireRole("admin")` finds nothing and returns **403 for everyone, admins included**. Fix:
  `options.MapInboundClaims = false` on both Portal and Catalog. Documented so no one re-debugs it.
- **Operational cost of the realm-as-code approach:** `WithRealmImport` only *creates* missing
  resources, so changing the realm requires wiping the Keycloak data volume to re-import (see
  HANDOFF known limitations).

## Alternatives rejected

- **Per-service auth, no gateway** — every service reimplements OIDC and the Portal learns every
  address. More surface, no upside.
- **Portal calls services directly (no proxy)** — loses the single ingress and the config-driven route
  table that a gateway gives you.
- **A hand-rolled auth / dev IdP** — wouldn't demonstrate a real OIDC + JWT + roles pipeline, which is
  the point.

**Diagrams:** [auth-sequence.md](../diagrams/auth-sequence.md) — OIDC login → bearer → gateway forward → JWT validation; topology in [ARCHITECTURE.md](../ARCHITECTURE.md#topology).
