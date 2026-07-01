# Atrium on a JS/TS stack — Option A vs Option B

A design exploration: *if* Atrium were rebuilt on a 100% open-source, no-vendor-lock JS/TS
stack, what would it look like, and where's the real architectural fork? This is a thought
document, not a migration plan. It exists to make the trade-offs concrete.

The **anti-lock filter** driving every pick: MIT/foundation-governed, deploy-anywhere without
degradation, no commercial platform benefits from you using it. That rules out Next/Vercel-style
frameworks and Prisma; it lets in TanStack, React Router v7, Fastify, Hono, Astro, SvelteKit, etc.

---

## 1. The baseline — Atrium as it exists (.NET)

Three server tiers plus shared packages. The **Portal is a stateful server** that holds the user's
token; the **gateway is a dumb reverse proxy**; the **services own their own databases**.

```
                         ┌──────────────┐
                         │  Keycloak    │  OIDC (portal) + JWT (services)
                         └──────────────┘
                            ▲         ▲
                    OIDC    │         │  validate JWT
                            │         │
  browser   ┌───────────────┴───┐  ┌──┴──────────┐   ┌────────────────────┐
 ─────────▶ │ Atrium.Portal     │  │ Atrium.     │──▶│ Catalog (core)     │
   (cookie) │ Blazor Server     │─▶│ Gateway     │   │  owns catalogdb    │
            │ • holds token     │  │ (YARP)      │   └────────────────────┘
            │ • module discovery│  │ dumb proxy  │   ┌────────────────────┐
            │ • typed clients   │  └─────────────┘──▶│ Storefront (app)   │
            └───────────────────┘                    │  owns storefrontdb │
                                                      │  ── relays bearer ─┼──▶ Catalog
                                                      └────────────────────┘
```

**Key property:** the access token never reaches the browser. It's captured server-side at login
and parked in the Portal (auth cookie / circuit). Everything the browser sees is server-rendered.

The tiers we'll compare against:

| Atrium project | Role |
|---|---|
| `Atrium.Portal` | **Stateful server.** Module discovery (reflection over `IModule`), OIDC login, token capture, typed clients. |
| `Atrium.Gateway` | **Dumb reverse proxy** (YARP). Routes `/catalog/**`, `/storefront/**`. |
| `Atrium.Services.Catalog` | **Core service** — owns `catalogdb`, no cross-service calls. |
| `Atrium.Services.Storefront` | **App vertical** — owns `storefrontdb`, composes Catalog over HTTP, relays bearer. |
| `Atrium.Abstractions` | `IModule` + `NavItem` contract. |
| `Atrium.Contracts` | Wire DTOs. |
| `Atrium.Design` | Design-system RCL (tokens + primitives). |
| `Atrium.Modules.*` | UI modules (RCLs) with typed clients. |
| `Atrium.ServiceDefaults` | Telemetry / health / resilience. |

---

## 2. What A and B share (the foundation)

Both options are identical everywhere **except the frontend/token tier**. Shared picks, all
anti-lock:

| Concern | Pick | Atrium analog |
|---|---|---|
| Monorepo | pnpm workspaces + Docker Compose | Aspire AppHost |
| Build | Vite | (msbuild) |
| Backend services | **Fastify** (core + vertical) | `Atrium.Services.*` |
| Data access | **Kysely** (typed SQL) + `mssql`/`pg` driver | Dapper |
| Migrations / sprocs | Kysely migrations / Umzug, two-lane | DbUp |
| Row → DTO | **Zod** `.parse()` | Mapperly |
| Contracts | `packages/contracts` — Zod schemas → inferred types | `Atrium.Contracts` |
| Design system | `packages/design` — React + Radix + CSS-var tokens | `Atrium.Design` |
| Module contract | `packages/module-contract` — TS `Module` interface | `Atrium.Abstractions` |
| Module discovery | **registry array** (each module exports a `Module`) | reflection over `IModule` |
| UI modules | `packages/modules/*` — routes + nav + typed client | `Atrium.Modules.*` |
| Identity | Keycloak (unchanged) + `openid-client` / `jose` | Keycloak |
| Observability | OpenTelemetry + health + retry | `Atrium.ServiceDefaults` |

So the **entire decision reduces to one question**: *where does the token-holding BFF tier live?*

---

## 3. The fork, in one sentence

- **Option A** — the frontend is a **pure static SPA (TanStack Router)** with *no server of its own*,
  so the **Fastify gateway is promoted to a BFF** and becomes the token holder. The Portal server
  tier disappears.
- **Option B** — the frontend is a **framework with its own server (TanStack Start / React Router
  v7)**, so **that server is the BFF/Portal tier** and the gateway stays a dumb proxy — exactly
  Atrium's topology.

Everything below elaborates that one fork.

---

## 4. Option A — TanStack Router SPA + Fastify gateway-as-BFF

### Tree

