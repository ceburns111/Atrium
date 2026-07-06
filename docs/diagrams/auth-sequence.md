# Auth & token propagation (sequence)

How a signed-in user's access token gets from Keycloak into an outbound API call, and how the
downstream service validates it. This is the runtime picture behind
[ADR-0003](../adr/0003-yarp-keycloak-auth.md) (YARP + Keycloak) and
[ADR-0004](../adr/0004-token-propagation-and-option-b.md) (token-in-claim).

Key facts every step reflects:

- The Portal is a confidential OIDC client (`atrium-portal`) using the authorization-code + PKCE flow
  (`src/Atrium.Portal/Program.cs`).
- On `OnTokenValidated`, the raw access token is stashed as a custom `access_token` claim in the
  principal, because a Blazor **circuit** has no `HttpContext` to fetch it from later.
- `MainLayout` copies that claim into the **scoped** `AccessTokenHolder`
  (`Tokens.AccessToken = (await AuthState).User.FindFirst("access_token")?.Value`).
- The module typed clients (`CatalogClient` / `OrdersClient` / `ReportsClient`) call
  `http.SendForJsonAsync(...)`, which calls `request.Authorize(tokens)` attaching `Authorization: Bearer …`
  **only when the holder is non-empty** (`src/Atrium.Design/HttpClientExtensions.cs`). **No
  factory-registered `DelegatingHandler`** — `IHttpClientFactory` builds handler chains in a separate DI
  scope where the circuit-scoped holder is empty. Exception: the AG-UI chat client owns its `HttpClient`
  internally and has no per-request send seam; its `BearerTokenHandler` is composed in circuit scope
  instead ([ADR-0011](../adr/0011-circuit-scoped-bearer-handler.md)).
- The **gateway is a pass-through** — it does no auth of its own; it forwards the request (and its
  bearer) to the target cluster.
- Each service validates the JWT with `AddKeycloakJwtBearer(realm: "atrium", Audience = "atrium")`.

```mermaid
sequenceDiagram
    autonumber
    actor U as Browser
    participant P as Atrium.Portal<br/>(Blazor Server)
    participant KC as Keycloak<br/>(realm: atrium)
    participant H as AccessTokenHolder<br/>(scoped)
    participant C as Typed client<br/>(CatalogClient / OrdersClient)
    participant GW as Atrium.Gateway<br/>(YARP, pass-through)
    participant S as Service<br/>(Catalog / Storefront)

    U->>P: GET a protected page
    P-->>U: 302 → /account/login (Results.Challenge)
    U->>KC: OIDC authorize (code + PKCE)
    KC-->>U: code → redirect back to Portal
    P->>KC: token endpoint (code → tokens)
    KC-->>P: id_token + access_token (aud: atrium)
    Note over P: OnTokenValidated stashes<br/>access_token as a claim in the cookie principal
    P-->>U: auth cookie set
    Note over P,H: MainLayout copies the access_token<br/>claim into AccessTokenHolder
    P->>H: Tokens.AccessToken = claim value
    C->>H: read AccessToken
    C->>GW: GET /catalog/products<br/>Authorization: Bearer …
    GW->>S: forward request (+ Bearer), no gateway auth
    S->>S: validate JWT (issuer=Keycloak, aud=atrium),<br/>authorize by policy (admin for writes)
    S-->>GW: 200 (or 401 → SessionExpiredException)
    GW-->>C: response
```

> Note: the token-in-claim shortcut carries known debt (no refresh, stale-cookie-after-restart). The
> documented exit is a server-side token store — see [ADR-0004](../adr/0004-token-propagation-and-option-b.md).
