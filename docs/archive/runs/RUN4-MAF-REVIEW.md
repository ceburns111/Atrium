# Run 4 — MAF/AIUI slice review (drift · jank · organization · conventions)

**Branch:** `review/maf-slice` (off `main`). **Scope:** the support-chatbot slice, commits **C0–C5**
(`aa01623`..`9ff317c`) — the MAF (Microsoft Agent Framework) order-support agent, the AG-UI transport,
the `<AgentChat>` design primitive, and the module/portal wiring. **Baseline:** build 0W/0E, `dotnet test`
81/81, csharpier clean.

## Method

Thin-orchestrator + three parallel domain reviewers (per `README.md`), each loading the matching
`atrium-*` skill as its convention baseline, plus the orchestrator's own full read of every changed file:

| Reviewer | Skill baseline | Surface |
|---|---|---|
| Backend service | `atrium-service` | `Services.Storefront/Support/*`, `Orders/*`, `Program.cs`, sproc |
| Design/UI RCL | `atrium-ui` | `Design/AgentChat*`, `BearerTokenHandler`, `AgentChatClientFactory` |
| Module/Portal/Abstractions | `atrium-module` | `Abstractions/AgentSurface`, `IModule`, `AssistantLauncher`, Portal wiring |

Every reviewer finding was **verified against the code before acting** — one finding was rejected as false
(below). This is a *review-and-fix* pass: clear wins were fixed with documented reasoning; subjective or
high-churn items were deferred with reasoning, not silently dropped.

## Overall verdict

The slice is **high quality and written to the conventions rather than around them** — fully tokenized
CSS, correct singleton-agent / request-scoped-tools lifetime reasoning, sproc-enforced user-scoping that is
the best-tested part of the change, a sound bearer-token seam, and disciplined disposal. **No High-severity
data-leak or gate-bypass bug exists.** The real weaknesses were consistency gaps: an opt-in step-up gate
that could be silently weakened by misconfiguration, a launcher that didn't apply the role gate every other
shell consumer uses, and a scatter of low-severity jank. All of those are now fixed.

---

## Fixed (13 findings) — 4 atomic commits

### Group A — step-up MFA hardening (`refactor(support): harden step-up MFA gate`)
- **[Med SECURITY] `Simulate` had no environment guard** — `Enabled=true`+`Simulate=true` treated *any*
  authenticated user as stepped-up in *any* environment. → `StepUpMfaHandler` now takes `IHostEnvironment`;
  `Simulate` is honored **only in Development**. A stray `Simulate=true` in a deployed config can no longer
  bypass the ceremony. (+ a Production-simulate-denial test.)
- **[Med SECURITY] the gate is opt-in and could go inert silently** — a deploy that forgets
  `SupportAgent:StepUp:Enabled=true` downgrades `/storefront/agent` to authenticated-only with no signal.
  → `WarnIfStepUpGateInert()` logs a startup **warning** outside Development. (Chose a warning over
  flipping the default so the demo/dev path is unchanged, but the misconfiguration is now visible.)
- **[Low DRIFT] extension misnamed** — `SupportAgentServiceCollectionExtensions` extends
  `IHostApplicationBuilder`, not `IServiceCollection`. → renamed to `SupportAgentBuilderExtensions`.

### Group B — assistant launcher correctness (`fix(portal): role-gate + stabilize the assistant launcher`)
- **[High CONVENTION] launcher didn't honor `RequiredRole`** — every other shell consumer (`NavMenu`,
  home cards) gates by module role; the launcher didn't, so a role-gated module's agent button would show
  to any authenticated user (latent today — only the un-gated Storefront has a surface). → surfaces are
  filtered by the same `IsVisible(module, user)` gate. *Chose module-level gating (reusing the existing
  `IModule.RequiredRole`) over adding a new per-surface `RequiredRole` field — YAGNI; the need today is
  "don't show a gated module's agent," which is module-level.*
- **[Med JANK] re-render on every navigation** — the launcher compared surfaces with `ReferenceEquals`,
  but `AgentSurfaces` is a computed property allocating a fresh record each call, so the guard was always
  true. → compares by the stable `Endpoint` identity. (Record value-equality wouldn't fix it either —
  `StarterPrompts` is an array, compared by reference.)