```
atrium-web/  (pnpm workspaces + Docker Compose)
│
├── apps/
│   ├── portal/                     # TanStack Router SPA — STATIC build (Vite)
│   │     • typed route tree + module registry
│   │     • design shell (nav, layout)
│   │     • NO server — ships to any static host / CDN / nginx
│   │
│   ├── gateway-bff/                # Fastify — reverse proxy + BFF (the token tier)
│   │     • /auth/login /callback  → OIDC dance (openid-client)
│   │     • server-side session    → httpOnly, Secure, SameSite cookie
│   │     • token refresh rotation (browser never sees a token)
│   │     • proxy /catalog/** /storefront/**  → attaches bearer server-side
│   │
│   └── services/
│       ├── catalog/                # Fastify core — owns catalogdb
│       └── storefront/             # Fastify app vertical — owns storefrontdb, calls catalog
│
└── packages/
    ├── module-contract/            # Module TS interface        (≈ Atrium.Abstractions)
    ├── contracts/                  # Zod DTOs                    (≈ Atrium.Contracts)
    ├── design/                     # React + Radix + tokens      (≈ Atrium.Design)
    ├── modules/ storefront|admin|reports   #                     (≈ Atrium.Modules.*)
    └── service-defaults/           # OTel / health / retry       (≈ Atrium.ServiceDefaults)
```

### Request flow (authenticated read)

```
browser (SPA)                 gateway-bff (Fastify)              services
    │  GET /storefront (route load)                                  │
    │  fetch same-origin /catalog/products                           │
    │  ── cookie: sid=… ─────────▶ │ look up session by sid          │
    │                              │ attach Authorization: Bearer …  │
    │                              │ ── proxy /catalog/products ────▶ │ validate JWT
    │                              │                                  │ authorize (role)
    │                              │ ◀──────────── 200 JSON ───────── │
    │ ◀──────── 200 JSON ───────── │                                 │
```

The SPA only ever talks to **its own origin** (the BFF). No token in JS. No CORS.

### How it differs from Atrium — and the pros/cons of each difference

| Difference vs Atrium | Pro | Con |
|---|---|---|
| **Portal is static assets, not a server** | Deploy the whole frontend to any CDN/nginx; trivially scalable and cacheable; zero server state to attack (no circuits) | Any "server-side" concern (BFF, SSR) must go *somewhere else* — here, the gateway |
| **Gateway is promoted from dumb proxy → BFF** | Collapses a tier: one stateful trust boundary instead of Portal+Gateway; the gateway already existed, so no new box | You hand-write session/cookie/refresh/proxy in Fastify (~100–150 lines); the gateway is now stateful (session store) |
| **Frontend rendered client-side (SPA)** | Simplest mental model you already use (React Router-style); ships as one artifact | No SSR → slower first paint, weaker SEO (fine for an authenticated portal), app logic shipped to browser |
| **Token lives in gateway session** | Same secrecy as Blazor Server (token never in browser); refresh handled server-side, fixing Atrium's "no refresh" gap | Requires a session store (in-memory for dev, Redis for scale) — a new stateful dependency |
| **TanStack Router for routing** | Best-in-class type safety (typed params + search params); pure library, max anti-lock | Router is stable but you assemble more yourself; team must learn its loader/search-param model |

### Where Atrium's concepts land

- **Module discovery** → registry array in the SPA; Vite code-splits each module by route. (Reflection
  becomes explicit imports — less magic, fully typed.)
- **Bearer relay (vertical → core)** → unchanged; Storefront service relays the bearer to Catalog
  server-side, exactly as today.
- **"Portal holds the token"** → moves to the gateway-BFF. The role didn't disappear; it relocated.

---

## 5. Option B — TanStack Start (or React Router v7) + dumb gateway

### Tree

```
atrium-web/  (pnpm workspaces + Docker Compose)
│
├── apps/
│   ├── portal/                     # TanStack Start / React Router v7 — SSR framework
│   │     • typed route tree + module registry + design shell
│   │     • server functions / loaders = BFF:
│   │         OIDC login, server-side session, httpOnly cookie, refresh
│   │     • loaders proxy to gateway with bearer attached
│   │     • THIS tier holds the token  (mirrors Atrium.Portal)
│   │
│   ├── gateway/                    # Fastify — DUMB reverse proxy (like Atrium's YARP)
│   │     • routes /catalog/** /storefront/** to service clusters
│   │
│   └── services/
│       ├── catalog/                # Fastify core — owns catalogdb
│       └── storefront/             # Fastify app vertical — owns storefrontdb, calls catalog
│
└── packages/                        # identical to Option A
    ├── module-contract/  ├── contracts/  ├── design/  ├── modules/*  └── service-defaults/
```

### Request flow (authenticated read)

```
browser            portal (Start/RR7 server)        gateway (dumb)        services
   │ GET /storefront                                     │                    │
   │ ── cookie: sid ──▶ │ loader runs SERVER-side        │                    │
   │                    │ read session → bearer          │                    │
   │                    │ fetch gateway /catalog/products│                    │
   │                    │ ──────────────────────────────▶│ ── route ────────▶ │ validate JWT
   │                    │                                 │                    │ authorize
   │                    │ ◀───────────────── 200 JSON ────┼──────────────────  │
   │ ◀── SSR'd HTML ─── │ (or JSON for client nav)        │                    │
```

