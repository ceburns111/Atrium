# Interview prep — own this codebase

These docs exist to make you **bulletproof discussing Atrium as its architect**. You designed it and
directed the build; the goal now is to hold the "why," the trade-offs, and the gotchas so completely that
the conversation never lands on "I'm not sure — that part was generated." This is realistic: senior
engineers direct work they didn't type every line of. What separates a strong candidate is being able to
**explain the decisions and defend the trade-offs**, and that's entirely learnable from what's here.

> **Why this matters for _this_ interview:** Atrium's architecture (minus the AI slice) deliberately mirrors
> the interviewing company's. Every decision doc below is therefore a decision *they* have opinions about —
> know yours.

## The docs

| # | Doc | What it makes you bulletproof on |
|---|---|---|
| 01 | [Architecture & topology](01-architecture.md) | Modular monolith, YARP gateway, SCS service split, Aspire, ingress model |
| 02 | [Auth & security](02-auth-security.md) | OIDC vs JWT, bearer relay, token propagation, roles, session expiry, step-up MFA |
| 03 | [Modules, portal & design system](03-modules-portal-design.md) | Reflection module discovery, `IModule`, the shell, tokens/primitives |
| 04 | [Backend services & data](04-services-data.md) | Feature folders, Dapper+sprocs+DbUp+Mapperly, idempotency, testing |
| 05 | [The AI slice](05-ai-slice.md) | MAF + AG-UI agent, tools, config-driven model, step-up, **run-a-real-model-locally** |
| 06 | [Cross-cutting Q&A & gotchas](06-cross-cutting-qa.md) | Rapid-fire drills, "where are the bodies buried," the honest-answer playbook |

Each subsystem doc follows the same shape: **90-second explanation → how it actually works → why (trade-offs
+ rejected alternatives) → talking points → likely Q&A → gotchas → how I'd evolve it.**

These sit on top of the canonical reference docs — read those too, they're the source of truth:
[`../ARCHITECTURE.md`](../ARCHITECTURE.md), the [ADRs](../adr/) (the "why" of every major decision),
[`../diagrams/`](../diagrams/), and [`../BEYOND-THE-DEMO.md`](../BEYOND-THE-DEMO.md) (what was deliberately
scoped out — knowing your own boundaries is a senior signal).

## The 60-second architecture pitch (memorize this)

"Atrium is a **modular-monolith Blazor Server portal**. One host shell discovers self-contained UI
**modules** by reflection through an `IModule` contract — the host references the module projects but names
none of them, so adding a feature area is one project reference plus one `IModule`. Behind the UI, a **YARP
gateway** is the single ingress to backend services split along a **Self-Contained-Systems** grain: a *core*
service owns a capability's data (Catalog owns products), and an *app vertical* owns its own database and
composes core services over HTTP (Storefront owns orders and calls Catalog to price them) — one database per
service, no cross-service SQL. Identity is **Keycloak**: OIDC for the portal, JWT bearer for the services;
the cookie hop is browser-to-portal only, every hop after that carries the user's bearer, which the gateway
forwards and each service validates. Data access is **Dapper + stored procedures + DbUp + Mapperly — no
EF** — chosen for explicit SQL and compile-time mapping. The whole thing runs locally from a single-file
**Aspire** host. On top I added an **AI support agent** (Microsoft Agent Framework over AG-UI) as just
another slice — same gateway, same bearer, scoped to the signed-in user."

## Know cold vs. be able to reason about

**Know cold (you must not fumble these):**
- Why modular monolith over microservices *and* over a plain monolith (ADR-0001).
- The token journey: OIDC cookie → access-token-as-claim → scoped `AccessTokenHolder` → bearer to gateway →
  service validates → app vertical relays bearer to core (ADR-0004, ADR-0005).
- Why **no factory-registered `DelegatingHandler`** for the bearer (the `IHttpClientFactory`
  separate-scope gotcha, ADR-0004) — and the one sanctioned circuit-scoped exception in the AI slice
  (ADR-0011).
- Why **Dapper + sprocs, not EF** (ADR-0002), and the DbUp two-lane init (Migrations run-once /
  Programmability run-always).
- Module discovery by reflection and the **assemblies-in-two-places** routing gotcha (ADR-0001).
- One database per service; Storefront gets products over **HTTP, not SQL**.
- (AI) singleton agent vs request-scoped tools; user-scoping enforced in the sproc `WHERE`.

**Be able to reason about (expect "how would you…"):**
- Splitting a service out to its own process / real microservices, and what breaks (ADR-0005, BEYOND-THE-DEMO).
- Production token handling: refresh, a token store, `Duende.AccessTokenManagement` (ADR-0004).
- Service-to-service auth in prod (mTLS, client credentials, managed identity).
- Resilience: retries, circuit breakers, timeouts on the gateway/typed clients.
- Events/outbox between services instead of synchronous HTTP.

## The "I built this with AI" question — the honest playbook

If it comes up (or to preempt it): you don't hide it, you **frame it as direction**. "I architected it and
drove the implementation with AI assistance, the same way I'd direct a team — I made the decisions, set the
conventions, and reviewed the work; I even ran a structured multi-agent code review of the AI slice and
fixed the findings myself" (that's Run 4 — [`../runs/RUN4-MAF-REVIEW.md`](../runs/RUN4-MAF-REVIEW.md)). Then
**demonstrate ownership** by going deep on a trade-off unprompted. The failure mode isn't "used AI" — it's
"can't explain my own system." These docs close that gap. Where you genuinely wouldn't know an
implementation detail cold, the right answer is a senior one: *"I'd check X, but the design intent is Y"* —
never bluff a mechanism.
