# Interview study — Open questions & clarifications

My personal study backlog: the things I flagged while reading docs 01–06 that I want to be able to
explain cold, plus concrete changes to make before the demo. Questions are grouped under the same
subsystem headings as the study docs, so this doubles as a companion checklist — work a section here
alongside its `0X` doc. Each item is a question with an **Answer** worked out against the actual code.

> Status: every question has a written answer (grounded in the code, not the quotes); the checkboxes are
> left unticked so I can mark each one as I can say it cold. Answers flag where something is *not built
> yet* (⚠️) or where the source quote was imprecise, so nothing here bluffs a mechanism. The goal is that
> no interview thread lands on "I'm not sure — that part was generated."

---

## Pre-demo action items

Concrete changes / prep to do before demoing, distinct from the study questions below.

- [ ] **Reimplement the UI with MudBlazor.**
  - How does this affect our `Dialog`/`Modal` primitive?
  - Can we reduce JS interop by leaning on MudBlazor (or other native Blazor abilities) instead?
- [ ] **Configure port forwarding for all URLs between MBP and STUDIO.**
- [ ] **Review indexes**, especially covering indexes — how they let us seek/scan efficiently for
  frequently sorted/filtered columns.
- [ ] **Walk the Keycloak admin UI manually.** Know where things live so I can explain how authz is
  configured *without* Aspire seeding it — the manual realm/client/role setup path.
