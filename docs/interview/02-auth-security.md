# Interview study — Auth & security

## The 90-second explanation

Atrium has two identity mechanisms and one identity provider. Keycloak is the IdP, running one
realm (`atrium`). The **Portal** — a Blazor Server app — is a **confidential OIDC client**
(`atrium-portal`) that does the interactive login: authorization-code flow **with PKCE**, and the
resulting session lives in an **encrypted auth cookie**. That cookie is the *only* cookie in the
system, and it exists on exactly one hop: **Browser ↔ Portal**.

Every hop *after* the Portal is a **Bearer JWT**. The Portal's typed HTTP clients attach the
signed-in user's access token to calls that go through the **YARP gateway** to the backend services
(Catalog, Storefront). Those services don't do OIDC — they're **JWT-bearer resource servers**: they
validate the token's issuer and signature against Keycloak's JWKS, require the shared **`atrium`
audience**, and authorize on a flat **`role`** claim. When an app vertical (Storefront) needs data
from a core service (Catalog), it **relays the caller's bearer token** onward, so the end user's
identity flows all the way to the service that owns the data.

So: **cookie for the browser session, JWT for everything machine-to-machine.** OIDC establishes who
you are once; the JWT carries that assertion to each service that needs to make an authorization
decision.

## How it actually works

Walking the token from login to a data read:

**1. Login — OIDC code+PKCE (Portal).** `src/Atrium.Portal/Program.cs` wires two schemes:
the default scheme is the **cookie** (`CookieAuthenticationDefaults`), and the default *challenge*
is **OpenIdConnect**. So an unauthenticated user hitting a protected surface gets challenged into
Keycloak. Config: `ResponseType = Code`, `UsePkce = true`, confidential client
(`ClientId = "atrium-portal"`, `ClientSecret` from `Keycloak:PortalSecret`), authority is the
realm URL resolved from Aspire service discovery. The client secret is injected by the AppHost as an
env var — it never lives in the repo (ADR-0003).

**2. Token capture — parked as a claim.** In `OnTokenValidated` the handler pulls
`context.TokenEndpointResponse.AccessToken` and adds it to the principal as a **custom `access_token`
claim**. `SaveTokens = true` is *also* set — but for a different reason (see below). The principal,
including that claim, is serialized into the auth cookie and rides back to the browser.

**3. Into the circuit — MainLayout → AccessTokenHolder.**
`src/Atrium.Portal/Components/Layout/MainLayout.razor` runs *inside* the Blazor circuit. In
`OnParametersSetAsync` it reads the cascading `AuthenticationState`, pulls
`User.FindFirst("access_token")`, and copies it into a **scoped `AccessTokenHolder`**
(`src/Atrium.Design/AccessTokenHolder.cs`) — a one-property per-circuit holder.

**4. Attach — typed clients.** Each module's typed client reads the holder and calls
`request.Authorize(tokens)` (`src/Atrium.Design/HttpClientExtensions.cs`), which sets
`Authorization: Bearer <token>` only if the holder is non-empty. The client is registered with
service discovery so it targets `https+http://gateway`. **No `DelegatingHandler`** (see Why).

**5. Gateway → service.** YARP forwards `/{catalog|storefront}/{**}` to the service cluster
(ADR-0003), carrying the bearer through untouched.

**6. Validate + authorize — the service.** `src/Atrium.Services.Catalog/Program.cs` and
`src/Atrium.Services.Storefront/Program.cs` call `AddKeycloakJwtBearer("keycloak", realm: "atrium")`
with `Audience = "atrium"`, `MapInboundClaims = false`, `NameClaimType = "preferred_username"`,
`RoleClaimType = "role"`. Authorization is policy-based: an `admin` policy
(`RequireRole("admin")`). Catalog's group is `RequireAuthorization()` but the two **reads**
(`GET /catalog/products`, `/categories`) opt back out with `.AllowAnonymous()`; the **writes**
(`POST`/`PUT /catalog/products`) require the `admin` policy. Storefront maps everything under
`/storefront` with `.RequireAuthorization()`, and Reports adds `.RequireAuthorization("admin")` on
top.

