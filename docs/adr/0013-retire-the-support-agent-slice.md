# ADR-0013 — Retire the Support agent slice

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** pre-demo hardening (2026-07)

## Context

The AI Support agent (Microsoft Agent Framework over Ollama; AG-UI SSE chat at
`/storefront/agent`; OTel → guardrail → cache decorator pipeline; step-up MFA gate; telemetry-only
feedback; LLM eval suite) shipped in PR #1 and was hardened by the 2026-07-02 audit (A1–A7, all
remediated). It worked. It was also the single largest piece of surface area in the repo relative
to how central it is to what Atrium demonstrates: modular Blazor architecture, auth, and
backend/data discipline.

For the demo, that ratio is the problem. The slice invites deep questioning on a stack
(MAF preview APIs, guardrail prompt design, eval methodology) that is peripheral to the system's
core story, and it carries five preview-version NuGet dependencies.

A feature flag was considered and rejected: flagged-off code keeps 100% of the in-repo scrutiny
surface — the packages, the pipeline, the ADR exception — while adding a toggle on top.

## Decision

Remove the slice from `main` entirely. The complete working implementation is preserved on the
**`feat/support-agent`** branch (pushed to origin), cut from the last pre-removal commit.

Removed: `Support/` in the Storefront service (agent, tools, guardrail, cache, step-up MFA,
feedback endpoint), the AG-UI client plumbing in `Atrium.Design` (`AgentChat`, factory,
`BearerTokenHandler`, feedback client), the Portal `AssistantLauncher`, the `IModule.AgentSurfaces`
seam, `FeedbackDto`, the AppHost Ollama wiring, the unit-test suite for the slice, and the
`Atrium.Evals` project.

[ADR-0011](0011-circuit-scoped-bearer-handler.md) is superseded by this ADR: its sanctioned
exception (a manually-composed, circuit-scoped `DelegatingHandler` for the AG-UI client) left with
the slice. The underlying rule it carved an exception from — no *factory-registered* bearer
handlers, per [ADR-0004](0004-token-propagation-and-option-b.md) — stands unchanged.

## Consequences

- The demo surface is exactly the system's core story; no preview packages remain.
- Feedback/eval history stays in `docs/archive/runs/`, `docs/audits/`, and the 2026-07-02 spec —
  the work is documented, reviewable, and honestly dated, without being deployed.
- Anyone can `git switch feat/support-agent` and run the full agent locally (Ollama required).

## Future direction

The agent returns — but shaped like the rest of the platform, not embedded in one vertical:

- A dedicated core service (working name `Atrium.Services.Agent`) owns the model pipeline
  (provider selection, guardrail, cache, telemetry) and exposes chat per registered surface.
- Its tools call the capability services **over HTTP with the relayed bearer** (the same
  composition grain as ADR-0005) instead of reaching into one service's repositories.
- Modules contribute chat surfaces declaratively — the `IModule.AgentSurfaces` +
  `AssistantLauncher` pattern preserved on `feat/support-agent` is the starting point; the branch
  is the reference implementation for the pipeline, the guardrail posture (screen all user
  messages, fail closed), and the eval harness.

Revive by porting from the branch, not by reverting the removal commits — the platform will have
moved (notably: the UI layer is migrating to MudBlazor, which replaces the chat styling substrate).
