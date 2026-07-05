# ADR-0011 — Circuit-scoped bearer handler for the AG-UI chat client

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** AI chat enhancements (2026-07)

## Context

[ADR-0004](0004-token-propagation-and-option-b.md) settled how the module typed clients authorize:
read the circuit-scoped `AccessTokenHolder`, attach the bearer per request, and use **no
`DelegatingHandler`** — because `IHttpClientFactory` builds handler chains in a **separate DI scope**,
a factory-registered handler reading the scoped holder comes back empty.

The AI chat surface broke the *shape* that rule assumed, without breaking its reason.
`AGUIChatClient` (the Microsoft Agent Framework AG-UI client behind the `AgentChat` primitive) **owns
its `HttpClient` internally**: you hand it a client at construction and it issues the SSE/POST traffic
itself. There is no typed-client send method where a per-request
`request.Authorize(tokens)` call could live — no seam. The only place to authorize its traffic *is* a
handler in the `HttpClient` pipeline, which is exactly what ADR-0004 appears to forbid.

## Decision

`BearerTokenHandler` (`src/Atrium.Design/BearerTokenHandler.cs`) is a `DelegatingHandler` that attaches
the signed-in user's bearer and translates a downstream 401 into the typed `SessionExpiredException`
([ADR-0008](0008-graceful-session-expiry-handling.md)) — the same `Authorize` /
`ThrowIfSessionExpired` helpers the typed clients use, written once.

It is **never registered with the factory**. `AgentChatClientFactory`
(`src/Atrium.Design/AgentChatClientFactory.cs`), a **scoped** service resolved inside the signed-in
Blazor circuit, composes it by hand:

1. Take the named client's pooled chain from
   `IHttpMessageHandlerFactory.CreateHandler(AgentChatDefaults.HttpClientName)` — so service discovery
   and telemetry (the host's `ConfigureHttpClientDefaults`) still apply.
2. Wrap it: `new BearerTokenHandler(tokens) { InnerHandler = gatewayChain }`, where `tokens` is **this
   circuit's** `AccessTokenHolder` — the wrapping happens in the circuit scope, so the handler captures
   the right holder.
3. Build `new HttpClient(bearer, disposeHandler: false)` and hand it to `AGUIChatClient`.
   `disposeHandler: false` is load-bearing: a `DelegatingHandler.Dispose()` cascades to its inner
   handler, and the inner chain is **pooled and owned by the factory**. The thin bearer holds no
   resources; the pooled chain must not be disposed by us.

### Why this does not violate ADR-0004

ADR-0004's real constraint was never "`DelegatingHandler` is forbidden." It was: **a handler built by
`IHttpClientFactory` cannot see circuit-scoped state**, because the factory constructs handler chains
in its own scope. `BearerTokenHandler` sidesteps the failure mode instead of hitting it — it is
constructed *manually, in the circuit scope*, after the factory has done its (scope-free) work.

### The rule, restated precisely

> Never register a bearer-attaching handler with `IHttpClientFactory`
> (`AddHttpMessageHandler`, or adding it to a named/typed client's chain) — it will read an empty
> holder. A `DelegatingHandler` that reads circuit-scoped state is legitimate **iff** it is
> constructed manually inside that scope and wraps the factory's pooled handler with
> `disposeHandler: false`.

## Consequences

- **One sanctioned exception, documented.** The categorical "no `DelegatingHandler`" phrasing in older
  docs was false against this code and invited a well-meaning "fix" of working code (audit finding D1);
  ADR-0004 carries an amendment pointing here.
- **Session expiry stays uniform.** A dead token mid-chat surfaces as the same typed
  `SessionExpiredException` the shell's `SessionErrorBoundary` already turns into a re-login prompt.
  The handler disposes the 401 response itself, since it never reaches the caller.
- **The caller owns the `HttpClient`.** `AgentChatClientFactory.Create` returns a client per chat; the
  `AgentChat` component disposes it on teardown. The pooled chain survives because of
  `disposeHandler: false`.
- **A registration trap to know:** the named client (`AgentChatDefaults.HttpClientName`) exists purely
  so its chain picks up service discovery/telemetry defaults. Adding the bearer to *that* registration
  (`AddAgentChat`) would reintroduce the ADR-0004 failure mode — the comment in
  `AgentChatServiceCollectionExtensions` guards the spot.

## Alternatives rejected

- **Register `BearerTokenHandler` via `AddHttpMessageHandler`** — the exact failure ADR-0004
  documents: the factory's scope has an empty `AccessTokenHolder`.
- **Pre-set `Authorization` as a default request header at client creation** — attaches the token but
  provides no per-response seam, so the 401 → `SessionExpiredException` translation is lost and expiry
  reverts to a raw failure mid-chat.
- **Wrap or fork `AGUIChatClient` to expose a per-request seam** — churn against a preview package to
  avoid a two-line handler; the handler is smaller and survives package updates.
