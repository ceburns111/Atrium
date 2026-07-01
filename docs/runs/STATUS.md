# STATUS — read me first

**Updated:** 2026-07-01 (Run 2 merged; **Run 3 IN PROGRESS** on `feat/support-chatbot`).

## ▶ RUN 3 — IN PROGRESS (resume here)

**Branch:** `feat/support-chatbot` (off `main`). **Baseline (run start):** csharpier no-op, build 0W/0E,
`dotnet test` **56/56** (MTP runner confirmed), Docker up — green, cleared to run.

**Current item:** **C3** — next (AG-UI endpoint + gateway route + step-up MFA policy + integration test). ✅ **C2** done (C2a data + C2b agent/tools/model).

**C2 is split into two atomic commits** (per the spec guardrail — land framework-sensitive work in
pieces): **C2a** = user-scoped "look up one order" data layer (`usp_Order_GetById` + `GetByIdAsync`
scoped by UserName for security + integration tests; NO status column exists, so no invented lifecycle).
**C2b** = `SupportAgent` + tools (`GetOrderStatus` wrapping C2a's method + derives an honest
"Confirmed"-style status; product lookup via `StorefrontCatalogClient`) + config-driven `IChatClient`
(Dev default = fake, `FoundryLocal`/`AzureFoundry` via config) + unit tests.

**★ Real MAF 1.12.0 API shape (verified in C0 — use in C1–C5, docs sketch was wrong):** create via
`new ChatClientAgent(IChatClient, instructions:, name:, tools: IList<AITool>?)` → `AIAgent`; run via
`agent.RunAsync(string, ...)` → **`AgentResponse`** (`.Text`, `.Messages`); session type is
`AgentSession`. There is **no** `IChatClient.CreateAIAgent(...)` and **no** `AgentRunResponse` in this
release. Tools built via `AIFunctionFactory.Create(...)`. Fake for tests:
`tests/Atrium.UnitTests/Support/FakeChatClient.cs`.
Spec: **[`RUN3-SUPPORT-CHATBOT.md`](RUN3-SUPPORT-CHATBOT.md)**. Execute under `README.md` (thin
orchestrator, one implementer subagent per item, deterministic gate, atomic commit per item). Keep this
file + `LOG.md` current after every step. **`docs/bugs/CARROTPAD.png` is the user's stray asset — leave
untracked, never `git add -A`.**

**The [DISCUSS FIRST] discussion happened (2026-07-01).** Outcome + full queue:
**[`RUN3-SUPPORT-CHATBOT.md`](RUN3-SUPPORT-CHATBOT.md)** ← the run spec.

