# Interview study — Architecture & topology

> My north star for this doc: I can whiteboard Atrium end-to-end, defend every seam, name the
> alternative I rejected and why, and be honest about the demo shortcuts without losing the thread of
> the production path. Sources: [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md), ADRs 0001/0003/0005/0009,
> `src/Atrium.AppHost/apphost.cs`, `src/Atrium.Gateway/`, `src/Atrium.ServiceDefaults/`,
> [`docs/BEYOND-THE-DEMO.md`](../BEYOND-THE-DEMO.md), [`docs/HANDOFF.md`](../HANDOFF.md).

---

## The 90-second whiteboard pitch

What I'd say out loud:

"Atrium is a **modular-monolith Blazor Server portal** in front of a set of backend services split
along **Self-Contained-Systems** lines. One host process — `Atrium.Portal` — discovers its UI modules
(Storefront, Admin, Reports) by **reflection** over an `IModule` contract, so the shell references the
module projects but names none of them in code. Behind the UI there's **one ingress**: a **YARP
gateway**. The gateway fronts two backend shapes — a **core service** (Catalog) that *owns* product
data in its own database, and an **app vertical** (Storefront) that owns *its own* database (orders,
reports) and **composes Catalog over HTTP** for anything it doesn't own. Identity is **Keycloak**:
OIDC for the Portal, JWT bearer for the services, one shared `atrium` audience. Data access is
**Dapper + stored procedures + DbUp + Mapperly** — deliberately no EF. The whole thing is wired for
local dev by a single-file **Aspire** AppHost that gives me service discovery and a per-service
database for free."

The one-line hook: **"N apps, one host, one ingress, one database per service."**

```
            ┌─────────────────────────── Atrium.Portal (Blazor Server) ───────────────────────────┐
 Browser ──cookie──▶  App shell + ModuleCatalog  ──▶  Modules: Storefront · Admin · Reports        │
   │                  (reflection-discovered via IModule)                                          │
   │  OIDC redirect                          typed clients attach Bearer JWT                        │
   ▼                                                    │                                          │
 Keycloak  ◀──OIDC code+PKCE── Portal                   ▼                                          │
 realm: atrium                              ┌──────  Atrium.Gateway (YARP)  ──────┐                 │
 (OIDC + JWT authority)                     │  /catalog/{**}   /storefront/{**}   │  (no auth here) │
        ▲  ▲                                └──────┬───────────────────┬─────────┘                 │
        │  │ validate JWT (aud: atrium)     Bearer │ forwarded         │ Bearer forwarded          │
        │  └───────────────────────────────┐       ▼                   ▼                           │
        │                              ┌─ Catalog (core) ─┐     ┌─ Storefront (vertical) ─┐        │
        │                              │  owns catalogdb   │◀────┤  owns storefrontdb       │       │
        └──────────────────────────────  no outbound calls│ GET │  bearer-relay, DIRECT    │       │
                                       └────────┬─────────┘ /catalog/products (https+http://catalog)
                                          (catalogdb)             └──────────┬──────────────┘
                                                                        (storefrontdb)
```

Key detail the sketch encodes: the **cookie hop is Browser↔Portal only**; every hop from the Portal's
typed clients onward is a **Bearer JWT**. And the **Storefront→Catalog** price/report relay goes
**direct** to the Catalog service at its discovery address (`https+http://catalog`), *not* back out
through the gateway.

---

## How it actually works

**Ingress is the gateway, and only the gateway.** The Portal never knows a service's address. Its
module HTTP clients target the single logical address `https+http://gateway`, resolved at runtime by
Aspire service discovery (`Program.cs` in `Atrium.Portal` calls `AddServiceDiscovery()` and
`ConfigureHttpClientDefaults(http => http.AddServiceDiscovery())`). YARP matches two catch-all routes
and forwards to two clusters — from `src/Atrium.Gateway/appsettings.json`:

- route `catalog`: `Path: /catalog/{**catch-all}` → cluster `catalog` → destination `https+http://catalog`
- route `storefront`: `Path: /storefront/{**catch-all}` → cluster `storefront` → destination `https+http://storefront`

