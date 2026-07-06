# Retire the Support agent slice — design

**Date:** 2026-07-05
**Status:** Approved (spec); implementation plan to follow
**Executes:** first of two sequential initiatives (this, then the MudBlazor migration)

## Context

The AI Support agent (Microsoft Agent Framework over Ollama; AG-UI SSE chat; guardrail/cache/OTel
pipeline) shipped in PR #1 and was hardened by the 2026-07-02 audit. It works — but for the interview
demo it is too much surface area to defend in depth, and it risks pulling scrutiny onto areas away
from the strongest competencies (auth, backend, architecture). `docs/interview/07-CLARIFICATIONS.md`
already records the decision: scrap it.

**Decision (2026-07-05):** remove the slice from `main` entirely, preserve it on a branch, and
document — in an ADR — the intent to eventually revive it from that branch as a *modular,
cross-module* agent capability rather than a Storefront-embedded one.

Rejected alternatives:
- **Feature flag, code stays** — doesn't solve the stated problem: flagged-off code (5 preview MAF
  packages, guardrail pipeline, evals project, ADR exception) remains in-tree and quizzable.
- **Extract to a modular agent service now** — architecturally the desired end-state, but it *grows*
  the surface area before the demo. Recorded as Future Direction instead.

## Preservation

Before any deletion, create and push `feat/support-agent` pointing at the last pre-removal commit:

```bash
git branch feat/support-agent <pre-removal-commit>
git push -u origin feat/support-agent
```

Everything removed below remains recoverable there. The branch is referenced by name in ADR-0013.

## Removal inventory

Discovery (2026-07-05) confirmed every seam; no non-AI code references any of these.

### `src/Atrium.Services.Storefront`
- Delete the entire `Support/` folder (10 files: `SupportAgent.cs`, `SupportEndpoints.cs`,
  `SupportAgentBuilderExtensions.cs`, `GuardrailChatClient.cs`, `SupportTools.cs`,
  `FeedbackEndpoints.cs`, `StepUpMfa.cs`, `SupportTelemetry.cs`, `TtlDistributedCache.cs`,
  `CannedChatClient.cs`). Step-up MFA has no consumer outside the agent endpoint.
- `Program.cs`: remove `builder.AddSupportAgent()`, `storefront.MapSupportAgent()`,
  `storefront.MapSupportFeedback()`, `app.WarnIfStepUpGateInert()`, and any now-unused usings /
  OTel source registrations for `SupportTelemetry` names.
- csproj: remove `Microsoft.Agents.AI`, `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`,
  `Microsoft.Extensions.AI.OpenAI`, `OpenAI` package references (verify nothing else uses
  `Microsoft.Extensions.AI` abstractions afterward; remove those too if orphaned).

### `src/Atrium.Design`
- Delete: `Components/AgentChat.razor` + `AgentChat.razor.css`, `wwwroot/js/agentchat.js`,
  `AgentChatClientFactory.cs`, `AgentChatServiceCollectionExtensions.cs`, `FeedbackClient.cs`,
  `BearerTokenHandler.cs` (its only consumers are the chat factory and feedback client — module
  typed clients attach the bearer via `request.Authorize()` inside `SendForJsonAsync`).
- csproj: remove `Microsoft.Agents.AI.AGUI`. The `Atrium.Contracts` project reference stays only if
  Design still uses a contract type after `FeedbackDto` is gone — verify; drop it if orphaned.
- **Stays:** `AccessTokenHolder`, `HttpClientExtensions` (`SendForJsonAsync`),
  `SessionExpiredException`, `Money`, `Toasts`, all non-chat components, `theme.js`, `dialog.js`.

### `src/Atrium.Portal`
- Delete `Components/Layout/AssistantLauncher.razor`; remove its topbar usage in `MainLayout.razor`.
- `Program.cs`: remove `builder.Services.AddAgentChat()`.

### `src/Atrium.Abstractions`
- Delete `AgentSurface` record and the `IModule.AgentSurfaces` member (a dead abstraction post-removal
  reads as over-engineering; the pattern is preserved on the branch and named in ADR-0013).

### `src/Atrium.Modules.Storefront`
- Remove the `AgentSurfaces` implementation from `StorefrontModule.cs`.

### `src/Atrium.Contracts`
- Delete `FeedbackDto.cs`.

### `src/Atrium.AppHost/apphost.cs`
- Remove the four `SupportAgent__*` `.WithEnvironment(...)` lines from the storefront resource
  (lines 37–40 as of this writing). No Ollama container resource exists; nothing else changes.

### Tests
- Delete `tests/Atrium.UnitTests/Support/` (9 files) plus `BearerTokenHandlerTests.cs` and
  `FeedbackControlTests.cs` (bUnit tests of the chat feedback thumbs), both in `tests/Atrium.UnitTests`.
