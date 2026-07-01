# ADR-0008 — Handle expired-session 401s gracefully instead of crashing the circuit

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 7 (post-polish)

## Context

The access token is captured once at login and never refreshed ([ADR-0004](0004-token-propagation-and-option-b.md)),
so after ~5 minutes it expires. An idle Blazor **Server** circuit outlives its token: the next action —
e.g. an Admin **Save** — sends the now-dead bearer, the gateway answers `401 Unauthorized`, and the
typed client's `EnsureSuccessStatusCode()` throws a raw `HttpRequestException`. Unhandled, that fell
through to the framework's generic `#blazor-error-ui` overlay ("An unhandled error has occurred") and
**terminated the circuit** — a jarring dead-end for what is really just "please sign in again".

We wanted expiry to degrade to a friendly, recoverable prompt. We did **not** want to add token refresh
here — that's still the option-B / `Duende.AccessTokenManagement` path in ADR-0004.

## Decision

Turn a 401 into a **typed, recoverable signal** and catch it at the shell.

1. **A typed exception.** `SessionExpiredException` (`src/Atrium.Design/SessionExpiredException.cs`) —
   a `sealed` exception meaning "a downstream API returned 401; the session has expired."
2. **A one-line mapping helper.** `HttpResponseMessage.ThrowIfSessionExpired()` (an extension in the
   same file, class `HttpResponseSessionExtensions`) throws `SessionExpiredException` when the response
   is `HttpStatusCode.Unauthorized`. Each typed client calls it **before** `EnsureSuccessStatusCode()`,
   so an expired token surfaces as the typed signal rather than a generic failure.
3. **All four typed clients map 401** through the helper: `CatalogClient` and `OrdersClient`
   (`Atrium.Modules.Storefront`), `ReportsClient` (`Atrium.Modules.Reports`), and `AdminCatalogClient`
   (`Atrium.Modules.Admin`). A **403** (wrong role) is deliberately *not* mapped — you're still signed
   in, so it stays an inline toast.
4. **A shell-level boundary.** `SessionErrorBoundary`
   (`src/Atrium.Portal/Components/Layout/SessionErrorBoundary.razor`) `@inherits ErrorBoundary` and
   wraps `@Body` in `MainLayout`. For a `SessionExpiredException` it renders a "your session has
   expired — sign in again" panel with a login link; for **any other** exception it renders a generic
   card **and logs it server-side** (via `OnErrorAsync`) instead of crashing the circuit into the
   browser console. `MainLayout` calls the boundary's `Recover()` on `LocationChanged` so the error
   state clears on navigation.

## Consequences

- **Expiry is now a soft landing.** The user sees a "sign in again" panel and the circuit survives,
  instead of the framework's terminal error overlay.
- **The boundary also nets genuine faults.** Non-session exceptions get a card plus a server log rather
  than tearing down the circuit — a strict improvement in shell resilience.
- **This is handling, not a fix.** The token still doesn't refresh; the real fix remains option B →
  `Duende.AccessTokenManagement` (ADR-0004). ADR-0004's "no refresh" consequence was updated to point
  at this flow.
- **Guarded by tests.** `SessionExpiredTests` (`tests/Atrium.UnitTests`) assert 401 → `SessionExpiredException`
  and 500 → still `HttpRequestException`, so the mapping can't silently over-catch non-401 failures.

## Alternatives rejected

- **Add token refresh now.** The correct production answer, but larger than this pass needed; it's the
  ADR-0004 option-B/Duende step, sequenced after.
- **Catch 401 in each component.** Scatters the same recovery UI across every page; the shell boundary
  states it once around the module body.
- **Let the generic error UI handle it.** That terminates the circuit and reads as a crash for an
  expected, benign condition.