The gateway itself is a *pure* reverse proxy (`src/Atrium.Gateway/Program.cs`):
`AddReverseProxy().LoadFromConfig(...).AddServiceDiscoveryDestinationResolver()`. Those
`https+http://…` destinations are **logical Aspire service names**, not URLs — the
`AddServiceDiscoveryDestinationResolver()` turns them into the real (dynamic) ports at runtime. No
hard-coded ports anywhere.

**Cookie hop vs bearer hops.** Auth has exactly one cookie edge and then bearer everywhere:

1. Browser ↔ Portal is a **cookie session** (`AddCookie` + `AddOpenIdConnect`, code flow + PKCE, in
   `Atrium.Portal/Program.cs`). The Portal is a confidential OIDC client (`atrium-portal`); its secret
   is injected by the AppHost as an env var (`Keycloak__PortalSecret`, set in `apphost.cs`), never in
   the repo.
2. On login, `OnTokenValidated` copies the raw **access token into a custom `access_token` claim** on
   the `ClaimsPrincipal`. This is the deliberate demo shortcut (see Gotchas). At render time
   `MainLayout` lifts that claim into a **scoped `AccessTokenHolder`**.
3. Each typed client (`CatalogClient` / `OrdersClient` / `ReportsClient`) reads the holder, sets
   `Authorization: Bearer …`, and calls the gateway.
4. YARP forwards the request **with the `Authorization` header intact**. The **gateway validates
   nothing.** The *target service* validates the JWT: Keycloak issuer, shared `atrium` audience,
   authorize by policy.

**Service discovery via `https+http://…`.** This scheme is the Aspire idiom: "prefer https, fall back
to http, for the service registered under this logical name." The Portal uses `https+http://gateway`;
the gateway's clusters use `https+http://catalog` / `https+http://storefront`; the Storefront vertical
uses `https+http://catalog` for its outbound client (`AddHttpClient<IStorefrontCatalogClient,
StorefrontCatalogClient>(client => client.BaseAddress = new Uri("https+http://catalog"))` in
`Atrium.Services.Storefront/Program.cs`); and even each service's **JWKS backchannel** resolves
`https+http://keycloak` the same way. The `WithReference(...)` calls in `apphost.cs` are what inject
those addresses into each consumer's config.

**Storefront → Catalog direct relay.** When the Storefront vertical needs product data it doesn't own
(pricing an order, bucketing report sales by category), it calls the Catalog **core service directly**
— not back through the gateway. `StorefrontCatalogClient.GetProductsAsync`
(`src/Atrium.Services.Storefront/Catalog/StorefrontCatalogClient.cs`) reads the caller's incoming
`Authorization` header off `IHttpContextAccessor` and **relays that same bearer** on the outbound call.
That works because a normal API request *has* an `HttpContext` (unlike a Blazor circuit — ADR-0004),
and the shared `atrium` audience is what lets Catalog accept the relayed token (ADR-0003, ADR-0005).

**One database per service.** `apphost.cs` provisions `catalogdb` and `storefrontdb` as **separate
databases** on one shared SQL Server instance (`sql.AddDatabase("catalogdb")` /
`sql.AddDatabase("storefrontdb")`). No cross-database joins, no second connection string into the
other schema. Storefront gets product data over the API like any other client. That's the property
the whole topology rests on (ADR-0005).

**What every service shares — via `Atrium.ServiceDefaults`.** One important nuance I should not
misstate: my `ServiceDefaults` is **telemetry only** — `AddAtriumTelemetry()` wires Serilog structured
logging + OpenTelemetry tracing (ASP.NET Core + HttpClient, plus SqlClient for the two data-owning
services) exported over OTLP so one trace spans Portal → Gateway → Service → SQL. It **deliberately
does not touch service discovery or health checks**; those are hand-wired per host
(`AddServiceDiscovery()`, `AddHealthChecks()`, `MapHealthChecks("/health")` in each `Program.cs`, and
`WithHttpHealthCheck("/health")` on the two services in `apphost.cs`). That's a departure from the
stock Aspire `ServiceDefaults` template, which bundles all four — I kept mine narrow on purpose.