- Delete the `tests/Atrium.Evals/` project entirely and remove it from `Atrium.slnx`.
- **Stay:** `SessionExpiredTests`, `MenuTests`, all service/integration tests.

### Gateway
- No changes — routes are prefix-based config; `/storefront/agent` simply stops existing.

## New/updated documentation

- **New `docs/adr/0013-retire-the-support-agent-slice.md`** — context (shipped, audited, cut for
  demo scope discipline), decision (full removal from `main`, preserved on `feat/support-agent`),
  consequences, and a **Future Direction** section: the intended revival is a dedicated core service
  (e.g. `Atrium.Services.Agent`) exposing chat per module surface, with HTTP tools into each
  capability service — resurrecting the `IModule.AgentSurfaces` pattern from the branch — so the
  agent becomes as modular as the rest of the platform instead of being embedded in one vertical.
- **ADR-0011** (`circuit-scoped-bearer-handler`): set `Status: Superseded by ADR-0013`. Do not
  delete — ADRs are history. Add a one-line note that the sanctioned exception left with the slice
  and the underlying rule (no factory-registered bearer handlers) still stands in ADR-0004.
- **ADR index** (`docs/adr/README.md`): add 0013, mark 0011 superseded.
- **`CLAUDE.md`**: remove the evals test command + Ollama/eval-results paragraph, the Support-agent
  bullet, the agent items in the auth matrix (step-up MFA), the AG-UI mentions in the Design
  description, and the ADR range ("0001–0012" → "0001–0013").
- **`AGENTS.md` / `docs/ARCHITECTURE.md`**: remove agent topology/pipeline sections; reference
  ADR-0013 where the story is worth one line.
- **`docs/ATRIUM-AI-EXTENSIBILITY-DESIGN.md`** → move to `docs/archive/`.
- **Historical docs untouched:** `docs/archive/runs/RUN3*/RUN4*`, `docs/audits/2026-07-02-full-audit.md`,
  `docs/superpowers/specs/2026-07-02-ai-chat-enhancements-design.md` (history stays).
- **`docs/interview/07-CLARIFICATIONS.md`**: tick the "Remove / deactivate the MAF agent slice"
  pre-demo checkbox with a note pointing at ADR-0013 + the branch. (Several answers already say
  "being removed with that slice" — spot-check they now read true.)
- **Local (non-repo) cleanup, done by the orchestrator at run end:** remove the `SessionStart` hook
  in `.claude/settings.local.json` that mandates reading the AI-chat spec (gitignored, this machine
  only), and update the operator's auto-memory note about the AI-chat run.

## Validation

Run mechanics follow the archived runbook (`docs/archive/runs/README.md`): dedicated run branch,
one atomic commit per item, orchestrator re-runs the gate itself before every commit, max 2 attempts
then revert-to-green + BLOCKED, SAFE-REVERT-POINT after the low-risk phase.

### Lane A — deterministic gate (per commit; authoritative)
```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q   # 0 warnings / 0 errors
dotnet test Atrium.slnx                                       # Docker up for the integration lane
```
Plus a **grep gate** (final commit): zero hits outside `docs/archive/`, `docs/audits/`,
`docs/superpowers/specs/2026-07-02-*`, `docs/interview/`, and `docs/adr/` for:
`AgentChat`, `SupportAgent`, `AGUI`, `Microsoft.Agents`, `agentchat`, `FeedbackDto`, `StepUpMfa`,
`AgentSurface`, `AddAgentChat`, `BearerTokenHandler`, `IFeedbackClient`, `Ollama` (src + tests must
be clean; docs listed above are sanctioned history).

### Lane B — live smoke (end of run, unattended via Playwright)
Ollama deliberately **not** running — proving nothing needs it is part of the test.

1. `cd src/Atrium.AppHost && aspire run` (background); wait for health:
   - Catalog: http://localhost:5260/health → `Healthy`
   - Storefront: http://localhost:5109/health → `Healthy`
2. Drive the Portal at https://localhost:7001 (fallback http://localhost:5035):
   - Sign in via Keycloak (http://localhost:8080) as `testuser` / `password`.
   - Topbar shows theme toggle + user menu and **no chat icon**.
   - Shop → add to cart → cart → checkout → order confirmation → orders list shows the order.
   - Sign out; sign in as `admin` / `password`; Admin product list renders; Reports renders.
   - Zero browser console errors on every page visited.
3. Screenshots of each page to the run's gitignored `artifacts/` folder; pass/fail per step in the
   run LOG.

## Out of scope
- Any MudBlazor work (separate spec/plan; executes after this).
- Building the modular agent service (Future Direction in ADR-0013 only).
- Rewriting historical docs/audits/specs.