- [ ] **Remove / deactivate the MAF agent slice before the demo.** Decision made — scrap it entirely
  (decouple and keep dormant if that's cheap, otherwise remove). It's too much surface area to defend in
  depth and risks conflating the competence read on the areas I'm strongest in: auth, backend, architecture.

---

## 01 — Architecture & topology

**Service discovery**
- [ ] How does Aspire's default service discovery work, how does *ours* work, and how does that differ from
  a production setup?

  **Answer.** Three layers — the mechanism, our wiring, and the prod swap.
  - **Aspire's mechanism (dev).** `apphost.cs` builds a resource graph. `.WithReference(catalog)` on a
    consumer injects the referenced resource's endpoints into that process as env vars
    (`services__catalog__https__0 = https://host:port`), and `.WaitFor(...)` just orders startup. Code
    never sees ports — it dials the logical scheme `https+http://catalog`, and the
    `Microsoft.Extensions.ServiceDiscovery` provider resolves that name to a real `host:port` from those
    env vars at request time. `https+http` = "prefer https, else http for this logical name."
  - **Our wiring.** Discovery is opt-in *per host*, not bundled: `Program.cs` calls `AddServiceDiscovery()`
    + `ConfigureHttpClientDefaults(h => h.AddServiceDiscovery())` (Catalog, so its JWKS backchannel resolves
    `https+http://keycloak`); the gateway adds `AddServiceDiscoveryDestinationResolver()` so YARP cluster
    addresses (`https+http://catalog`) resolve the same way. Nothing hard-codes a port anywhere.
  - **Production.** The `https+http://name` abstraction stays; you swap only the resolver's *source* — from
    Aspire's injected env vars to a real registry: Kubernetes DNS (`catalog.ns.svc.cluster.local`) or
    Consul, fed into a config-driven YARP route table. Because no ports are hard-coded today, that's a
    config/platform change, not a code rewrite — which is the whole point of using discovery addresses now
    (BEYOND-THE-DEMO #5).
  ```csharp
  // apphost.cs — the reference graph is what injects logical addresses (no ports anywhere)
  var catalog = builder.AddProject<Projects.Atrium_Services_Catalog>("catalog")
      .WithReference(catalogDb).WithReference(keycloak);
  builder.AddProject<Projects.Atrium_Gateway>("gateway").WithReference(catalog);
  ```
  ```csharp
  // consumer side — opt into discovery, then dial the logical name
  builder.Services.AddServiceDiscovery();
  builder.Services.ConfigureHttpClientDefaults(h => h.AddServiceDiscovery());
  // HttpClient BaseAddress = "https+http://catalog"  ← resolved at request time
  ```
  - *If they push further:* what if two `catalog` instances exist? Discovery resolves to a **set** of
    endpoints and the HttpClient load-balances across them — in K8s that's the Service's endpoints, in
    Aspire dev it's the single launched process. Consumer code is byte-identical either way; that's the
    entire payoff of dialing a logical name instead of a URL.

**ServiceDefaults scope**
- [ ] Is `Atrium.ServiceDefaults` *truly* telemetry-only? The `DatabaseInitializer` lives in there too.
- [ ] Where are discovery/health "hand-wired per host," and what does the alternative (bundling them, like
  stock Aspire) look like? Why did I deliberately *not* bundle them?

  **Answer.** "Telemetry-only" was the *original* framing; **ADR-0012 broadened it** to a shared
  **deployment-infrastructure** library. The rule is *deployment infra, never domain code* — so it holds
  four things and deliberately excludes two.
  - **In the box:** `AddAtriumTelemetry` (Serilog + OTel), `AddAtriumJwtAuth` (Keycloak JWT + `admin`
    policy), `MapAtriumApiDocs` (the Redoc page), and `DatabaseInitializer` (the DbUp runner). Note the DB
    piece is *initialization* (schema/sproc deploy), not data *access* — repos and sprocs stay in each
    service. So yes, `DatabaseInitializer` living there is consistent with the (revised) charter, not a leak.
  - **Deliberately out — hand-wired per host:** service discovery and health checks. You can see them in
    `Catalog/Program.cs`: `AddServiceDiscovery()`, `ConfigureHttpClientDefaults(h => h.AddServiceDiscovery())`,
    `AddHealthChecks()`, `MapHealthChecks("/health")`; plus `.WithHttpHealthCheck("/health")` in `apphost.cs`.
  - **Why not bundle (unlike stock Aspire's `AddServiceDefaults()`):** each host needs a *different* subset,
    so a god-method would wire things a host shouldn't have. The gateway owns no DB (no SqlClient
    instrumentation, no DbUp); the Portal is an OIDC **cookie** client, not a JWT-bearer service (no
    `AddAtriumJwtAuth`); only the two data services instrument SqlClient and run DbUp. Keeping
    discovery/health explicit makes each `Program.cs` declare its own dependencies. Trade-off: a few
    repeated lines per host in exchange for no leaky one-size wiring.
  ```csharp
  // Catalog/Program.cs — shared infra is ONE call each; discovery/health are explicit per host
  builder.AddAtriumTelemetry(instrumentSqlClient: true);   // ServiceDefaults
  builder.AddAtriumJwtAuth();                              // ServiceDefaults
  builder.Services.AddServiceDiscovery();                  // per-host, deliberately NOT bundled
  builder.Services.AddHealthChecks();                      // per-host, deliberately NOT bundled
  DatabaseInitializer.Initialize(conn, typeof(Program).Assembly, app.Logger); // ServiceDefaults
  ```
  - *Rejected alt:* stock Aspire's `AddServiceDefaults()` bundles telemetry + discovery + health +
    resilience into one call. Convenient, but it wires things a given host shouldn't have and hides
    per-host intent.
  - *If they push further:* the honest counter-argument is DRY — four `Program.cs` files repeat the
    discovery/health lines. I accept it: the repetition is ~3 legible lines, versus a god-method that's
    terse but lies about what each host actually needs.

**End-to-end tracing**
- [ ] How is end-to-end distributed tracing configured, and what would the alternative (per-service /
  per-module) look like?

  **Answer.** One shared call does it: `AddAtriumTelemetry()` (in `TelemetryExtensions`) registers
  OpenTelemetry with ASP.NET Core + HttpClient instrumentation — and SqlClient for the two data services
  via `instrumentSqlClient: true`. It calls `UseOtlpExporter()` **only if** `OTEL_EXPORTER_OTLP_ENDPOINT`
  is set; Aspire injects that env var into every launched resource, so traces flow to the dashboard with
  zero per-host config, and in tests/standalone the exporter is skipped to avoid connection noise.
  - **Why it spans end-to-end:** every hop uses the same instrumentation, and W3C trace-context propagates
    automatically over the HttpClient calls, so the child spans stitch into one trace: Portal → Gateway →
    service → SQL. "Free" because each host just makes the one call.
  - **The alternative (per-service/per-module):** wire the OTel pipeline in each `Program.cs`. You'd get
    drift in sampler/exporter/instrumentation and inconsistent resource attributes, and it's easy to miss
    context propagation — leaving you with disconnected per-service traces instead of one correlated tree.
    The shared extension is precisely what guarantees uniform config and a single joined trace.
  ```csharp
  // TelemetryExtensions — instrumentation is uniform; export is guarded so tests stay quiet
  otel.WithTracing(t => { t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                          if (instrumentSqlClient) t.AddSqlClientInstrumentation(); });
  if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
      otel.UseOtlpExporter();   // Aspire injects the endpoint; absent in tests → exporter skipped
  ```
  - *If they push further:* the spans correlate because the W3C `traceparent` header propagates on the
    outbound HttpClient calls automatically — I don't thread a correlation id by hand. In production you'd
    point `UseOtlpExporter()` at a real collector (Tempo/Jaeger/a vendor) instead of the Aspire dashboard —
    same code, different `OTEL_EXPORTER_OTLP_ENDPOINT`.

**Ingress model — north-south vs east-west**
- [ ] Why does the relay call go direct (service-to-service) instead of back through the gateway? What's the
  alternative and what would it net us?

  **Answer.** The gateway is **north-south ingress only** — the single public entry for the browser/Portal.
  Internal (**east-west**) composition, Storefront→Catalog, dials the service *directly* at its discovery
  address (`http.SendAsync` to `catalog/products` in `StorefrontCatalogClient`, base address
  `https+http://catalog`), relaying the caller's bearer.
  - **The alternative** — bounce the internal call back out through the gateway (`/catalog/...`) — costs an
    extra proxy hop (latency roughly doubles), forces every internal dependency to have a *public* route,
    and turns the gateway into a chokepoint/SPOF for internal traffic. No upside, since discovery already
    hands us a direct address and the bearer relays either way.
  - **What direct nets us:** fewer hops, internal traffic stays off the public surface, and the route table
    stays about *ingress*, not internal wiring — internal topology can change without touching the external
    route table.
  ```csharp
  // StorefrontCatalogClient — east-west call dials the service directly, NOT back through the gateway
  using var request = new HttpRequestMessage(HttpMethod.Get, "catalog/products"); // base "https+http://catalog"
  request.Headers.Authorization = AuthenticationHeaderValue.Parse(incoming);       // relay the user's bearer
  ```
  - *If they push further:* the counter-case *for* routing internal calls through a proxy is a single
    choke-point for uniform policy / rate-limiting / mTLS termination. Valid at scale — but that's what a
    **service mesh** gives you east-west *without* turning the north-south ingress into an internal SPOF,
    which is why the mesh (next question) is the right tool there, not the public gateway.
- [ ] How and why would I close the gateway↔service segment with mTLS or a service mesh — and why choose one
  over the other?

  **Answer.** Today that segment is **trusted transport**: services validate the JWT (proving the *user*),
  but nothing authenticates the *calling workload* — anything that can reach a service's address is
  accepted. Closing it means mutually authenticating the two ends:
  - **mTLS directly:** each service carries a workload cert; peers verify each other's cert on the TLS
    handshake. Fewest moving parts, but you own cert issuance/rotation/trust-store.
  - **Service mesh (Istio/Linkerd):** sidecars do mTLS + workload identity (SPIFFE) transparently, and
    throw in policy, retries, and telemetry. Cert rotation/enforcement handled for you — at the cost of
    real infra to run.
  - **Choosing:** mTLS-alone when you just need the segment authenticated and have few services; a mesh
    when you also want uniform policy/observability/traffic-management across many services and don't want
    to hand-roll it. Crucial framing: you do **not** move user-auth to the edge — that stays per-service
    (defense in depth). mTLS/mesh adds *workload* auth *underneath* the existing JWT check.
  - *Rejected alt:* **move auth to the gateway edge** (terminate identity there, trust everything behind
    it). Rejected — it collapses defense-in-depth to one boundary; a foothold behind the gateway then talks
    to any service unauthenticated. Keep per-service JWT *and* add workload auth beneath it.
  - *If they push further:* who issues the workload certs? A mesh ships its own CA (SPIFFE/SPIRE
    identities, auto-rotated); mTLS-alone means you run issuance/rotation yourself (cert-manager, cloud
    PKI). That operational cost is usually the deciding factor once you're past a handful of services.

**Contracts distribution**
- [ ] Explain the second half of this: how versioned-NuGet contracts let a producer ship without lockstep
  consumer rebuilds.

  **Answer.** Today `Atrium.Contracts` is a **shared project** every producer and consumer references in one
  solution — so a breaking DTO change fails *both* sides' build in the same commit. That lockstep is fine,
  even desirable, in a monorepo.
  - **The "no lockstep rebuilds" part** kicks in once teams split into polyrepos, where a shared *project*
    can't be referenced across repos. You publish `Atrium.Contracts` (or a per-domain slice) as a **SemVer
    NuGet package**; consumers **pin** a version. Now a producer can ship a new backend and cut a release on
    its own cadence, and each consumer keeps building against its pinned contract version, upgrading
    *deliberately* when it chooses — nobody is blocked on everyone rebuilding at once.
  - The ADR-0006 guardrail (contracts stay **DTO-only**, no behavior) keeps that package small and stable,
    so version churn — and the coordination it forces — stays rare. (BEYOND-THE-DEMO #3.)
  ```csharp
  // Atrium.Contracts — DTO-only records; a breaking change here fails BOTH sides' build today (monorepo)
  public sealed record ProductDto(int Id, string Name, string Category, decimal Price);
  ```
  - *If they push further:* once you're on NuGet you *lose* that free compile-time break — so how do you
    catch a breaking change? Public-API / SemVer analyzers in the producer's CI (e.g.
    `Microsoft.CodeAnalysis.PublicApiAnalyzers`) force a **major** version bump on a breaking edit, instead
    of silently shipping and breaking pinned consumers at runtime.

**Evolution — extraction & resilience**
- [ ] How would fault isolation work end to end — specifically the timeout / retry / circuit-breaker
  handlers on the typed clients?

  **Answer.** ⚠️ *Not built yet* — grep finds no resilience handlers, and ADR-0005 explicitly calls Polly a
  production concern. So this is a "how I'd do it," and say that plainly rather than implying it exists.
  - **The wiring:** add `Microsoft.Extensions.Http.Resilience`'s `AddStandardResilienceHandler()` to each
    typed client (the module→gateway clients and Storefront's `StorefrontCatalogClient`). That one handler
    stacks a total-request timeout, a per-attempt timeout, retry with jittered backoff on transient
    failures, and a circuit breaker that trips open when a downstream's failure rate crosses a threshold —
    so calls fail *fast* instead of piling up against a sick dependency.
  - **The isolation payoff:** combine that with the per-module error boundaries in the Blazor shell, and a
    flaky downstream degrades just *that* module (an error panel) rather than taking down the circuit/shell.
    That's most of the fault-isolation benefit of microservices without the deployment cost — and the
    natural precursor to extracting a hot module to its own deployable.
  - **Caveat:** retries are only safe on idempotent operations — the order write already carries an
    idempotency key, so a retried checkout can't double-book.
  ```csharp
  // How it WOULD be wired (not present today) — per typed client at registration:
  builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://gateway"))
      .AddStandardResilienceHandler();   // total timeout + per-attempt timeout + retry(jitter) + breaker
  ```
  - *If they push further:* retry composes correctly with the existing idempotency key — a retried POST
    replays the *same* key, so the server returns the already-created order rather than a second one (see
    the idempotency question in §04). Without that key, retries would be unsafe on the write path.
- [ ] What would splitting Storefront into real microservices actually involve, step by step?

  **Answer.** Half the extraction is *already done*: Storefront owns its own DB (`storefrontdb`) and its own
  gateway route (`/storefront/{**catch-all}`). The remaining steps:
  1. **Own deployable** — split it to its own repo + pipeline (build → test → publish image), deploying on
     its own cadence.
  2. **Contracts as NuGet** — publish the DTOs it shares under SemVer so it and Catalog no longer rebuild in
     lockstep (Q6 / BEYOND #3).
  3. **Resilient Catalog client** — replace the plain `StorefrontCatalogClient` HTTP call with a resilient
     one (retry / circuit-breaker / timeout), because the network-partition surface is now real (Q7).
  4. **Real service-to-service auth** — today it relays the *user's* bearer; add *workload* identity
     (client-credentials token or mTLS/mesh, Q5) so Catalog authenticates the calling service, not just the
     user.
  5. **Async where eventual consistency is OK** — move the synchronous product fan-out to an outbox +
     events (`ProductPriceChanged`) so Storefront keeps its own read model and survives Catalog being down
     (trades freshness for availability).
  - **The headline:** the composition pattern (ADR-0005) doesn't change — only ownership, packaging, and
    transport do. The `IModule`/SCS seams were drawn to make this a **refactor, not a rewrite**.
  - *If they push further:* what breaks *first* when you actually pull it apart? The synchronous Catalog
    call — an in-process-fast dependency becomes a network-partition surface. That's exactly why steps 3
    (resilient client) and 5 (outbox/events) exist; order them by how much availability you need before the
    split is safe under load.
- [ ] What would adding resilience/observability give us that we don't already have?

  **Answer.** Distinguish what we *have* from what adding *nets*.
  - **Already have (observability):** Serilog structured logs + OTel traces over OTLP to the Aspire
    dashboard (Portal → Gateway → service → SQL), plus ASP.NET Core/HttpClient/runtime metrics.
  - **Resilience nets *reaction*, not just recording.** `Microsoft.Extensions.Http.Resilience` on the typed
    clients makes the system *respond* to downstream failure — retry transient blips, fail fast via circuit
    breaker, bound latency via timeouts — instead of merely *recording* the hang. It turns a downstream
    outage from a cascading pile-up into a contained, fast failure.
  - **SLO-based alerting nets *proactivity*.** The metrics/traces already exist but nobody's watching them;
    alerting pages a human when error-rate/latency SLOs burn.
  - **One-liner:** today's stack tells you *what happened* after the fact; adding these makes the system
    *survive* the failure and *notify* you before a user files the ticket.
  - *If they push further:* the order I'd actually do it — **metrics/SLOs first** (can't alert on what you
    don't measure, and the OTel metrics already flow), **resilience second** (it changes failure behavior,
    so you want the observability in place to *see* its effect), then tune circuit-breaker thresholds
    against the real latency histograms rather than guessing.

---

## 02 — Auth & security

**The authenticated read, hop by hop**
- [ ] Know the exact JWT setup and be able to walk an authenticated read end to end.

  **Answer.** Six hops. Setup first, then the walk.

  **Login (OIDC code + PKCE).** The Portal is a confidential client (`atrium-portal`). On callback,
  `OnTokenValidated` parks the raw access token as a custom claim; the principal is serialized into the
  auth cookie so it rides into the circuit (there's no `HttpContext` later — see the asymmetry question):
  ```csharp
  // Atrium.Portal/Program.cs
  options.ResponseType = OpenIdConnectResponseType.Code;
  options.UsePkce = true;
  options.SaveTokens = true;                 // properties copy — logout needs it (id_token_hint)
  options.Events.OnTokenValidated = ctx =>
  {
      var at = ctx.TokenEndpointResponse?.AccessToken;
      if (!string.IsNullOrEmpty(at) && ctx.Principal?.Identity is ClaimsIdentity id)
          id.AddClaim(new Claim("access_token", at));   // claim copy — the circuit needs it
      return Task.CompletedTask;
  };
  ```

  1. **Browser → Portal** is a **cookie** session (not a bearer). The circuit is server-side.
  2. **Lift token into the circuit.** `MainLayout` runs *in* the circuit and reads the cascading auth
     state (only a component can), copying the claim into a scoped `AccessTokenHolder`:
     ```csharp
     // MainLayout.razor
     Tokens.AccessToken = (await AuthState).User.FindFirst("access_token")?.Value;
     ```
  3. **Typed client attaches the bearer** through the one shared pipeline and calls the gateway
     (`https+http://gateway`):
     ```csharp
     // TypedClientSendExtensions.SendForJsonAsync
     request.Authorize(tokens);          // Authorization: Bearer {tokens.AccessToken}
     var response = await http.SendAsync(request, ct);
     response.ThrowIfSessionExpired();   // 401 → typed signal, BEFORE EnsureSuccessStatusCode()
     ```
  4. **Gateway (YARP)** matches the route and forwards *unchanged*, Authorization header included — a pure
     pass-through that never terminates or re-mints the token:
     ```jsonc
     "catalog": { "ClusterId": "catalog", "Match": { "Path": "/catalog/{**catch-all}" } }
     // cluster destination "https+http://catalog" resolved by service discovery at runtime
     ```
  5. **Service validates the JWT** — Keycloak issuer, `atrium` audience, flat `role` claim — and
     authorizes: `GET /catalog/products` is anonymous; writes require the `admin` policy. An *authenticated*
     read like `GET /storefront/orders` additionally scopes rows to the caller in the sproc `WHERE`.
  6. **Response flows back** the same path. Only the final service interprets the token.

  *If they push further:* issuer/signature are validated against Keycloak's JWKS, fetched over the
  discovery backchannel (`https+http://keycloak`), which is why the services also register service
  discovery. Token life is ~5 min with no refresh, so an idle circuit's next call can 401 (→ session-expiry
  question). The `GET /catalog/products` "read" is actually anonymous — the bearer is still sent, just not
  *required* there; use `/storefront/orders` as the example when they want an *authorized* read.

- [ ] How does the shared `atrium` audience let Catalog accept the relayed token?

  **Answer.** JWT validation requires the token's `aud` to contain what the service is configured to
  accept. Every service sets the *same* audience, and Keycloak stamps that *same* audience on every access
  token via a realm protocol-mapper — so one token minted for the user is valid at *all* services:
  ```csharp
  // AuthExtensions.AddAtriumJwtAuth — both Catalog and Storefront
  options.Audience = "atrium";
  ```
  ```jsonc
  // realm-export.json — atrium-audience mapper, applied to issued access tokens
  "protocolMapper": "oidc-audience-mapper",
  "config": { "included.custom.audience": "atrium", "access.token.claim": "true" }
  ```
  That shared audience is exactly what makes the **bearer relay** (Storefront → Catalog) work: Storefront
  forwards the *user's* token untouched and Catalog accepts it, because the token's `aud` already includes
  `atrium` — no token exchange, no re-mint.
  - *Rejected alt:* **per-service audiences** (`aud: catalog`, `aud: storefront`). More correct isolation
    — a token for one service wouldn't be replayable at another — but it forces a **token-exchange / STS
    round-trip** at each hop. Shared audience is the pragmatic call *within one trust domain*.
  - *If they push further:* the cost of a shared audience is that any service holding the token can replay
    it to any other as the user. In production you'd narrow audiences and add **workload auth** (mTLS /
    client-credentials) underneath, so a compromised service can't impersonate the user elsewhere.

**Why a service can relay the bearer but a Blazor page can't**
- [ ] Explain the `HttpContext` asymmetry in depth.

  **Answer.** The dividing line is *"do I have an ambient `HttpContext` right now?"*
  - **A service does.** Storefront is handling an inbound HTTP request, so an `HttpContext` exists for that
    request's whole lifetime — it just reads the incoming `Authorization` header and forwards it:
    ```csharp
    // StorefrontCatalogClient — the clean relay
    var incoming = httpContext.HttpContext?.Request.Headers.Authorization.ToString();
    if (incoming?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(incoming);
    ```
  - **A Blazor circuit doesn't.** `HttpContext` exists *only* for the initial HTTP request that negotiates
    the SignalR connection. Once the interactive circuit is running, there is no ambient request — so a
    component **cannot** call `HttpContext.GetTokenAsync(...)` at render time. That's why the token has to be
    smuggled in another way: parked as a claim at login, lifted into a scoped `AccessTokenHolder` by
    `MainLayout`, attached by the typed clients.
  - *If they push further:* "why not inject `IHttpContextAccessor` into the circuit?" — because in a
    long-lived circuit it's null/stale: the accessor tracks the request that opened the connection, not
    subsequent renders. That dead-end is the entire reason for the claim → holder dance (ADR-0004).

**Token-in-cookie & the production replacement**
- [ ] What does our token-access-management code look like today, and how does it change under option B /
  Duende?
  - Is the fix here something like Duende on the gateway to handle the cookie/token exchange dance? Are
    these all the same underlying problem?

  **Answer.** **Today:** token parked as an `access_token` claim in `OnTokenValidated` → serialized into
  the auth cookie → lifted into a scoped `AccessTokenHolder` in `MainLayout` → attached as a bearer by the
  typed clients. Three accepted debts fall out of that: a **credential rides in the identity cookie**
  (conflation + bloat), **no refresh** (~5 min → 401 → `SessionExpiredException`), and **stale cookie
  across restarts**.

  **Option B (documented, not built):** capture the tokens into a **server-side, session-keyed store** (or
  an `ITicketStore` backing the cookie) instead of onto the principal; the cookie then carries only a
  **session id**; a scoped service reads the store by that id and exposes the token — *same
  `AccessTokenHolder` shape, so no call site changes.* This gets the credential out of the cookie without a
  heavy dependency.

  **Duende.AccessTokenManagement:** the step *after* B — it adds silent **refresh** (fixing "no refresh"),
  at the cost of a dependency and a token store to operate, in exchange for long-lived sessions.

  Answering the two sub-questions directly:
  - **"Duende on the *gateway*?"** No — not in this topology. The cookie↔token boundary lives at the
    **Portal** (a Blazor Server host *is* the BFF; the server holds the tokens), so token management belongs
    there. The YARP gateway is a dumb pass-through that never terminates the cookie, so it has nothing to
    manage. A Duende **BFF** gateway only enters the picture if you switch to a *separate SPA + BFF*
    topology — a different design than Blazor Server.
  - **"Same underlying problem?"** Yes. Token-in-cookie, no-refresh, and stale-cookie are three *symptoms*
    of one root cause: the token is captured once at login and stashed in the cookie with **no server-side
    lifecycle**. Option B fixes the cookie conflation; Duende adds refresh; together they retire all three.

**Why the token is stored twice**
- [ ] Why does logout need the token in a different place than the circuit? What's the alternative?
- [ ] Look at samples of where logout accesses the token vs where the circuit does, and how they differ.

  **Answer.** Two different consumers need a token in two different places, so the *access* token is stored
  twice (once in `SaveTokens` properties, once as a claim). Not redundancy — different readers, different
  scopes:
  - **Logout reads the `SaveTokens` properties.** `/account/logout` runs **server-side** (it's a normal
    request with an `HttpContext`) and does RP-initiated sign-out over the OIDC scheme. `SaveTokens = true`
    stashed the tokens in the auth ticket's properties; the OIDC handler pulls the **id_token** from there
    to send `id_token_hint`, so Keycloak 18+ **skips the "confirm logout" interstitial**:
    ```csharp
    app.MapGet("/account/logout", () => Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
    ```
  - **The circuit reads the claim.** A component can't touch `AuthenticationProperties` (no `HttpContext`),
    so the *access* token is *also* on the principal as a claim, which the circuit reads via cascading auth
    state (`MainLayout`, shown above) to attach as a bearer.
  - **So:** `SaveTokens` properties → logout's `id_token_hint`; claim → circuit's bearer. The only true
    duplication is the access token specifically.
  - *If they push further:* option B collapses the duplication — a single session-keyed store both logout
    and circuit read by session id, with the cookie down to just that id.

**Roles / claim mapping**
- [ ] Explain the flat-role-claim gotcha in depth.

  **Answer.** Keycloak's realm-role mapper emits a **flat, multivalued `role`** claim (e.g.
  `"role": ["admin"]`) — the short name. But JWT-bearer/OIDC default `MapInboundClaims = true`, which runs
  the **legacy inbound claim map** that renames short claim types to the long `ClaimTypes.*` URIs — so the
  inbound `role` silently becomes `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`.
  Meanwhile we tell the framework the role claim is the *short* `"role"`. Mismatch → `RequireRole("admin")`
  finds nothing under `"role"` → **403 for everyone, admins included.** The fix disables the rename and
  pins both claim types:
  ```csharp
  // AuthExtensions (services) and Portal/Program.cs — identical settings
  options.MapInboundClaims = false;                                   // keep Keycloak's short names
  options.TokenValidationParameters.RoleClaimType = "role";           // RequireRole matches this
  options.TokenValidationParameters.NameClaimType = "preferred_username"; // User.Identity.Name
  ```
  - Set in **three places**: the Portal and *both* services — miss one and that host silently 403s.
  - *If they push further:* the tell is that the failure is **uniform** — admins get 403 too — which points
    at claim *mapping*, not policy logic. Diagnose by dumping `User.Claims` and seeing which URI the role
    value actually landed under. (`NameClaimType` is the same class of bug for `Identity.Name`.)

**Why no factory-registered `DelegatingHandler` for the bearer**
- [ ] Why can't we attach the bearer via a `DelegatingHandler` registered on the typed clients? Review the
  actual code/flow.

  **Answer.** The tempting pattern — a `DelegatingHandler` that injects a scoped token holder and sets the
  header — **fails in Blazor Server** because `IHttpClientFactory` builds the handler chain in its **own DI
  scope**, separate from the Blazor circuit's scope. A handler that injected `AccessTokenHolder` would
  resolve a *different, empty* instance than the one `MainLayout` populated in the circuit scope → no token
  → 401. So the token is attached **explicitly**, from the circuit-scoped holder the client was constructed
  with, inside the shared pipeline:
  ```csharp
  // AccessTokenHolder is injected into the client (circuit scope); attached per request:
  request.Authorize(tokens);   // HttpRequestAuthorizationExtensions → Authorization: Bearer {token}
  ```
  - **The precise rule (ADR-0004 amendment / ADR-0011):** it's not "never a `DelegatingHandler`" — it's
    "no handler **built by `IHttpClientFactory`**," because *that's* what runs in the wrong scope. A handler
    composed **manually inside the circuit scope** is legitimate. (That was the AI slice's sanctioned
    exception — now being removed with that slice, but the principle stands.)
  - *If they push further:* how would you *prove* it's a scope bug and not just a null token? Log the
    holder's `GetHashCode()`/instance id in both the handler and `MainLayout` — two different instances is
    the smoking gun that they're in separate scopes.

**Realm config lifecycle**
- [ ] What does "realm changes need a volume reset" actually imply — do we wipe the DB/volume to apply new
  config?
- [ ] What's the non-workaround version of the stale-cookie problem?

  **Answer (realm lifecycle).** `apphost.cs` does `.AddKeycloak(...).WithDataVolume().WithRealmImport("./realms")`.
  `WithRealmImport` **only creates *missing* resources** on startup — it does **not** reconcile or update
  existing ones. Keycloak's live state lives in its **Docker data volume**, so once the realm is imported,
  editing `realm-export.json` (a new client, a changed mapper, a role) has **no effect** on the running
  realm. To apply a realm change you **wipe Keycloak's data volume** so it re-imports fresh.
  - **Important scoping:** that's *Keycloak's* volume only — `catalogdb`/`storefrontdb` are separate SQL
    volumes and are untouched. So "reset the volume" ≠ "clear the app DB"; you're not losing product/order
    data to change an auth setting.

  **Answer (stale cookie).** Cookies are scoped **per host, not per port**. After an Aspire restart the
  Portal returns, but the browser still holds the old auth cookie — carrying a now-dead access token (and
  possibly signed with **Data Protection keys** that regenerated on restart). Module pages then attach a
  dead bearer (or the cookie fails to decrypt) → 401/500 until re-login. The demo **workaround** is
  `/account/logout` → sign back in. The **non-workaround** version, in order of thoroughness:
  1. **Persist Data Protection keys** across restarts (e.g. keys to a mounted volume) so the cookie stays
     decryptable — fixes the decrypt-failure half.
  2. **Server-side token lifecycle** (option B + refresh) so the cookie isn't a frozen dead credential —
     fixes the dead-token half. Same root cause as the token-in-cookie question.
  - *If they push further:* it's the **same underlying problem** as no-refresh — a captured-once credential
    with no server-side lifecycle. There's no clean *cookie-only* fix; the real answer is moving the token
    server-side.

**Evolution — authz as data**
- [ ] How would module-level authorization-as-data (policy names / capability sets) work vs today's single
  role string?
  **Answer.** Today module gating is a **single role string**: each module declares a `RequiredRole`, and
  the one cross-service policy is `AddPolicy("admin", p => p.RequireRole("admin"))`. Evolve to
  **authorization-as-data**:
  - A module declares the **policy names** or a **capability/permission set** it needs (e.g.
    `["catalog:write", "reports:read"]`) rather than a raw role.
  - Those map to **ASP.NET Core authorization policies** (`AddPolicy(...)`), evaluated at *both* layers the
    role string is evaluated today — nav-gating in the shell **and** the endpoint
    (`RequireAuthorization("catalog:write")`).
  - **Role → permission becomes a lookup**, sourced from the token (a Keycloak client-scope/role mapper) or
    a claims-transformation over a permissions store — so you can grant a capability without minting a new
    role across every host. It's the existing `"admin"` policy shape, generalized.
  - *If they push further:* where does the role→permission map live? Keycloak client roles/groups/scopes
    keep the token the source of truth (services enforce with no DB hit); a separate permissions service is
    the heavier option when permissions get relational/dynamic.

- [ ] Production auth-hardening: server-side token store / `ITicketStore`, refresh via Duende, managed
  identity for cloud service-to-service. → ADR-0004.

  **Answer.** A **sequenced** hardening path, each step additive (ADR-0004):
  1. **Get the credential out of the cookie** — option B: server-side session-keyed token store /
     `ITicketStore`, cookie down to a session id. (Cheapest; fixes the conflation smell.)
  2. **Add refresh** — `Duende.AccessTokenManagement` on top of the store, for silent refresh and
     long-lived sessions. (Heavier; sits *after* B, not instead of it.)
  3. **Real service-to-service auth** — replace the *user-bearer relay* with **workload identity**: managed
     identity in cloud, or client-credentials / mTLS (see the mTLS/mesh question), so services authenticate
     *as themselves*, not just as the relayed user.
  - *If they push further:* ordering matters for 1→2 (B is the prerequisite cookie fix); step 3 is
    **orthogonal** and can land independently of the token-store work.

---

## 03 — Modules, portal shell & design system

**Compile-enforced boundaries**
- [ ] How is cross-module coupling prevented by references rather than discipline?

  **Answer.** The dependency graph is a shallow star, enforced by **project references**, not by review
  discipline. Each module RCL references *only* the three shared projects — never another
  `Atrium.Modules.*`:
  ```xml
  <!-- Atrium.Modules.Storefront.csproj -->
  <ProjectReference Include="..\Atrium.Abstractions\Atrium.Abstractions.csproj" />
  <ProjectReference Include="..\Atrium.Design\Atrium.Design.csproj" />
  <ProjectReference Include="..\Atrium.Contracts\Atrium.Contracts.csproj" />
  ```
  To call into another module you'd need a reference to its assembly — and there isn't one, so it won't
  compile. The **host** references the module projects but names none of them (it discovers `IModule` by
  reflection), so even the host doesn't create a coupling seam. "Nowhere to hide" is literal: writing the
  coupling requires adding a `ProjectReference`, which is a visible, reviewable act that also breaks the SCS
  grain.
  - *Rejected alt:* enforce the boundary by **convention / an architecture-test / a linter**. Weaker — it
    relies on catching the violation *after* it's written. The reference graph makes it un-writable.
  - *If they push further:* "couldn't a module reach another's service through DI?" No — it would need the
    service's **type**, which lives in the other module's assembly it can't reference. And `Atrium.Contracts`
    is DTO-only (no behavior), so the one shared surface can't smuggle cross-module logic either. Sanctioned
    sharing is exactly three things: `Abstractions` (the `IModule`/`NavItem` contracts), `Design`
    (primitives), `Contracts` (wire DTOs).
- [ ] How do we ensure a new module can't add a duplicate `BasePath`?

  **Answer.** ⚠️ **Honest answer: nothing enforces it today.** `BasePath` is a *declared convention* on
  `IModule`; actual routing is by the `@page` attributes inside each module. `ModuleLoader.Discover()` just
  collects modules + assemblies — no dedup, no collision check:
  ```csharp
  // ModuleLoader.Discover() — gathers, does NOT validate uniqueness
  modules.AddRange(found);
  assemblies.Add(assembly);
  ```
  Two modules sharing a `BasePath` (or overlapping `@page` routes) wouldn't fail the build — they'd throw
  an `AmbiguousMatchException` **at request time** for the overlapping route. So "routes don't collide" is
  a discipline claim, not a guarantee.
  - **How I'd make it a guarantee:** a fail-fast check at startup (in `Discover()` or the `ModuleCatalog`
    ctor) so a collision is a boot error, not a 500:
    ```csharp
    var dupes = modules.GroupBy(m => m.BasePath, StringComparer.OrdinalIgnoreCase)
                       .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
    if (dupes.Count > 0)
        throw new InvalidOperationException($"Duplicate module BasePath(s): {string.Join(", ", dupes)}");
    ```
  - *If they push further:* `BasePath` is really *advisory metadata* (nav + module identity); the true
    collision surface is the `@page` route templates. A rigorous guard would scan each routable component's
    `RouteAttribute` and assert every route falls **under** its module's declared `BasePath` — so a module
    can't squat another's prefix even with a stray `@page "/admin/...".`