- **[Med JANK] non-deterministic off-section fallback** — depended on module discovery order. → visible
  modules are ordered by `BasePath` before selection.

### Group C — backend organization / symmetry (`refactor(support): endpoint symmetry + FindProduct guard`)
- **[Low DRIFT] AG-UI endpoint mapped inline in `Program.cs`** — unlike `OrdersEndpoints`/`ReportsEndpoints`
  which own their subtree via a `Map*` extension. → hoisted into `SupportEndpoints.MapSupportAgent`.
- **[Low JANK] `FindProduct` matched the whole catalog on a blank query** — returned an arbitrary first
  five. → asks for a keyword instead (+tests).
- **[Low DRIFT] `AgentSurface.Endpoint` doc drift** — the XML example showed a leading slash
  (`"/storefront/agent"`) that would break `<AgentChat>`'s gateway-relative resolution; the real convention
  is no leading slash. → corrected the doc + noted it's a topology path (vs `NavItem.Path`, a portal route).

### Group D — design/UI polish (`fix(design): AgentChat observability + resource/scroll polish`)
- **[Med JANK] swallowed error with no telemetry** — `catch (Exception)` showed a generic notice and logged
  nothing. → injects `ILogger<AgentChat>`, logs the failure (never the token) before the notice.
- **[Low JANK] undisposed 401 response** — `BearerTokenHandler` threw `SessionExpiredException` after
  `SendAsync` without disposing the response the caller never receives. → disposes it before throwing.
- **[Low JANK] scroll yanked the viewport down** — auto-scroll fired on every token even if the user
  scrolled up. → `scrollToEnd` only pins when already near the bottom.
- **[Low DRIFT] caret hard-coded `0.5rem`/`1rem`** — contradicting the CSS file's "every value is a token"
  header. → tokenized to `--space-2`/`--space-4` (exact equivalents).

---

## Deferred (with reasoning — not silently dropped)

- **Extract an `Atrium.Client`/infrastructure project** out of `Atrium.Design` (which now also holds the
  HTTP factory, `BearerTokenHandler`, `AccessTokenHolder`, session helpers, and a preview agent SDK).
  *Real observation, but a high-churn architectural move, and the placement is **consistent with existing
  precedent** — the auth/HTTP helpers already live there. Worth doing as its own deliberate refactor with
  the user in the loop, not folded into a review pass.* Noted as accepted debt.
- **Starter-prompt chips → a `Button` variant.** The chips re-implement hover/focus/disabled that `Button`
  owns, but they're an intentionally distinct pill shape; converting risks a visual regression for a
  subjective gain. *Flagged, not changed.*
- **`AgentSurfaces` is plural but the launcher renders only the first.** Kept plural as a deliberate
  extension point (mirrors `NavItems`); the launcher's first-surface behavior is now documented. A second
  surface is unreachable today but the contract is future-proof.
- **`AgentSurface.Icon` is declared but unused.** Mirrors `NavItem.Icon` (also unused) — consistent, not
  new drift; left for whenever icons are wired.
- **`GetOrderStatus` returns a hard-coded "Confirmed" status.** Already honestly documented (the store has
  no status column); a `const` wouldn't fix the deeper "if a status column ever lands, this silently lies"
  concern, and inventing a lifecycle would be worse. Left as-is.
- **Real `IChatClient` registered as an externally-created singleton instance** (DI won't dispose it).
  Negligible at app lifetime. Left as-is.

## Rejected (verified false)

- **"`StepUpMfaHandler` has no unit test."** False — `tests/Atrium.UnitTests/Support/StepUpMfaHandlerTests.cs`
  already covers the full Disabled / Simulate / amr / acr / unauth / config-override matrix. Confirmed by
  reading the file before acting; extended it with the new Production-simulate case rather than duplicating.

## Result

**Gate:** csharpier clean · build **0W/0E** · `dotnet test` **84/84** (81 baseline + 3 new). Four atomic
commits on `review/maf-slice`. No behavior change to the happy path; the security posture is tightened and
the low-severity jank is gone. Ready to review + merge.
