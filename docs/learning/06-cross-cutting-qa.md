# Interview study — Cross-cutting Q&A, gotchas & the honest-answer playbook

The connective tissue: the subtle mechanics that span subsystems, a rapid-fire drill, and how to handle the
questions you can't fully answer. Read this last, and re-read it the morning of.

## Where the bodies are buried (know every one)

These are the non-obvious mechanics an interviewer who reads the code will find. Each has a documented home.

- **Bearer token uses no `DelegatingHandler`.** `IHttpClientFactory` builds handler instances in a
  *separate* DI scope from the Blazor circuit, so a handler that read the **scoped** `AccessTokenHolder`
  would read it empty. Instead the typed clients attach the token explicitly from the circuit-scoped holder.
  (The AI slice's `BearerTokenHandler` *is* a `DelegatingHandler`, but it's composed **inside** the circuit
  scope by `AgentChatClientFactory`, precisely because the AG-UI client owns its own `HttpClient` and there's
  no other seam — different constraint, deliberate exception, now formally recorded.) → ADR-0004, ADR-0011.
- **The access token rides in the auth cookie as a custom claim.** A Blazor Server *circuit* has no
  `HttpContext` (that exists only for the initial SignalR-opening request), so a component can't call
  `GetTokenAsync`. Workaround: `OnTokenValidated` parks the raw access token as a claim on the
  `ClaimsPrincipal`, which rides into the circuit; `MainLayout` copies it into the scoped `AccessTokenHolder`.
  This conflates identity with a credential and bloats the cookie — an accepted demo smell with a documented
  fix (option B: a server-side token store, cookie holds only a session id). → ADR-0004 / HANDOFF.
- **`SaveTokens = true` is not redundant with the claim.** It's what lets the OIDC handler send
  `id_token_hint` on RP-initiated logout (Keycloak 18+ otherwise shows a "confirm logout" interstitial). The
  only true duplication is the access token specifically being stored twice (logout needs it in one place,
  the circuit in another).
- **Roles need `MapInboundClaims = false` + `RoleClaimType = "role"`.** Keycloak emits a flat multivalued
  `role` claim; the legacy inbound map would rename it and `RequireRole("admin")` would match nothing — the
  "403 for everyone" gotcha. Set on the Portal *and* both services. → ADR-0003.
- **Module routing registers assemblies in TWO places** — the `<Router AdditionalAssemblies>` (interactive
  client-side routing) *and* `MapRazorComponents().AddAdditionalAssemblies()` (server-side endpoint routing
  for deep-links / static SSR). Miss either and some navigations 404. → ADR-0001.
- **`Atrium.ServiceDefaults` is telemetry-only.** Unlike the stock Aspire template, it deliberately does
  *not* add service discovery or health checks — those are hand-wired per host (`AddServiceDiscovery()`,
  `AddHealthChecks()`/`MapHealthChecks`, and `WithHttpHealthCheck` in the AppHost). Know this if the
  interviewer knows Aspire.
- **Catalog reads are `AllowAnonymous`.** `GET /catalog/products` and `/categories` succeed without a token
  so the storefront browses signed-out; the per-endpoint `AllowAnonymous` overrides the group's
  `RequireAuthorization`. The bearer matters for **writes** (admin-gated) and authenticated surfaces, not
  product reads — don't overstate the gate.
- **The cart is circuit-scoped.** A full-page navigation (browser `goto`) starts a fresh empty circuit and
  loses the cart; in-app `NavLink` clicks preserve it. Cart survival across the OIDC login round-trip is
  handled by `CartPersistence` (localStorage). → checkout-flow diagram.
- **Storefront→Catalog price relay goes direct**, not back through the gateway (`https+http://catalog`), and
  relays the caller's bearer via `IHttpContextAccessor` — valid because an API request *has* an `HttpContext`
  (a circuit does not). → ADR-0005.
- **DbUp Programmability runs every startup** (`CREATE OR ALTER` sprocs); Migrations run once. SQL files are
  embedded resources executed by `DatabaseInitializer`. → ADR-0002.

## Rapid-fire drill (say the answer out loud, then check)

1. **Why modular monolith, not microservices?** One deploy/debug story and no distributed-systems tax, but
   hard module boundaries (the `IModule` contract) so it can split later. Microservices would be premature
   for this scope. → ADR-0001.
2. **Why a gateway if it's mostly one portal?** Single ingress + one address for the client to know; the
   services stay addressable only via discovery, and it's the seam where cross-cutting concerns (routing,
   later: rate-limit/resilience) live. It does **no auth** — it forwards the bearer, the service validates.
3. **Core vs app vertical?** Core (Catalog) owns data, makes no cross-service calls. App vertical
   (Storefront) owns its own DB *and* composes cores over HTTP. → ADR-0005.