**Assemblies-in-two-places routing**
- [ ] What does this mean, and is there a way to register once and have it propagate to both places?

  **Answer.** A module's routable pages live in its RCL assembly, and Blazor has **two routers** that must
  each know about that assembly — because they serve two different moments:
  1. **Interactive client-side router** — `<Router AdditionalAssemblies=...>` in `Routes.razor`. Handles
     in-app navigation once the circuit is live.
  2. **Server-side endpoint routing** — `MapRazorComponents<App>().AddAdditionalAssemblies(...)` in
     `Program.cs`. Handles the initial SSR/prerender and, crucially, **deep-links / hard refresh** — a GET
     straight to `/storefront/cart`.

  Miss #2 and in-app links work (router #1 handles them) but a refresh/deep-link **404s**, because the
  server endpoint router doesn't know the page exists.

  **"Register once and propagate?"** It already *is* single-sourced — both read from the same
  `ModuleCatalog.Assemblies` (a singleton built by `Discover()`), so you never hand-list assemblies twice:
  ```razor
  @* Routes.razor *@
  <Router AppAssembly="typeof(Program).Assembly" AdditionalAssemblies="Catalog.Assemblies">
  ```
  ```csharp
  // Program.cs
  app.MapRazorComponents<App>().AddInteractiveServerRenderMode()
     .AddAdditionalAssemblies([.. moduleCatalog.Assemblies]);
  ```
  So the "two places" are two *framework wiring points* (unavoidable — two routers), but the **source of
  truth is one**. You can't collapse the call sites (different subsystems), but feeding both from
  `ModuleCatalog` removes the drift risk that the gotcha warns about.
  - *If they push further:* why two routers at all? Blazor Server does an initial **server render** over
    endpoint routing, then hands off to the **interactive circuit** router. They're genuinely separate
    pipelines — a deep-link/refresh hits the endpoint one; in-app nav hits the circuit one — which is
    exactly why a page can resolve in-app yet 404 on refresh if only one is registered.