**7. Relay — slice calls core.** When Storefront needs product data it calls Catalog via
`src/Atrium.Services.Storefront/Catalog/StorefrontCatalogClient.cs`. This reads the **incoming**
request's `Authorization` header from `IHttpContextAccessor` and copies it onto the outbound request
— **relaying the caller's bearer**. This works because a Storefront **API request has an
`HttpContext`** (unlike the Blazor circuit). Storefront registers `AddHttpContextAccessor()` for
exactly this.

**8. Expiry — graceful.** There's no refresh. When the ~5-min token expires, the next call gets a
`401`. Every typed client calls `response.ThrowIfSessionExpired()`
(`src/Atrium.Design/SessionExpiredException.cs`) **before** `EnsureSuccessStatusCode()`, turning the
401 into a typed `SessionExpiredException`. A shell-level `SessionErrorBoundary` around `@Body`
catches it and shows "your session has expired — sign in again" instead of crashing the circuit
(ADR-0008).

**9. Step-up MFA — the agent.** The support-agent SSE endpoint at `/storefront/agent` is gated by a
custom `StepUpMfa` policy (`src/Atrium.Services.Storefront/Support/StepUpMfa.cs`) — authenticated
*plus* a step-up claim (Entra `amr` / Keycloak `acr`), with a Development-only simulate switch.

## Why it's built this way