---

## Why it's built this way

**Modular monolith, not micro-frontends, not a flat app** (ADR-0001). The premise is "several UI
areas that feel like independent apps but ship in one portal." The two extremes both lose: a single
flat Blazor app has **no boundary enforcement** — it rots into a big ball of mud (that's literally why
I'm rebuilding CozenDemo); real micro-frontends buy independent deploy but cost iframes / module
federation / cross-app routing / shared-shell problems with **no payoff at demo scale**. The modular
monolith gives me the *seam* — a module can't reach into another's internals (they share only
`Atrium.Abstractions` and `Atrium.Design` by type) and adding one is a project reference plus one
`IModule` class — **without paying for separate deployables yet**. The trade-off I accepted: all
modules rise and fall as one deploy. The exit is documented (BEYOND-THE-DEMO item 6), not built.
*Rejected: MEF/plugin framework — more machinery than a one-interface reflection scan needs.*

**Self-Contained Systems, not microservices, not a shared DB** (ADR-0005). The real decision is "how
does Storefront get product data it doesn't own?" The tempting shortcut — a cross-database join or a
second connection string into `catalogdb` — quietly couples the two services at the **schema** level
and destroys the "each service owns its data" property. So I split services into two shapes and let
them compose **over HTTP, never over the database**: core services own data and make no outbound
calls; app verticals own their own DB and compose cores. The trade-off is honest: **an extra network
hop and a partial-failure surface** — if Catalog is down, Storefront degrades. That's the real cost of
real isolation; caching/resilience (Polly) is a production concern I chose not to fake.
*Rejected: shared "products" library linked into both (moves coupling from DB to binary — same
problem); duplicating product data into `storefrontdb` (introduces a sync/staleness problem with no
benefit at this scale).* This is also why I didn't go **full microservices**: same isolation grain,
but a demo doesn't need N deployables, N pipelines, and a distributed-systems ops story to prove the
architecture.

**Gateway pass-through, not auth-at-the-edge** (ADR-0003). I put a YARP gateway in so the Portal knows
*one* address and a new service is a config route, not a Portal code change. But I deliberately do
**not** authenticate at the gateway. Auth lives at each service because (a) the shared `atrium`
audience means one token is valid everywhere, so edge-validation would just be duplicated work, and
(b) the **bearer-relay** pattern (ADR-0005) needs the token to arrive at the service unmolested so it
can be forwarded onward to Catalog as the same user. Terminating auth at the edge would break that
relay or force me to re-mint tokens. The trade-off: the gateway is "dumb," so a compromised network
segment between gateway and service is trusted — in production I'd close that with mTLS (see the last
section). *Rejected: per-service auth with no gateway (every service reimplements OIDC and the Portal
learns every address); Portal calls services directly (loses the single ingress and the config-driven
route table).*

**Direct core call, not back-through-the-gateway.** The Storefront→Catalog relay dials
`https+http://catalog` directly. Routing an internal east-west call back out through the public
ingress would add a pointless hop, double the latency, and conflate internal service topology with the
external route table. The gateway is my **north-south ingress**; east-west composition is
service-to-service by discovery address. The cost: Storefront has a compile-time-ish dependency on
Catalog's *address*, but that's exactly the SCS "vertical composes cores" contract, and it's abstracted
behind service discovery so it's config, not a hard-coded port.

**Route nesting under a service-root group** (ADR-0009). Each Storefront feature maps a **relative**
subtree onto one `app.MapGroup("/storefront").RequireAuthorization()` parent, instead of re-typing the
`/storefront` prefix and the auth call per feature. So the URL tree mirrors the feature-folder tree,
and shared auth is stated once. Catalog is the degenerate single-feature case — its one `/catalog`
group *is* the service root.

---

## What's impressive here / talking points

Things I'd steer toward:

- **The `IModule` reflection seam is genuinely load-bearing, not decoration.** The host has *zero*
  compile-time knowledge of any module — `ModuleLoader.Discover()` scans `Atrium.Modules.*`, each
  module self-registers its services (`RegisterServices`), and the shell surfaces its nav + home card.
  Adding a module is a project reference + one class. That's a real architectural property with a
  documented extraction path.
- **Two distinct service shapes with a crisp rule.** "Core owns data and makes no outbound calls; app
  vertical owns its own DB and composes cores over HTTP with a bearer relay." I can point at exactly
  where each lives and why Reports "landed cheaply" — it reused the *same* relay mechanism as pricing.
- **One database per service, enforced — not aspirational.** Two real databases, product data crosses
  the wire as `ProductDto`, never as SQL. I can explain the exact failure mode I refused (cross-DB
  join) and what it would have cost me.
- **End-to-end distributed tracing for free.** Because `ServiceDefaults` exports OTLP and Aspire
  injects the endpoint, one trace spans Portal → Gateway → Catalog/Storefront → SQL in the dashboard.
- **The auth model is *real*** — real OIDC code+PKCE to Keycloak, real JWT validation with a shared
  audience, real role policy (`admin` for writes) — not a fake login. And I can narrate the exact
  cookie-vs-bearer boundary.