**Design system — BEM primitives**
- [ ] What are BEM wrappers?

  **Answer.** **BEM** = **B**lock **__E**lement **--M**odifier, a CSS naming convention. A "primitive" is a
  shared class/component in `Atrium.Design` styled with BEM and driven by design **tokens**, so it renders
  identically in every module:
  - **Block** — `.btn` (the base: sizing, radius, font — all from tokens).
  - **Modifier** — `.btn--primary`, `.btn--accent`, `.btn--sm` (variants tweaking only specific props).
  - **Element** — `.dialog__panel`, `.dialog__header` (named parts of a block).
  ```css
  /* atrium.css — the "thin wrapper": base + modifiers, values from tokens (custom properties) */
  .btn        { height: 36px; padding: 0 var(--space-4); border-radius: var(--r-md);
                font: 500 var(--text-sm)/1 var(--font-sans); }
  .btn--accent{ background: var(--accent); color: var(--on-accent); }
  .btn--sm    { height: 30px; font-size: var(--text-xs); }
  ```
  "Thin wrapper" = the primitive is just base-class + modifiers pulling from tokens, so `btn--accent` is the
  same everywhere and no module hand-rolls button CSS. (The **atrium-ui** skill exists to enforce exactly
  this: use tokens/primitives, never ad-hoc styling.)
  - *Rejected alt:* per-module component styles, or pulling in a UI library. The first duplicates and drifts;
    the second is a heavy dependency. BEM + tokens is one cheap source of truth. *(Tension worth naming: the
    pre-demo action item weighs adopting MudBlazor — which would trade this hand-owned primitive set for a
    library's components. That's a real fork, not a free win.)*
  - *If they push further:* BEM's payoff is **flat specificity** — every selector is a single class, so
    there are no specificity wars and modifiers compose (`class="btn btn--accent btn--sm"`). Theming is
    pure tokens: `:root[data-theme="dark"]` swaps the custom-property *values*, so the same BEM classes flip
    light/dark with **no new selectors**.

