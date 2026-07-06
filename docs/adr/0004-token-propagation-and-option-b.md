# ADR-0004 — Getting the access token into the Blazor circuit (token-in-claim, with option B as the exit)

**Status:** Accepted, with known debt · **Deciders:** Atrium build · **Context phase:** 4b

> **Amended 2026-07-03:** the categorical "no `DelegatingHandler`" phrasing below is too strong. The
> real constraint is that a handler *built by `IHttpClientFactory`* cannot see circuit-scoped state.
> The AI chat surface ships `BearerTokenHandler` — a `DelegatingHandler` composed manually *inside*
> the circuit scope, which is legitimate. [ADR-0011](0011-circuit-scoped-bearer-handler.md) records
> the precise rule and the one sanctioned exception.

## Context

The Portal's typed clients (`CatalogClient`, `OrdersClient`, `ReportsClient`) need to attach the user's
access token as a `Bearer` when calling the gateway. In Blazor **Server**, that's harder than it looks:

- A Blazor **circuit** has **no `HttpContext`**. `HttpContext` exists only for the initial HTTP request
  that opens the SignalR connection; once the interactive circuit is running there is no request, so a
  component **cannot** call `HttpContext.GetTokenAsync(...)` to fetch the token.
- The obvious "attach a token in a `DelegatingHandler`" pattern **fails here**: `IHttpClientFactory`
  builds handlers in a **separate DI scope** from the component's scope, so a scoped token holder read
  from inside the handler comes back **empty**. (This one bit us directly.)

So the token has to travel from the login request into the long-lived circuit somehow.

## Decision (current)

Carry the access token **inside the `ClaimsPrincipal`**:

1. In OIDC `OnTokenValidated`, park the raw access token as a **custom claim** on the principal (and
   keep `SaveTokens = true`).
2. The principal is serialized into the **auth cookie**, so it rides into the circuit with the user.
3. `MainLayout` (which *does* run in the circuit) copies that claim into a **scoped**
   `AccessTokenHolder` (`Atrium.Design`).
4. The typed clients read the holder and set `Authorization: Bearer …`. No `DelegatingHandler`.

`SaveTokens = true` is **not** redundant with the claim: it's what lets the OIDC handler send
`id_token_hint` on RP-initiated logout (without it, Keycloak 18+ shows a "confirm logout"
interstitial). The only true duplication is the *access token specifically* being stored twice — once
in the `SaveTokens` properties (needed by logout), once as the claim (needed by the circuit) — because
each consumer needs it in a different place.

## Consequences (the debt we're accepting)

- **A credential travels in the auth cookie.** The access token is a *credential*; putting it in the
  identity **cookie** conflates identity with credentials and bloats the cookie. It works, it's simple,
  and for a demo it's acceptable — but it's an architecture smell we're naming, not hiding.
- **No refresh.** The token is captured once at login; there's no refresh. After it expires (~5 min)
  Catalog returns 401. The clients now translate that 401 into a typed `SessionExpiredException`
  (`Atrium.Design`), which a shell-level `SessionErrorBoundary` around the module body turns into a
  "your session has expired — sign in again" panel, instead of tearing down the circuit with the
  generic unhandled-error UI ([ADR-0008](0008-graceful-session-expiry-handling.md)). This is graceful
  **handling** of expiry, not a fix for it — the real fix is refresh (option B →
  `Duende.AccessTokenManagement`). Workaround remains: sign in again.
- **Stale cookie across restarts.** Cookies are per-host, not per-port, so an old Portal cookie
  carrying a dead token can 500 the storefront after an Aspire restart until you re-login.

## Option B — the preferred replacement (documented, not built)

A small **server-side token store**, keeping the cookie down to a session id:

- In `OnTokenValidated`, capture the tokens into a **session-keyed store** (or an `ITicketStore`
  backing the cookie) instead of onto the principal.
- The cookie then carries only a **session id**, not the credential.
- Surface the current token to the circuit via a **scoped service** that reads the store by session id
  — same `AccessTokenHolder` shape the clients already use, so the call sites don't change.

This removes the token from the cookie **without** pulling in the full framework. The eventual
production path is **`Duende.AccessTokenManagement`**, which also adds silent **refresh** (fixing the
"no refresh" limitation above) — heavier than option B, so it's the step *after* it, not instead of it.

## Alternatives rejected

- **`DelegatingHandler` + scoped token** — fails outright (handler runs in a different scope; see
  Context).
- **Blazor WebAssembly / BFF token handoff** — different hosting model than this project targets.
- **Jump straight to `Duende.AccessTokenManagement`** — the right prod answer, but more than a demo
  needs; option B is the cheaper intermediate that already fixes the cookie smell.

**Diagrams:** [auth-sequence.md](../diagrams/auth-sequence.md) — token-in-claim → `AccessTokenHolder` → bearer attach.