4. **Why one DB per service?** No cross-service SQL coupling — Storefront gets products over HTTP, so
   Catalog can change its schema freely. The cost is no cross-domain joins (you compose in code).
5. **Why Dapper + sprocs, not EF?** Explicit SQL, a clear DBA-reviewable surface, compile-time mapping via
   Mapperly, no query-translation surprises. Trade-off: more boilerplate, sprocs to maintain. → ADR-0002.
6. **How do migrations work?** DbUp, two lanes — Migrations run-once (schema+seed), Programmability
   run-always (`CREATE OR ALTER` sprocs), SQL as embedded resources, applied at service startup.
7. **How is identity propagated across services?** Cookie only browser↔portal; every hop after is the user's
   bearer JWT — gateway forwards it, the service validates issuer + `atrium` audience.
8. **How do modules get discovered?** Reflection: `ModuleLoader` finds `IModule` implementations →
   `ModuleCatalog` singleton → drives nav, home cards, and the router. Adding one = project ref + `IModule`.
9. **How do you keep UI consistent across modules?** A design-system RCL: tokens (`tokens.css`) + shared
   primitives; modules consume tokens, never ad-hoc CSS. → ADR-0010 for the native `<dialog>`.
10. **How is order creation safe under retries?** A **user-scoped** idempotency key + `IsNew` flag from the
    create sproc, inside a transaction — a replay returns the original order id and skips re-adding lines;
    the response is the order **read back from the DB**. A concurrent double-submit is settled by TRY/CATCH
    on the unique index; replaying another user's key is a 409, never their order.
11. **Where's the authz boundary for reading an order?** The sproc `WHERE Id=@OrderId AND UserName=@UserName`
    — a non-owned order returns null. Not in app code, not in the prompt.
12. **How do you test the data layer?** Testcontainers SQL integration tests against the real sprocs;
    repository interfaces co-located with an integration test. → ADR-0007.
13. **(AI) singleton agent, per-request data — safe how?** Agent captured once; tools resolved per
    invocation from the request scope, so each tool call sees the current caller + their scoped connection.

## Likely "how would you change it" prompts

- **"Split Storefront into real microservices."** Extract behind its own gateway route + DB (already
  separate), replace the direct Catalog HTTP call with a resilient client (retries/circuit-breaker/timeouts),
  add real service-to-service auth (client-credentials or mTLS), and move to async messaging (outbox) where
  eventual consistency is acceptable. The `IModule`/SCS boundaries were chosen to make this a refactor, not
  a rewrite. → BEYOND-THE-DEMO.
- **"Production-harden auth."** Move the token out of the cookie (server-side store / `ITicketStore`), add
  refresh via `Duende.AccessTokenManagement`, use managed identity for cloud service-to-service, and put the
  step-up-MFA gate on by default outside Dev (the code already warns when it's inert). → ADR-0004.
- **"Handle a module failing."** Today a module is in-process; isolation would mean loading modules into
  their own assembly-load-contexts (or out-of-process) and degrading the shell gracefully when one fails to
  register — a trade-off against the simplicity that makes the monolith worth it.
- **"Add resilience/observability."** Serilog + OpenTelemetry over OTLP to the Aspire dashboard already give
  traces portal→gateway→service→SQL; I'd add `Microsoft.Extensions.Http.Resilience` on the typed clients and
  SLO-based alerting.

## The honest-answer playbook

- **Never bluff a mechanism.** If you don't know an exact implementation detail, give the senior answer:
  *"I'd verify X, but the design intent is Y."* Interviewers respect calibrated uncertainty; they punish
  confident wrongness.
- **Steer to trade-offs.** When you know an area, don't just describe it — name the alternative you rejected
  and why. That's the architect signal.
- **Own the shortcuts.** Token-in-cookie, no refresh, simulated payments, ephemeral agent threads — these are
  *documented, deliberate* demo boundaries with production paths. Stating them first makes you look
  rigorous, not exposed. → BEYOND-THE-DEMO, HANDOFF "Known limitations".
- **On "you used AI":** you directed it, set the conventions, and reviewed it (Run 4 was a structured
  multi-agent review of the AI slice that you triaged and fixed). Then go deep on a decision to prove
  ownership. See [00-README](00-README.md).

## Two-minute pre-interview refresh

Cookie is browser↔portal only · bearer every hop after · gateway forwards, service validates · no
DelegatingHandler (separate scope) · token-in-cookie is the accepted smell · roles need
`MapInboundClaims=false` · assemblies in two places · one DB per service, products over HTTP · Dapper+sprocs
+DbUp two lanes, no EF · idempotent orders · authz in the sproc WHERE · reflection module discovery · agent
singleton / tools request-scoped · ServiceDefaults is telemetry-only.