**JS interop & cart**
- [ ] Understand how the JS partial hydration works within Storefront / Cart / CartPersistence.
- [ ] Enumerate *all* instances of JS interop in the app and how each works.
- [ ] Explain the circuit-scoped cart + `CartPersistence` in depth (with code snippets).

  **Answer (a) — "partial hydration" in Blazor Server.** This is a render-lifecycle thing, not WASM
  hydration. A component **prerenders on the server** (static SSR — no circuit, no JS runtime yet), then
  becomes interactive once the SignalR circuit connects. The load-bearing rule (ADR-0010): **JS interop must
  not run during prerender** — there's no JS runtime, so calling it throws. So cart work is deferred to the
  first *interactive* render and every call is guarded:
  ```csharp
  // CartPersistence — hydrate only once the circuit is live; degrade, never throw
  catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException)
  { /* JS unavailable (prerender/disconnect) → empty in-memory cart */ }
  ```
  `HydrateAsync()` is called from `OnAfterRenderAsync(firstRender)` (interactive), never `OnInitialized`
  (which runs during prerender).

  **Answer (b) — every JS interop surface (there are five, all guarded ES-module imports).**

  | # | Module (`import`) | Caller | What it does |
  |---|---|---|---|
  | 1 | `Design/js/dialog.js` | `Dialog.razor` | `showModal(el)` / `close(el)` on native `<dialog>` — platform gives focus-trap, Esc, backdrop, top-layer (ADR-0010) |
  | 2 | `Design/js/theme.js` | `ThemeToggle.razor` | `get()` / `set(theme)` on `documentElement.dataset.theme` + localStorage; inline host script sets initial theme (no flash) |
  | 3 | `Storefront/js/cart-storage.js` | `CartPersistence.cs` | `load()` / `save(json)` / `clear()` — localStorage, min `{id,qty}` snapshot |
  | 4 | `Portal/…/ReconnectModal.razor.js` | `ReconnectModal` | colocated reconnect-UI script |
  | 5 | `Design/js/agentchat.js` | `AgentChat` | AG-UI chat — *being removed with the agent slice* |

  Shared pattern: an `IJSObjectReference` from a dynamic `import`, cached single-flight, disposed in
  `DisposeAsync`, every call wrapped for `JSDisconnectedException`. *If they push further:* why so few, and
  why ES modules over `<script>` globals? The design leans on the **native platform** (`<dialog>`,
  `data-theme` CSS, localStorage) instead of JS widgets, so interop stays a thin edge; ES-module imports are
  scoped (no global namespace), lazy-loaded per component, colocated, and disposable.

  **Answer (c) — circuit-scoped cart + `CartPersistence` in depth.** `CartService` is `AddScoped`, and in
  Blazor Server **scoped == per circuit** (per SignalR connection). So the cart is in-memory, tied to one
  circuit:
  ```csharp
  services.AddScoped<CartService>();       // StorefrontModule.RegisterServices — one cart per circuit
  services.AddScoped<CartPersistence>();
  ```
  - **In-app `NavLink`** navigation stays in the same circuit → cart preserved.
  - **Full-page navigation** (browser goto, or the OIDC login redirect round-trip) tears down the circuit →
    fresh empty `CartService` → cart lost. That's the gap `CartPersistence` closes via localStorage:
    - **Save** on every `CartService.Changed`: serialize a minimal `{productId, quantity}` snapshot, chained
      FIFO (`_saveChain`) so an older write can't overwrite a newer one.
    - **Hydrate** once per circuit on first interactive render: load the snapshot, **re-fetch products from
      the catalog** (re-price — snapshots carry only id+qty, never stale prices), and `MergeRestored` into
      the live cart (merge, not replace, so items added while hydration was in flight survive).
  - *If they push further:* **why re-price instead of storing prices?** Stored prices could show a stale
    total if the catalog changed between sessions; re-pricing from the live catalog is the source of truth
    (same principle as the order-replay "faithful response" in §04). **Why merge, not replace?** A race —
    the user adds an item while `HydrateAsync` awaits the catalog fetch; replace would wipe it, merge sums
    the quantities. **Why not a server-side cart?** That's the production path (DB/distributed cache keyed by
    session), but it adds a store to operate and doesn't help the *anonymous, pre-login* cart, which is
    exactly what the localStorage bridge covers cheaply.