**Two mechanisms, one IdP (ADR-0003).** OIDC is the right tool for an *interactive* user in a
browser — redirect-based login, a session the server manages. JWT bearer is the right tool for a
*resource server* that just needs to validate an assertion on each request without redirects or
session state. A single Keycloak realm and a **shared `atrium` audience** mean one access token is
accepted by every service, which is what makes the bearer-relay possible. Rejected: per-service auth
with no gateway (every service reimplements OIDC, Portal learns every address); a hand-rolled dev
IdP (wouldn't demonstrate a real OIDC+JWT+roles pipeline).

**Token-in-claim instead of a DelegatingHandler (ADR-0004 — the load-bearing decision).** In Blazor
**Server**, a running **circuit has no `HttpContext`** — `HttpContext` only exists for the initial
request that opens the SignalR connection. So a component can't call `HttpContext.GetTokenAsync(...)`.
The *obvious* fix — attach the token in a `DelegatingHandler` reading a scoped holder — **fails
outright**: `IHttpClientFactory` builds its handler chain in a **separate DI scope** from the
component's scope, so the scoped `AccessTokenHolder` read from inside the handler comes back
**empty**. That's why the token instead travels *inside the ClaimsPrincipal* (cookie) → is copied by
a component (`MainLayout`, which *is* in the circuit) into the scoped holder → and the typed clients
attach it themselves in code. It's not the elegant pattern; it's the one that actually works given
the scope boundary.

**Why `SaveTokens = true` AND the claim — not redundant.** They feed different consumers.
`SaveTokens` stores tokens in the auth properties, which is what lets the OIDC handler send
`id_token_hint` on RP-initiated logout (without it, Keycloak 18+ shows a "confirm logout"
interstitial). The `access_token` claim is what the circuit reads. The only true duplication is the
access token being stored in both places — because each consumer needs it in a different location.

**Relay the caller's token, don't mint a new one.** The end user's identity should reach the service
that owns the data, so authorization decisions there are made *as that user*. Relaying the existing
bearer (valid, correct audience) is the simplest thing that preserves identity end-to-end. Rejected
for the demo: a client-credentials service identity (loses the user context; the right *addition*
for prod, not a replacement — see hardening).

**`MapInboundClaims = false` (ADR-0003 — the gotcha that cost real time).** Keycloak's realm-role
mapper emits a **flat `role`** claim, and the services set `RoleClaimType = "role"`. But JWT-bearer
defaults to `MapInboundClaims = true`, which **renames** inbound `role` to the long
`ClaimTypes.Role` URI — so `RequireRole("admin")` matches nothing and returns **403 for everyone,
admins included**. Setting `MapInboundClaims = false` on Portal *and* both services keeps the short
names so the role match works.

**Role-gate in two places, on purpose.** The nav/home cards hide admin surfaces via `AuthorizeView`
(`NavMenu.razor`, `Home.razor`, driven by each module's `RequiredRole`), and pages carry
`[Authorize(Roles="admin")]`. But UI gating is **cosmetic** — the real enforcement is server-side
at the endpoint (`RequireAuthorization("admin")` on Catalog writes and Storefront Reports). Client
gating is UX; server gating is security. Both, always.

**Graceful expiry over inline refresh-hacks (ADR-0008).** We deliberately did *not* add token
refresh here — that's a bigger change (option B / Duende). Instead a 401 becomes a typed,
recoverable signal caught once at the shell, so an expected benign condition ("sign in again")
doesn't read as a crash.

## What's impressive here / talking points

- **The cookie/JWT seam is deliberate and I can defend both sides.** Cookie is Browser↔Portal only;
  everything onward is a stateless Bearer JWT validated against JWKS. That's the standard BFF-style
  shape, folded into the Blazor Server host because the server already holds the tokens.
- **I hit a real Blazor Server scope trap and understood *why* the textbook fix fails.** The
  `DelegatingHandler`-runs-in-a-different-scope gotcha is documented in ADR-0004 and in the
  `AccessTokenHolder` XML doc, not hand-waved.
- **End-to-end identity propagation** via token relay, and I know precisely why it's legal in one
  place (API request has `HttpContext`) and impossible in another (circuit doesn't).
- **Defense in depth on roles** — UI *and* endpoint — plus a concrete war story
  (`MapInboundClaims` → 403-for-everyone) that shows I understand the claims pipeline, not just the
  happy path.
- **Step-up MFA** that abstracts the cloud/local seam: identical handler, satisfied by Entra `amr`
  or Keycloak `acr`, with the simulate escape hatch **fenced to Development** so a stray config flag
  can't weaken a deployed gate.
- **I name the debt honestly** — token-in-cookie, no refresh — and can sequence the exact fix
  (option B token store → Duende.AccessTokenManagement).

## Likely interview questions → strong answers

**Q: How do you propagate identity across services?**
The user authenticates once at the Portal via OIDC; that yields an access token with the shared
`atrium` audience. The Portal attaches it as a Bearer on calls through the gateway. When a service
needs another service — Storefront calling Catalog — it **relays the same bearer** from the incoming
request (`StorefrontCatalogClient`, reading `IHttpContextAccessor`). One token, one audience, valid
at every hop, so the end user's identity reaches the service that owns the data and authorization is
evaluated as that user.

**Q: Why not a DelegatingHandler to attach the token?**
Because in Blazor Server it doesn't work. `IHttpClientFactory` builds the handler chain in a
*separate DI scope* from the component's scope, so a scoped `AccessTokenHolder` read inside the
handler is empty. On top of that, the running circuit has no `HttpContext` to fetch the token from
in the first place. So I park the token as a claim in the cookie, have `MainLayout` (which runs in
the circuit) copy it into the scoped holder, and the typed clients attach it explicitly. Documented
in ADR-0004.

**Q: How do you handle token refresh / expiry?**
Honest answer: in the demo I don't refresh. The token is captured once at login and expires in
~5 minutes. I handle that *gracefully* rather than crash: every typed client maps a 401 to a typed
`SessionExpiredException` before `EnsureSuccessStatusCode()`, and a shell `SessionErrorBoundary`
renders "sign in again" (ADR-0008). The real fix is silent refresh — option B (a session-keyed token
store) as the cheap step, then `Duende.AccessTokenManagement` for actual refresh.

**Q: Why is the access token in the cookie? Isn't that a smell?**
Yes, and I name it in ADR-0004. A cookie carries *identity*; an access token is a *credential*.
Putting the credential in the identity cookie conflates the two and bloats the cookie. It's a
deliberate demo shortcut. The fix — option B — keeps only a session id in the cookie and reads the
token from a server-side store keyed by that id, same `AccessTokenHolder` shape so no call sites
change.

**Q: How would you do service-to-service auth in production?**
Two layers. Keep the user-token relay for user-initiated calls so authorization stays user-scoped.
Add a **service identity** — client-credentials tokens (or SPIFFE/mTLS) — for calls with no user
context (background jobs, agent tools acting on their own behalf), and validate both the user
assertion and the calling service. Prod would also move token acquisition/refresh to
`Duende.AccessTokenManagement`.

**Q: Where could this leak another user's data?**
Main risk is the **scoped `AccessTokenHolder`**: it must be per-circuit, and `MainLayout` must
repopulate it on `OnParametersSetAsync`. If it were mis-scoped to singleton, or a client cached a
token across users, you'd relay user A's bearer for user B. It's `AddScoped`, one-property, written
once, populated from the *current* auth state each parameter set — that's the safeguard. Second risk
is the relay: `StorefrontCatalogClient` copies whatever bearer is on the *incoming* request, so it's
only ever the current caller's token, never a stored one.

**Q: Why OIDC for the Portal but JWT for the services?**
OIDC is an interactive, redirect-based protocol for a human in a browser establishing a session.
JWT bearer is for a resource server validating a self-contained assertion per request with no
session and no redirects. Different jobs. Same IdP, same realm, so it's one identity story with two
transports (ADR-0003).

**Q: What's PKCE and why do you use it?**
Proof Key for Code Exchange. The client generates a secret verifier, sends its hash (challenge) on
the authorize request, and the verifier on the token exchange — so an intercepted authorization code
is useless without the verifier. It's mandatory for public clients and best practice for
confidential ones, which is why `UsePkce = true` even though the Portal is confidential with a
secret. Defense in depth on the code.

**Q: How does the gateway fit the auth story?**
YARP is pure transport for auth purposes — it forwards the Bearer untouched and does **not**
terminate or validate it. Validation happens at each service. The gateway gives me one ingress and a
config-driven route table; it deliberately isn't a policy enforcement point in this design (a prod
option would be to validate at the edge too).

**Q: How are roles enforced, and where?**
Keycloak stamps a flat `role` claim. Services set `RoleClaimType = "role"` and define an `admin`
policy (`RequireRole("admin")`). Enforcement is at the endpoint: Catalog writes and Storefront
Reports require `admin`; reads are anonymous or just-authenticated. The UI *also* hides admin nav and
pages (`AuthorizeView`, `[Authorize(Roles="admin")]`) but that's cosmetic — the server is the
authority.

**Q: You said 403-for-everyone once — what happened?**
`MapInboundClaims` defaults to true, which renamed the inbound `role` claim to the long
`ClaimTypes.Role` URI, so `RequireRole("admin")` — matching on `"role"` — found nothing and forbade
everyone, admins included. Fix: `MapInboundClaims = false` on Portal and both services. It's in
ADR-0003 so nobody re-debugs it.

**Q: What does the access-denied path do?**
A wrong-role user reaching a gated route by full-page GET (deep link, refresh, bookmark) is denied
at the endpoint. I set `AccessDeniedPath = "/forbidden"` because the default `/Account/AccessDenied`
isn't a real route here and would fall through to "Not Found". So they get a clean Forbidden page.

**Q: How does step-up MFA work on the agent?**
The `/storefront/agent` endpoint uses a custom `StepUpMfa` policy: require an authenticated user
*first* (so anonymous → 401), then require a step-up claim (authenticated but no step-up → 403). The
claim is satisfied by an Entra `amr` value (mfa/otp/hwk/sms) **or** a Keycloak `acr` value — one
handler, both clouds. There's a `Simulate` switch for local dev, but it's honored **only in the
Development environment**, so a stray `Simulate=true` in a deployed config can't bypass the real
ceremony.

**Q: How do you log auth failures without leaking tokens?**
The typed clients and the relay client log a structured warning at the downstream seam — method,
path, status, and "session expired" vs generic — but **never** the auth header or token value
(`HttpClientExtensions.cs`, `StorefrontCatalogClient.cs`).

## Gotchas & things that could trip you up

- **The circuit has no `HttpContext`.** This is the root of the whole token-in-claim design. If you
  forget it, the whole approach looks over-engineered. It isn't — it's forced.
- **`DelegatingHandler` runs in a different scope.** Don't claim the typed clients attach the token in
  a handler; they don't, and the reason is the scope boundary. The precise rule: no *factory-registered*
  handler may read circuit state — the AI slice's `BearerTokenHandler` is legitimate because it's
  composed manually **inside** the circuit scope (ADR-0011).
- **`MapInboundClaims = false` must be on Portal *and* services.** Miss it anywhere roles are
  checked and you get silent 403s.
- **`SaveTokens = true` is not redundant with the claim.** It's specifically for `id_token_hint` on
  logout. Don't say "I store it twice for no reason."
- **Catalog reads are `AllowAnonymous`.** The group is `RequireAuthorization()` but per-endpoint
  `AllowAnonymous` metadata wins. So a relayed token isn't strictly *required* for a product read —
  it's still relayed, but reads work without one. Know this so you don't overstate the gate.
- **Stale cookie across restarts.** Cookies are per-host not per-port, so an old Portal cookie with a
  dead token can 500 the storefront after an Aspire restart until you re-login (ADR-0004).
- **Simulate is Development-only.** State that clearly — it's the difference between a demo
  convenience and a security hole.
- **The token holder must stay scoped.** If asked "what if it were a singleton," the answer is
  cross-user token leakage. Know that failure mode cold.

## If they push deeper / how I'd harden it for production

1. **Get the credential out of the cookie — option B (ADR-0004).** In `OnTokenValidated`, capture
   tokens into a **session-keyed server-side store** (or an `ITicketStore` backing the cookie); the
   cookie then carries only a session id. Surface the current token to the circuit via the same
   scoped-service shape, so no call site changes. Cheap, removes the cookie smell.

2. **Then add silent refresh — `Duende.AccessTokenManagement`.** This is the step *after* option B:
   it manages token lifetime and refreshes transparently, which retires the "no refresh / sign in
   again" limitation entirely. ADR-0008's graceful handling stays as a backstop.

3. **A real service identity for non-user calls.** Client-credentials (or mTLS/SPIFFE) for
   background and agent-initiated work, so those aren't dependent on a relayed user token. Keep the
   relay for user-initiated calls.

4. **Validate at the edge too.** Optionally have the gateway validate the JWT (fail fast, shed load)
   in addition to per-service validation — defense in depth, not a replacement.

5. **Tighten the token surface.** Shorter access-token lifetime with refresh; audience/scope
   narrowing per service instead of one shared `atrium` audience if isolation matters; sender-
   constrained tokens (DPoP/mTLS) so a stolen bearer isn't replayable.

6. **Turn on real step-up in the cloud.** Flip `SupportAgent:StepUp:Enabled`, drop `Simulate`, and
   let Entra `amr` / Keycloak `acr` drive the gate — the handler already supports it unchanged.

7. **Cookie hardening / secret management.** Data-protection keys persisted and shared across
   instances; secrets from a vault (the Portal secret already comes from env, not the repo);
   `RequireHttpsMetadata` is already environment-gated to true outside Development.