The token stays inside the framework's server. The browser gets SSR'd HTML + an httpOnly cookie —
the closest analog to Blazor Server's "browser sees only rendered output."

### How it differs from Atrium — and the pros/cons of each difference

| Difference vs Atrium | Pro | Con |
|---|---|---|
| **Portal is a JS SSR server (not Blazor)** | Preserves Atrium's 3-tier topology exactly; SSR gives fast first paint + SEO; sensitive logic stays server-side | You're running (and scaling) a stateful-ish frontend server again — more ops than a static SPA |
| **Gateway stays a dumb proxy** | Matches Atrium 1:1; gateway stays simple and stateless | You now have *two* server tiers to run (portal + gateway) vs Option A's one |
| **BFF is idiomatic (loaders/server fns)** | Little hand-wiring; token handling falls out of the framework's data layer | You inherit the framework's conventions — more framework surface than Option A |
| **TanStack Start specifically** | TanStack type safety *with* a server; Nitro = deploy anywhere | **Youngest of the three** — smaller ecosystem, API still settling; early-adopter risk |
| **React Router v7 specifically** | Battle-tested (Remix heritage); the conservative version of this shape | Slightly less thorough type safety than TanStack; more "framework" feel |

### Where Atrium's concepts land

- **Module discovery** → registry array, same as A, but rendered/SSR'd by the framework.
- **"Portal holds the token"** → stays in the Portal tier (now a JS server). This is the **most
  faithful** mapping of Atrium's design.
- **Gateway** → unchanged role (dumb proxy), unchanged from Atrium.

---

## 6. Head-to-head

| Axis | Option A (SPA + gateway-BFF) | Option B (Start / RR7 + dumb gateway) |
|---|---|---|
| Server tiers to run | **2** (gateway-BFF, services) | 3 (portal, gateway, services) |
| Fidelity to Atrium topology | Lower (Portal→static, gateway→BFF) | **Higher** (same 3 tiers, same roles) |
| Frontend artifact | Static — any CDN/nginx | Node/Nitro server |
| First paint / SEO | Client-rendered | **SSR** |
| BFF effort | Hand-wired in Fastify (~150 LOC) | **Idiomatic** (loaders/server fns) |
| Type safety | **Highest** (TanStack Router) | High (Start) / Good (RR7) |
| Maturity | Router stable; you own the glue | **RR7 very mature**; Start young |
| New stateful dependency | Session store on gateway | Session store on portal |
| "Assemble it yourself" fit | **Strong** | Moderate |
| Token secrecy (vs Blazor Server) | Equal | Equal |
| Anti-lock | **Max** (library + your glue) | High (open framework) |

**Security is a wash.** Both keep the token server-side (BFF), both need an httpOnly cookie + CSRF
defense + strict CSP. Neither has Blazor Server's stateful-circuit DoS surface. See the security
discussion in the conversation that spawned this doc.

---

## 7. How to choose

Pick **A** if you value: a **pure static frontend**, the **fewest server tiers**, the **strongest
type safety**, and you *like* owning the BFF glue as clarity rather than chore.

Pick **B** if you value: **fidelity to Atrium's existing topology**, **SSR**, and a BFF that's
**idiomatic instead of hand-written** — with **React Router v7** as the low-risk pick and **TanStack
Start** as the "same idea, newer, more type-safe, more early-adopter risk" pick.

**The tell:** if hand-writing the Fastify BFF proxy (session → cookie → refresh → proxy) sounds like
*clarity*, go A. If it sounds like *a chore you'd rather the framework owned*, go B — and default to
React Router v7 unless you specifically want TanStack's search-param type safety enough to accept
Start's youth.

---

## 8. One-glance mapping: Atrium → A → B

| Atrium (.NET) | Option A | Option B |
|---|---|---|
| `Atrium.Portal` (stateful server) | `apps/portal` **static SPA** (TanStack Router) | `apps/portal` **SSR server** (Start / RR7) |
| token holder | **gateway-BFF** | **portal server** |
| `Atrium.Gateway` (dumb proxy) | `apps/gateway-bff` (**proxy + BFF**) | `apps/gateway` (**dumb proxy**, unchanged) |
| `Atrium.Services.Catalog` | `apps/services/catalog` (Fastify) | same |
| `Atrium.Services.Storefront` | `apps/services/storefront` (Fastify) | same |
| `Atrium.Abstractions` | `packages/module-contract` | same |
| `Atrium.Contracts` | `packages/contracts` (Zod) | same |
| `Atrium.Design` | `packages/design` (React + Radix) | same |
| `Atrium.Modules.*` | `packages/modules/*` | same |
| `Atrium.ServiceDefaults` | `packages/service-defaults` | same |
| Aspire AppHost | pnpm workspaces + Docker Compose | same |
| Dapper / DbUp / Mapperly | Kysely / migrations / Zod | same |
| Keycloak | Keycloak (unchanged) | same |

The single difference to internalize: **A moves the token tier down into the gateway and makes the
frontend static; B keeps the token tier in a frontend server and keeps the gateway dumb — which is
Atrium's exact shape.**