**Evolution — module failure isolation**
- [ ] How would handling a module failing actually work?

  **Answer.** Today modules are in-process RCLs discovered by reflection and loaded into the **default
  `AssemblyLoadContext`**; they share one process and rise/fall as one deploy. The failure surface and the
  isolation ladder:
  - **Startup failure (cheapest fix).** A module that throws in `RegisterServices` currently takes down host
    boot. `Discover()` is the seam — wrap each module so one bad module is **skipped and logged** (its nav
    hidden) instead of crashing the shell:
    ```csharp
    // ModuleLoader.Discover — the isolation seam (try/catch per module, not present today)
    try { module.RegisterServices(services, config); }
    catch (Exception ex) { logger.LogError(ex, "Module {Name} failed to register; skipping", module.Name); }
    ```
  - **UI failure (partly here already).** Per-module error boundaries in the shell (the same
    `ErrorBoundary` mechanism as `SessionErrorBoundary`) degrade *one module's page* to an error card
    rather than tearing down the circuit.
  - **Runtime isolation (heaviest).** Load each module into its **own `AssemblyLoadContext`** (or
    out-of-process) so it can fail/unload independently — the folder-drop / plugin path in BEYOND-THE-DEMO
    #6. More machinery (ALC versioning, isolation) for genuine independence.
  - *If they push further:* the honest limit — even with error boundaries, in-process modules share memory
    and the thread pool, so a module can still starve the host. **True** fault isolation needs a process/ALC
    boundary, and *that's* the point where you're really paying microservices' cost. The `IModule` seam
    keeps every rung above a **packaging/hosting** change, not a module rewrite — which is why the boundary
    was drawn there.

---

## 04 — Backend services & data

**Cancellation tokens**
- [ ] How do cancellation tokens work within the services, and in an API generally?

  **Answer.** A `CancellationToken` is a cooperative signal that the caller went away — client
  disconnected, timeout fired, host shutting down. ASP.NET Core mints one per request (bound to
  `HttpContext.RequestAborted`) and minimal APIs inject it just by declaring the parameter. The discipline
  is to **thread it all the way down** so abandoned work actually stops. Here it flows endpoint → repo →
  Dapper → driver:
  ```csharp
  // OrdersEndpoints.CreateOrder — injected per request
  var products = await catalog.GetProductsAsync(ct);              // outbound HTTP honors it
  orderId     = await repository.CreateAsync(userName, key, lines!, ct);
  ```
  ```csharp
  // OrderRepository — handed to the driver via Dapper's CommandDefinition
  await db.OpenAsync(ct);
  await db.QuerySingleAsync<OrderCreateResult>(new CommandDefinition(
      "dbo.usp_Order_Create", args, tx, commandType: CommandType.StoredProcedure, cancellationToken: ct));
  ```
  So a disconnect propagates: SqlClient aborts the command, HttpClient aborts the outbound call, and a
  cancelled write mid-transaction rolls back (the `await using` transaction disposes without commit).
  - *Anti-pattern:* passing `CancellationToken.None` / ignoring it — the request keeps burning a pooled DB
    connection and CPU after the caller's gone; under load that's a pool-exhaustion vector.
  - *If they push further:* two subtleties — (1) do **not** pass the request token to fire-and-forget work
    that must outlive the response (an audit write) — it'd be cancelled when the response completes; (2)
    cancellation is **cooperative** — it's observed at async yield points (SqlClient cancels the TDS
    request), so a tight synchronous loop won't notice it.