**What was decided (so it isn't re-litigated):**
- **Azure deploy — DEFERRED, not in this run** (user's call). It's a *supervised* effort later (needs the
  user's Azure account/creds). Agreed direction (ACA via `azd`, Entra/Keycloak split, cost/teardown,
  CI/CD) is captured in the spec's "Deferred" section.
- **Support chatbot — IN this run, agreed shape:** slice-first, **MAF/AG-UI (mandatory)** + Azure AI
  Foundry (cloud) / Foundry Local (dev) / fake (tests); a **Storefront support agent** (`GetOrderStatus`
  + product lookup) gated by **step-up MFA** (Entra `amr` in cloud; Keycloak-ACR + dev-simulate locally,
  config-driven). Design basis: `../ATRIUM-AI-EXTENSIBILITY-DESIGN.md`.
- **MTP + xUnit — ALREADY DONE** (discovered 2026-07-01: `global.json` runner = MTP, `xunit.v3.mtp-v2`,
  no legacy VSTest SDK). Ticked in `TODO.md`; **not a queue item.**

**The Run 3 queue:** **A** NavMenu visible-vs-loaded count → **C0–C5** the chatbot slice (C0 = MAF/AG-UI
package spike = **go/no-go**; `BLOCKED` if the preview stack won't wire — **no MEAI substitution
unattended**). Full specs + gate + guardrails in the spec file.

**Run 2 is MERGED to `main`** (merge commit `09b42b8`, 2026-07-01; `main` green — build 0W/0E, tests 56/56).
Branch `feat/storefront-checkout-diagrams` can be deleted at leisure. `HANDOFF.md` retirement is still
deferred (6 docs link to it — fold content + fix links first).

---

## ✅ RUN 2 COMPLETE — queue drained, wound down cleanly (2026-07-01)

All seven items (A–G) are done and committed on `feat/storefront-checkout-diagrams`; `main` is untouched.
Gate green throughout (build 0W/0E, tests **56/56**, Docker up). Wake-up summary: **`docs/runs/GOOD-MORNING.md`**.

**✅ LIVE-VERIFIED (2026-07-01):** the whole flow was driven through the **Playwright MCP** against
`aspire run` — anon browse, sign-in gate, **cart survival**, payment decline/approve (real order `#7002`),
role-gated cards, dark mode + the Save-button fix. All ✅; evidence in
[`verification/`](verification/) (10 screenshots + a re-runnable playbook). This also seeded
**`docs/agentic-workflow.md`** (the portfolio write-up of the whole harness). **Only remaining: review +
merge the branch → `main`** (optionally swap in real product photography for F).

Commits: A `afb89eb` · B `7ab96e5` · C `da4abba` · D `6c015d0` · E `cbf4fb2` · F `3ab697a` · G `b332388`
(+ per-item bookkeeping commits). **`SAFE-REVERT-POINT = afb89eb`** (drops the B–G auth/checkout/diagram/
theme phase, keeps the card fix + setup).

## Where we are

**Run 2 — storefront / checkout / diagrams.** New autonomous queue, branch
`feat/storefront-checkout-diagrams` (created off the unmerged `fix/modal-center-and-reports-gate`, which
carries the reports-gate + centered-modal fixes). `main` stays pristine for the user to review/merge.

**Run 1 (2026-07-01 overnight) is DONE and merged to `main`** — ADR sweep, new-app guide, AGENTS.md +
authoring skills, per-project READMEs, OpenAPI/Redoc, OTel/Serilog, application logging, role-gating,
Forbidden view, UI audit. Detail lives in `LOG.md` (append-only) + `GOOD-MORNING.md`. Not re-litigated here.

**Baseline (this run):** branch created; `dotnet build` 0W/0E; `dotnet test` **23/23**; Docker up;
csharpier 1.3.0 no-op. Green — cleared to run the queue.

**Queue (see `QUEUE.md`):** **A** home-card role-gating → **B** anonymous storefront + checkout sign-in
gate → **C** basic payment/checkout → **D** architecture + UI-flow mermaid diagrams → **E** dark mode
(best-effort) → **F** store images (best-effort). None started yet.

## Next concrete action

**Items A–D DONE** (`afb89eb` / `7ab96e5` / `da4abba` / `6c015d0`); **E `[~]`** (`cbf4fb2`) + **F `[~]`**
(`3ab697a`) best-effort. **Now item G — dark-mode button colors** (user feedback + screenshot 2026-07-01):
the accent "Save" button is pale/washed with invisible text and the ghost "Cancel" label is too faint in
dark mode. Inspect `.btn`/`.btn--accent`/`.btn--ghost` in `atrium.css` + the tokens they consume; fix the
**dark token overrides** (tokens-first) so every button variant has a legible fill + AA label in BOTH
themes. Gate = build + test green; look still needs the user's eye → mark `[~]`. After G, wind down: write
the wake-up summary + STATUS pointing at the supervised remainder.

## Autonomy boundary

Run the whole queue unattended on `feat/storefront-checkout-diagrams`, one atomic single-purpose commit per
item, thin-orchestrator + one implementer subagent per item, escalation/backoff per `README.md`. The gate is
**deterministic only** (csharpier + build + test) — **no unattended browser/aspire**; all live/login/visual
checks are the supervised pass in `QUEUE.md`. Items **E** and **F** are subjective/asset-driven → **best-effort
+ flag** (`[~]`, never claim "done"), the same way Run 1 handled the Dialog aesthetic polish.

**Tiering:** A = Tier-1 (auth-adjacent, low-risk). **B = Tier-1 mandatory (auth surface).** C = Tier-1
(order-flow change). D = Tier-1 accuracy (showcase doc — grep every node/edge). E/F = Tier-0 best-effort.

**★ SAFE-REVERT-POINT = `afb89eb`** (item A) — the last pre-auth-phase commit. `git reset --hard afb89eb`
on `feat/storefront-checkout-diagrams` drops the whole B/C auth+checkout phase and keeps the low-risk card
fix + the run setup. Everything after this is the auth/checkout/diagram work.

## Stack / environment

- **Docker up** (integration tests use Testcontainers). **Aspire stack NOT required** — deterministic gate
  only; live checks are the supervised pass.
- Branch: `feat/storefront-checkout-diagrams`. `main` pristine.

## Blockers

- None yet. Anticipated judgment calls, handled by default (documented in LOG for morning review):
  - **C payment realism** — simulated payment, no real gateway, no card storage (kept honest).
  - **F store images** — no unattended web-download of licensed photos; generated on-brand SVG placeholders,
    flagged for the user to swap in real imagery (or `BLOCKED` if it can't be done cleanly).

## How to resume

Say "resume the storefront run"; the agent reads this file, confirms the green baseline on
`feat/storefront-checkout-diagrams`, and works `QUEUE.md` in order (A→F) under the gate in `README.md`.
