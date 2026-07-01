# Beyond the demo — what Atrium would grow into

Atrium is a working slice, not a finished platform. Six things were **deliberately scoped out** and
documented instead of built — because each is "more of the same" mechanically, or a
production concern that a demo can't honestly exercise. This doc says how each one grows out of what's
already here, so the shape is a decision on record rather than a gap.

For the decisions already made, see the [ADRs](adr/). For how the built system fits together, see
[ARCHITECTURE.md](ARCHITECTURE.md).

---

## 1. The other two backend verticals (Admin API + DB, Reports API + DB)

**Today.** Admin and Reports exist as **UI modules** that reuse existing services: Admin writes through
the **Catalog core**, Reports reads a `/storefront/reports/sales` aggregate on the **Storefront
vertical**. Neither has its own backend, because neither owns data yet.

**When they'd grow one.** The moment Admin owns data that isn't a product (audit logs, feature flags,
back-office settings) or Reports needs its own read-optimized store (a denormalized reporting DB /
materialized rollups), each grows its own API + database exactly the way Storefront did:

- new `Atrium.Services.Admin` / `Atrium.Services.Reports` project, own database (`admindb` /
  `reportsdb`), same **Dapper + sprocs + DbUp + Mapperly** recipe ([ADR-0002](adr/0002-dapper-sprocs-dbup.md));
- register it in the AppHost with `.WithReference(db).WithReference(keycloak)`;
- add a gateway route (`/admin/{**catch-all}`, `/reports/{**catch-all}`) ([ADR-0003](adr/0003-yarp-keycloak-auth.md));
- compose core services over HTTP where it needs data it doesn't own ([ADR-0005](adr/0005-slice-calls-core.md)).

There's nothing new to design — this is the vertical template applied twice more, which is precisely
why it's documented rather than coded.

## 2. Promote Orders to its own core service

**Today.** Orders are owned by the **Storefront app vertical** (`storefrontdb`, `usp_Order_*`). That's
correct while Storefront is the only thing that reads or writes orders — "extract when it hurts."

**The trigger.** A **second** slice needs orders (e.g. an Admin fulfillment view, or a customer-service
tool). At that point orders are a shared capability, not one vertical's private data, and duplicating
the order tables into a second DB would create a sync problem.

**The move.** Graduate Orders to a **core service** (`Atrium.Services.Orders`, own DB) the same shape
as Catalog: it owns order data and exposes an API; makes no outbound service calls. Storefront stops
owning order tables and instead **composes** the Orders core the same bearer-relay way it already
composes Catalog ([ADR-0005](adr/0005-slice-calls-core.md)). The composition pattern doesn't change —
only who owns the data does.

## 3. Polyrepo split + contracts as versioned NuGet

**Today.** One repo, one solution, one deploy; contracts shared as a **project**
([ADR-0006](adr/0006-shared-contracts-then-nuget.md)).

**The move, when teams and cadences diverge.** Split one repo per vertical (Catalog, Storefront, and
each graduated core/vertical), each with independent CI/CD. The shared-project contracts can no longer
be referenced across repos, so:

- `Atrium.Contracts` (or a per-domain slice of it) is **published as a versioned NuGet package** from
  its owning repo, under SemVer;
- consumers **pin** a version and upgrade deliberately, so a producer can ship without lockstep
  rebuilds of every consumer;
- each repo gets its own pipeline (build → test → publish image / package), and deploys on its own
  cadence.

This is the standard Self-Contained-Systems packaging story; the guardrail from ADR-0006 (contracts
stay DTO-only) keeps that package small and stable.

## 4. Per-team gateway route self-registration

**Today.** Routes live in `Atrium.Gateway/appsettings.json` — a central file the gateway owner edits
whenever a service is added.

**The move.** Make route ownership follow service ownership: each service **declares its own route**
(config the service ships, a discovery-backed route provider, or a small registration call at
startup), and the gateway assembles its route table from those declarations instead of a hand-edited
central file. A team can stand up or change a service's ingress without a cross-team edit to the
gateway repo — the config-driven-YARP version of "the module owns its own surface."

## 5. Production service discovery

**Today.** Aspire provides discovery for local dev — `https+http://catalog` resolves because the
AppHost wires it. That's a **development-time** convenience, not a production mechanism.

**The move.** Behind the same discovery-address abstraction, swap in a real registry per environment:

- **Kubernetes** — services resolve by DNS (`catalog.namespace.svc.cluster.local`); the route table and
  cluster destinations come from config/secrets per environment.
- or a **service registry** (Consul / cloud-native equivalent) feeding a config-driven YARP route
  table.

Because nothing hard-codes ports today (the gateway and the Portal both use discovery addresses), this
is a **configuration + platform** change, not a code rewrite. The point of using discovery addresses
now is to keep that door open.

## 6. True independent UI-module deploy

**Today.** UI modules are **RCLs discovered by reflection in one host process**
([ADR-0001](adr/0001-modular-monolith.md)) — strong *code* boundaries, but they rise and fall as one
deploy.

**Options, lightest to heaviest** — deliberately *not* built, because the monolith's boundaries already
give the demo what it needs:

- **UI-module-as-versioned-NuGet.** Package each module and let the host consume pinned versions. A
  module can be built and versioned independently; the host still redeploys to pick up a new version.
  The cheap compromise — module *versioning* without module *runtime* independence.
- **Runtime folder-drop / plugin load.** Load module assemblies from a directory at startup (or hot),
  so dropping a new module DLL doesn't require rebuilding the host. More moving parts (assembly load
  contexts, versioning, isolation) for genuinely independent module delivery into one host.
- **Micro-frontends.** Separate deployables composed in the browser (module federation / iframes /
  web components). True independent deploy and independent runtime — and the heaviest: cross-app
  routing, shared-shell/session, and design-system distribution all become real problems. The right
  answer only if independent UI deploy becomes a hard requirement.

The `IModule` seam means any of these is a **packaging/hosting** change, not a rewrite of the modules
themselves — which is the whole reason the boundary was drawn there first.

---

## The through-line

Every item above is reachable **without redesigning** what exists: the vertical template (1),
the core-extraction pattern (2), the contracts guardrail (3), discovery addresses (4, 5), and the
`IModule` seam (6) were each chosen so that growth is additive. "Extract when it hurts" — and when it
does, the extraction points are already cut.