**Idempotency**
- [ ] What generates the idempotency key used in `OrdersEndpoints.cs`?

  **Answer.** The **client** — specifically `Checkout.razor`. A fresh `Guid.NewGuid()` is minted when the
  checkout page initializes, held in `_orderKey`, **reused across retries of the same cart**, and **rotated
  only after a successful placement**:
  ```csharp
  // Checkout.razor
  private Guid _orderKey = Guid.NewGuid();   // one key per checkout attempt (survives retries)
  // ...on success:
  Cart.Clear();
  _orderKey = Guid.NewGuid();                // the NEXT order gets a fresh key
  ```
  The server *requires* it (rejects `Guid.Empty`) but never generates it, and it travels in
  `CreateOrderRequest(Items, IdempotencyKey)`.
  - *If they push further:* why client-side, not server-side? A server-minted key can't dedupe the
    client's retry — the client wouldn't know to resend the same value, so the retry reads as a new order.
    The key has to be stable **from the client's perspective across its own retries**, which means the
    client owns it. (Standard Stripe-style idempotency-key pattern.)

- [ ] What is "replay" exactly, and how does it drive the faithful-response behavior?

  **Answer.** "Replay" = the same idempotency key arriving again — a retry after an *ambiguous* failure
  (server committed, but the response timed out on the way back). The write path is idempotent in the sproc:
  ```sql
  -- usp_Order_Create: key already committed for THIS user → return original id + IsNew = 0
  DECLARE @ExistingId INT = (SELECT Id FROM dbo.Orders WHERE IdempotencyKey=@Key AND UserName=@User);
  IF @ExistingId IS NOT NULL BEGIN SELECT @ExistingId AS OrderId, CAST(0 AS BIT) AS IsNew; RETURN; END
  ```
  The repo skips re-adding lines when `IsNew = 0`; a unique **filtered index** on `IdempotencyKey` +
  TRY/CATCH is the concurrency backstop (a double-submit race resolves to a replay for the same user, or
  error `50002` → 409 for a *different* user, so no cross-user leak). Then the **faithful response**: the
  endpoint returns the order **read back from storage**, not a reconstruction of the request:
  ```csharp
  var order = await repository.GetByIdAsync(orderId, userName, ct);   // the STORED order, not this request
  ```
  - *If they push further:* why read back instead of echoing what you just priced? Because they can
    diverge — if catalog prices changed between the original submit and the retry, the *committed* order is
    authoritative and re-pricing would show a total that doesn't match what was charged. Read-back = single
    source of truth. (Mirror image of the §03 cart re-price: there's no commit yet on the cart, so we
    re-price; here there **is** one, so we read back.)
- [ ] **POSSIBLE BUG:** does idempotency around orders prevent adding a new item to an existing order?
  - *On second thought this may be correct:* we don't create the order then add items — an order is locked
    to its own items. Confirm this reading against the code.

  **Answer.** ✅ **Confirmed — no bug. Your second reading is right.** Orders are **immutable and atomically
  created**: there is no "add an item to an existing order" operation anywhere. `CreateOrder` is the only
  mutating endpoint, and it writes the header + *all* line items in **one transaction**:
  ```csharp
  // OrderRepository.CreateAsync — header + every line in ONE transaction; no post-hoc item-add path
  await using var transaction = await db.BeginTransactionAsync(ct);
  // usp_Order_Create (header) → usp_OrderItem_Add per line (only when IsNew) → CommitAsync
  ```
  The idempotency key is scoped to **one checkout submission**, so it never blocks a legitimate new order:
  - Retrying the *same* submission (same `_orderKey`) → **replay**, returns the original order, adds nothing.
  - A *new* checkout (cart cleared, `_orderKey` rotated) → a brand-new order with a new key.
  - *If they push further:* if the product model later needed *mutable* orders (add/remove lines after
    placement), that'd be a **distinct operation** with its own concurrency control (optimistic
    concurrency on an order `rowversion`), modeled explicitly — not the create key overloaded. Today "an
    order is locked to its own items" is exactly the intended invariant.

**Authorization boundary in the data layer**
- [ ] Where is the authz boundary for reading an order, and could we enforce it outside the DB (and cache
  it)?
  - In a real DB this is an extra join per query, checked at the DB every time. Keeping it outside the DB
    and cached could be cheaper and/or act as a second security boundary — what would that look like?

  **Answer.** There are **two** boundaries today, deliberately layered:
  - **HTTP layer** — `/storefront` group's `.RequireAuthorization()` blocks anonymous callers before the
    handler runs.
  - **Data layer** — every user-scoped sproc filters on `@UserName`. `usp_Order_GetById` filters on *both*
    id and owner, so a wrong owner gets **zero rows — indistinguishable from "not found"** (no existence
    leak). It lives where an app-layer mistake can't bypass it, and it's proven by tests
    (`GetById_returns_null_when_the_order_belongs_to_another_user`):
    ```sql
    -- usp_Order_GetById — the ownership filter IS the security boundary
    WHERE o.Id = @OrderId AND o.UserName = @UserName;
    ```
  **Caching it outside the DB** (the sub-question): keep the DB filter *and* add an app-layer gate in front
  of it — resource-based authorization (`AuthorizeAsync(user, orderId, "OrderOwner")`) backed by a cache
  (e.g. a distributed `orderId → ownerId` map) that rejects a foreign read **before** the DB round-trip.
  Ownership rarely changes, so it caches well.
  - *Trade-offs:* a cache is an **invalidation + staleness** problem, and the rule now lives in two places
    that must stay consistent. So the sproc stays **authoritative**; the cache is a first-gate optimization
    and a second boundary, never a *replacement*.
  - *If they push further:* the reason it's in the sproc *today* is that the `WHERE` clause **cannot be
    forgotten** — a future endpoint that forgets an app-layer check would silently open a hole, but it
    can't forget the predicate baked into the read. Prod ordering: DB filter always; cache as an
    optimization on top; never cache *instead of*.

**Repository interfaces / DIP**
- [ ] What is a "DIP seam"?

  **Answer.** **DIP** = Dependency Inversion Principle: the handler depends on an **abstraction**
  (`IOrderRepository`), not on the concretion (Dapper / `OrderRepository`). The **"seam"** is that
  interface — the swappable joint where you can substitute an implementation without touching call sites.
  Here it's **co-located** right above its single implementation (the standalone interface *file* was pure
  ceremony and was removed — ADR-0007):
  ```csharp
  public interface IOrderRepository { Task<int> CreateAsync(...); Task<OrderDto?> GetByIdAsync(...); }
  public sealed class OrderRepository(SqlConnection db, ILogger<OrderRepository> logger) : IOrderRepository { ... }
  ```
  The load-bearing nuance (ADR-0007): the seam is kept **not for mockability**. Mocking a repo tests the
  mock, not the SQL — so the repos are **integration-tested against real SQL** (Testcontainers), and the
  logic worth isolating (`OrderPricing`, `SalesReportBuilder`) was extracted into pure functions tested with
  no repo at all.
  - *If they push further:* "one implementation — why keep the interface?" Co-locating it removes the only
    real cost (the extra file), and it preserves the decorator/fake option at ~zero cost (next answer).
    Deleting it is defensible on "less abstraction" grounds but breaks the convention most .NET reviewers
    expect for near-zero gain.
- [ ] How does keeping the repository interface keep the decorator/fake door open?

  **Answer.** Because the handler depends on `IOrderRepository`, you can **wrap or replace** the
  implementation in DI without editing the handler:
  - **Decorator** — a `CachingOrderRepository(IOrderRepository inner)` (or logging/retry) that implements
    the same interface and delegates, adding cross-cutting behavior transparently:
    ```csharp
    services.AddScoped<OrderRepository>();                       // the real SQL repo
    services.AddScoped<IOrderRepository>(sp =>                   // wrapped — handlers unchanged
        new CachingOrderRepository(sp.GetRequiredService<OrderRepository>(), sp.GetRequiredService<ICache>()));
    ```
  - **Hand-rolled fake** — if a handler ever grows real branching worth unit-testing without a DB (a 404
    path), implement the interface as a tiny in-memory fake — cheaper and more honest than a mock framework.
  - *If they push further:* why a decorator over just editing the repo? Single-responsibility — the SQL repo
    stays about SQL; caching/logging compose orthogonally and can be re-ordered or toggled in DI. It's the
    same shape as the chat-pipeline decorators the project already used elsewhere.