- **Honesty as a feature.** Every shortcut has a home: ADRs for the "why," BEYOND-THE-DEMO for the
  "what's next," HANDOFF "Known limitations" for the warts. I designed the extraction seams *first*
  (that's the point), so every growth step is packaging/config, not a rewrite.

---

## Likely interview questions → strong answers

**Q: Why a modular monolith and not microservices from day one?**
A: Same isolation grain, far less ceremony. I get enforced boundaries (modules share only the
`IModule` contract and design primitives) and a clean extraction path without N deployables, N
pipelines, and a distributed ops story a demo can't honestly exercise. "Extract when it hurts" — the
seam is drawn so extraction is a packaging change (ADR-0001, BEYOND-THE-DEMO 6).

**Q: Walk me through an authenticated read, hop by hop.**
A: Browser→Portal is a cookie session. `MainLayout` lifts the access token from a claim into a scoped
`AccessTokenHolder`; the typed client attaches it as a Bearer and calls `https+http://gateway`. YARP
matches `/catalog/{**catch-all}`, resolves the `catalog` cluster via discovery, and forwards the
request *with the Authorization header*. Catalog validates the JWT (Keycloak issuer, `atrium`
audience) and authorizes — anonymous for `GET /catalog/products`, `admin` policy for writes. Nothing
in the chain re-mints or terminates the token except the final service.

**Q: The gateway does no auth? Isn't that a hole?**
A: It's a deliberate pass-through. One shared audience means every service already validates the same
token, so edge-validation would be duplicated work, and — more importantly — the bearer-relay to
Catalog needs the token to reach Storefront intact. The honest cost is that the gateway↔service
segment is trusted; in production I'd close that with mTLS or a service mesh, not by moving auth to the
edge. (ADR-0003.)

**Q: How does Storefront get product data it doesn't own — and why not just join the tables?**
A: One database per service is the property the topology rests on. A cross-DB join or a shared
connection string couples the two schemas and I'd never be able to evolve them independently.
Storefront calls Catalog over HTTP (`StorefrontCatalogClient`, direct to `https+http://catalog`) and
relays the caller's bearer via `IHttpContextAccessor`. It gets product data the same way any client
would, authorized as the same user. The cost is a network hop and a partial-failure surface — the
honest trade for isolation. (ADR-0005.)

**Q: Why does the relay call go direct instead of back through the gateway?**
A: The gateway is my north-south *ingress*. East-west composition is service-to-service by discovery
address. Bouncing an internal call back out through the public ingress adds a hop, doubles latency,
and tangles internal topology with the external route table for no gain.

**Q: Why can a service relay the bearer but a Blazor page can't just call `GetTokenAsync`?**
A: `HttpContext`. A normal API request has one, so Storefront reads the incoming Authorization header
and forwards it. A Blazor Server *circuit* has no `HttpContext` — it only exists for the initial
request that opens the SignalR connection — so the token can't be pulled from there at render time.
That asymmetry is why the Portal parks the token as a claim and lifts it into a scoped holder
(ADR-0004), while the service uses the clean relay (ADR-0005).

**Q: What does Aspire actually give you, and what happens in production without it?**
A: Local orchestration and, crucially, **service discovery** — the `WithReference`/`WaitFor` graph in
`apphost.cs` injects logical addresses so nothing hard-codes ports, and it provisions a database per
service and the OTLP endpoint. In production, discovery is a config/platform swap behind the same
`https+http://…` abstraction: Kubernetes DNS or a registry like Consul feeding the YARP route table.
Because nothing hard-codes ports today, that's config, not a rewrite (BEYOND-THE-DEMO 5).

**Q: How do you add a whole new service end-to-end?**
A: New `Atrium.Services.X` project with the Dapper/sprocs/DbUp/Mapperly recipe and its own database;
register it in `apphost.cs` (`.WithReference(db).WithReference(keycloak).WithHttpHealthCheck`); add one
gateway route (`/x/{**catch-all}` + cluster `https+http://x`); compose cores over HTTP where it needs
data it doesn't own. No Portal change. (BEYOND-THE-DEMO 1.)

**Q: When would Orders stop living inside the Storefront vertical?**
A: The moment a *second* slice needs orders (an Admin fulfillment view, a CS tool). Then orders are a
shared capability, not one vertical's private data, and I'd graduate Orders to its own **core service**
shaped exactly like Catalog. Storefront stops owning order tables and composes the Orders core the same
bearer-relay way. The composition pattern doesn't change — only who owns the data. (BEYOND-THE-DEMO 2.)

**Q: Admin and Reports are modules but have no backend — is that inconsistent?**
A: No — they don't own data yet. Admin writes through the Catalog core; Reports reads a
`/storefront/reports/sales` aggregate off the Storefront vertical (which itself composes Catalog for
the category map). Each grows its own API + DB the day it owns data the others don't — the vertical
template applied again. (BEYOND-THE-DEMO 1.)

**Q: Why Dapper + stored procedures instead of EF?**
A: (Points to ADR-0002.) It's a deliberate demonstration of explicit SQL ownership: DbUp runs
migrations once and stored procedures always as `CREATE OR ALTER`, Dapper executes, Mapperly maps rows
to DTOs at compile time. No hidden query generation, no migration magic — the data layer is as
inspectable as the topology.

**Q: What breaks if Keycloak is down?**
A: New logins fail (OIDC) and JWKS validation can't refresh — services reject tokens. Existing valid
tokens keep working until expiry. It's a hard identity dependency by design; in production Keycloak
would be HA. Note the demo has no token refresh (see Gotchas), so sessions are short-lived anyway.

---

## Gotchas & things that could trip you up

- **Module routing needs the assemblies registered in TWO places.** `<Router
  AdditionalAssemblies=Catalog.Assemblies>` in `Routes.razor` handles the interactive client-side
  router; **and** `MapRazorComponents<App>().AddAdditionalAssemblies([.. moduleCatalog.Assemblies])`
  in `Program.cs` handles server-side endpoint routing. Miss the second and module deep-links / SSR /
  refreshes **404 only on refresh** while in-app navigation works — a nasty intermittent. (ADR-0001.)
- **The gateway does NO auth.** If asked "where's the token validated," the answer is *the target
  service*, never the gateway. The gateway forwards the `Authorization` header untouched.
- **`ServiceDefaults` is telemetry-only here.** Don't claim it does discovery/health — those are
  hand-wired per host. (Stock Aspire's template bundles them; mine doesn't, on purpose.)
- **`MapInboundClaims = false` is load-bearing.** Keycloak's realm mapper emits a **flat `role`**
  claim and I set `RoleClaimType = "role"`. JWT-bearer's default `MapInboundClaims = true` renames
  inbound `role` to the long `ClaimTypes.Role` URI, so `RequireRole("admin")` finds nothing → **403
  for everyone, admins included.** Set on both Portal and the services. (ADR-0003, HANDOFF.)
- **The access token rides in the auth cookie as a custom claim** — a conscious demo smell. A Blazor
  circuit has no `HttpContext`, so I stash the token in the `ClaimsPrincipal` (`OnTokenValidated`) to
  carry it into the circuit. Consequence: a credential travels in the cookie (size bloat, no refresh,
  identity/credential conflation). Named replacement is **option B**: a server-side session-keyed token
  store, cookie down to a session id. (ADR-0004, HANDOFF.)
- **`SaveTokens = true` is not redundant.** It's what lets the OIDC handler send `id_token_hint` on
  RP-initiated logout so Keycloak 18+ skips the "confirm logout" interstitial. The access token is
  stored twice (properties + claim) because logout and the circuit each need it in a different place.
- **No token refresh; ~5-minute token life.** After expiry Catalog returns 401; clients map that to a
  typed `SessionExpiredException` and the shell shows a "session expired" panel instead of crashing the
  circuit (ADR-0008). Expiry itself is unfixed — prod path is `Duende.AccessTokenManagement`.
- **Stale cookie across restarts.** Cookies are per-host, not per-port; an old Portal cookie carrying a
  dead token can 500 the module pages after an Aspire restart. Workaround: `/account/logout` then log
  back in. (HANDOFF.)
- **Realm changes need a volume reset.** `WithRealmImport` only *creates* missing resources, so
  changing the realm means wiping the Keycloak data volume to re-import.
- **`https+http://` is a service-discovery scheme, not a URL.** If someone reads it as a typo, explain
  it's Aspire's "prefer https else http for this logical service name," resolved at runtime — that's
  why there are no ports in config.

---

## If they push deeper / how I'd evolve it

Credible next steps, each with the trade-off I'd weigh:

- **Extract a module or a vertical to an independent deploy.** The `IModule` seam makes UI-module
  independence a *packaging* change, not a rewrite: lightest is module-as-versioned-NuGet (independent
  *versioning*, host still redeploys), then runtime folder-drop / plugin load (independent *delivery*,
  costs assembly-load-context and isolation machinery), heaviest is micro-frontends (true independent
  runtime, costs cross-app routing + shared shell + design-system distribution). I'd only pay for the
  heavy option if independent UI deploy became a hard requirement. (BEYOND-THE-DEMO 6.)
- **Promote Orders to a core service** when a second slice needs it — pure "extract when it hurts,"
  and the composition pattern is unchanged. (BEYOND-THE-DEMO 2.)
- **Resilience on the east-west hop.** Today Storefront degrades if Catalog is down. I'd add Polly —
  timeouts, retries with jitter, a circuit breaker — and probably a short-TTL cache for the
  product→category map (it's read-heavy and slow-changing). Trade-off: caching introduces staleness I'd
  have to bound, so I'd tune TTL against how fresh reports need to be.
- **mTLS / a service mesh between gateway and services.** Closes the "gateway trusts the internal
  segment" gap without moving auth to the edge. Trade-off: cert lifecycle + operational weight (a mesh
  like Linkerd/Istio), which is why it's a production concern, not a demo one.
- **Real token management.** Swap the token-in-cookie for a server-side store (option B), then adopt
  `Duende.AccessTokenManagement` for refresh. Trade-off: a dependency and a token store to operate, in
  exchange for long-lived sessions and getting the credential out of the cookie.
- **Production service discovery + per-team gateway route self-registration.** Swap Aspire's dev
  discovery for K8s DNS or Consul behind the same `https+http://…` abstraction (config, not code), and
  let each service *declare its own* gateway route instead of a hand-edited central `appsettings.json`
  — the "the module owns its own surface" idea applied to ingress. (BEYOND-THE-DEMO 4, 5.)
- **Polyrepo + contracts as versioned NuGet** when team cadences diverge: publish `Atrium.Contracts`
  under SemVer so a producer ships without lockstep consumer rebuilds. The DTO-only guardrail
  (ADR-0006) keeps that package small and stable. Trade-off: version-pinning discipline and the risk of
  contract drift across repos. (BEYOND-THE-DEMO 3.)