**Orders table performance**
- [ ] Optimize / review optimizations for the Orders table — the single biggest table; be ready to talk
  through how it's optimized or could be.
  - CQRS? Could separate reads and writes to avoid lock contention.

  **Answer.** **Where it is today (honest, small):** `Orders` + `OrderItems`, indexed for the current access
  paths:
  ```sql
  CREATE INDEX IX_Orders_UserName ON dbo.Orders (UserName);          -- "my orders"
  CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems (OrderId);    -- FK-supporting, for the header×line join
  CREATE UNIQUE INDEX UX_Orders_IdempotencyKey ON dbo.Orders (IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
  ```
  **As it grows (Orders is the biggest, write-heavy table):**
  - **Covering / composite indexes** for the hot shapes — e.g. `IX_Orders_UserName_PlacedAtUtc INCLUDE (Total)`
    so "my recent orders" seeks *and* is covered, no key lookup. (Ties to the pre-demo action item on
    reviewing covering indexes.)
  - **Write contention:** an ever-appending `IDENTITY` PK can hot-spot the last page under high write
    concurrency ("last-page insert contention"); if it ever shows up, `OPTIMIZE_FOR_SEQUENTIAL_KEY` (SQL
    2019+) or partitioning — usually a non-issue at this scale.
  - **CQRS / read-write split:** keep the write model lean and serve heavy reads from a **read model** so
    reporting doesn't contend with order writes. Lightweight: a read replica + route list/report queries
    there. Heavier: a denormalized read store (materialized order/sales summaries) fed by events — Reports
    especially wants its own read-optimized store (BEYOND-THE-DEMO #1, and ties to the outbox item below).
  - *If they push further:* **measure first.** The DIP seam + sprocs mean a caching decorator or a
    read-replica routing is *additive*, not a rewrite — so premature CQRS is cost without payoff at demo
    scale. The senior answer is "here's the sequence and the trigger," not "I'd shard it now."

**Connection lifetime & pooling**
- [ ] Revisit the connection-per-request wording — why is the `SqlConnection` scoped per request rather than
  per some longer-lived unit? The phrasing sounds suspect; understand pooling behind it.

  **Answer.** The wording isn't suspect once you separate two things — the **object** and the **physical
  connection**. `builder.AddSqlServerClient("storefrontdb")` registers `SqlConnection` as **scoped** (scope
  == one HTTP request), and the repository (also scoped) receives it by injection:
  ```csharp
  builder.AddSqlServerClient("storefrontdb");                       // SqlConnection: scoped == per request
  builder.Services.AddScoped<IOrderRepository, OrderRepository>();
  ```
  Per-request is *correct*, not wasteful: `SqlConnection` is **not thread-safe**, so you must not share one
  across concurrent requests. And crucially — **the object is per-request, but the underlying TCP+login
  connection is pooled.** ADO.NET keeps a pool keyed by connection string; `OpenAsync` *rents* a physical
  connection and dispose/close *returns* it. So "new `SqlConnection` per request" is cheap — you're renting,
  not handshaking. Scoped disposal is what returns it to the pool.
  - *"Revisit pooling if chattier":* add Polly around transient `SqlException`s (retry/circuit-breaker),
    cache the read-heavy/rarely-changing product list, and batch if a request opened many short
    connections. None needed at demo scale.
  - *If they push further:* two gotchas — (1) **never** make the connection a singleton (thread-safety, and
    it would pin one pooled connection forever); (2) the pool has a **max size** (default 100) — a leak
    (undisposed connection) or long-held connections exhausts it and new requests block on `OpenAsync`.
    Scoped + `await using` is exactly what keeps the pool healthy. (Note the repo opens explicitly for its
    transaction; Dapper otherwise opens/closes per call.)

**Versioning**
- [ ] How would versioning work for services? For modules it's versioned NuGet packages — what's the backend
  equivalent?

  **Answer.** A module ships as a versioned NuGet package because its consumer is *the host, at build time*.
  A service's consumer is *another process, at run time*, so its contract is the **HTTP surface** — you
  version that, across three independent axes:
  - **API version** (for consumers): `/catalog/v1/products` — URL segment, or an `api-version` header /
    media-type, via `Asp.Versioning.Http`. A breaking shape becomes `/v2`, with `/v1` kept through a
    deprecation window.
  - **Contract package** (the DTOs): `Atrium.Contracts` versions alongside — a shared project today (a
    breaking change fails both builds), SemVer NuGet later (§01 / BEYOND #3) so a producer ships without
    lockstep consumer rebuilds.
  - **DB schema:** DbUp run-once migrations, evolved **expand-contract** (add column → backfill → switch
    reads → drop), never a breaking migration in one step.
  - *If they push further:* the discipline is **backward-compatible-by-default** — additive changes (a new
    optional field, a new endpoint) need no version bump; only breaking changes do, and you run old+new in
    parallel through the deprecation window. That's the same "extract/evolve when it hurts, additively"
    grain as the rest of the system.

**Evolution — outbox & events**
- [ ] What would an outbox + inter-service events actually look like?

  **Answer.** **Today:** Storefront calls Catalog **synchronously** over HTTP on every order (to price) and
  every report (to categorize) — a live fan-out with a partial-failure hop (Catalog down → Storefront
  degrades). **The decoupled shape:**
  - **Transactional outbox:** when Catalog commits an event-worthy change, it writes the domain row **and**
    an `OutboxMessages` row **in the same transaction** — so the event can't be lost or diverge from the
    commit. A relay polls the outbox and publishes to a broker, marking messages sent.
  - **Consumer read model:** Storefront subscribes and keeps its **own local copy** of the product data it
    needs (`id → price/category`), updated by events. Pricing/reporting then read Storefront's *own* table —
    no live call to Catalog.
  ```text
  Catalog:    [commit product change] + [insert OutboxMessage]  (one tx)  → relay → broker → ProductPriceChanged
  Storefront: consume → upsert local product read model → price / report locally (no live Catalog call)
  ```
  - **The trade:** freshness for availability + resilience — Storefront survives Catalog being down
    (eventually consistent), and the partial-failure hop disappears.
  - *If they push further:* why an outbox, not just "publish after commit"? The **dual-write problem** —
    commit succeeds, publish fails (or vice-versa) → DB and event diverge. The outbox makes the event part
    of the same transaction (at-least-once delivery); consumers **dedupe by event id** — the *same
    idempotent-consumer pattern* as the order idempotency key. The SCS boundaries were drawn to make this
    additive (BEYOND-THE-DEMO).

**Testing / DbUp**
- [ ] How is the DbUp machinery used to build/seed the MSSQL Testcontainers for the integration tests?

  **Answer.** The tests reuse the **exact same DbUp runner the services use in production** — that's the
  whole point: exercise the real schema/sprocs, not a hand-maintained test copy.
  - **One container per run.** `SqlServerFixture` (an `IAsyncLifetime` shared via an xUnit collection)
    starts a single throwaway SQL Server in Docker (Testcontainers), amortizing the ~seconds of startup
    once:
    ```csharp
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    ```
  - **A database per test class** on that shared container (`ConnectionStringFor("storefront_test")`), so
    schemas/data don't collide.
  - **Same runner, pointed at the service assembly.** Each class's `InitializeAsync` calls the production
    `DatabaseInitializer.Initialize(...)` with the *service's* assembly, so it runs that service's embedded
    `Data/Scripts` (Migrations then Programmability) against the container — the sprocs/tables under test
    **are** the production ones:
    ```csharp
    StorefrontDb.Initialize(_connectionString, typeof(OrderRepository).Assembly, NullLogger.Instance);
    ```
  Then the tests run the concrete `OrderRepository` against it — real Dapper, real sprocs, real transaction
  (ADR-0007's "test the thing that can actually break"). This is also *why* `DatabaseInitializer` lives in
  `ServiceDefaults` (shared deployment infra): test-init and prod-init are byte-identical.
  - *If they push further:* isolation is by **database-per-class + distinct user names per test** (the
    tests use `alice-create`, `carol-owner`, `erin-key-owner`…) rather than transaction-rollback — so tests
    can exercise **real commits**, including the idempotency unique-index race
    (`Create_rejects_a_replay_of_another_user_s_idempotency_key`). The container is disposed at the end;
    nothing persists.
