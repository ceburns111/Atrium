# STATUS — read me first

**Updated:** 2026-07-01 (Run 2 complete + live-verified; **Run 3 planned, not started**).

## ⏭ NEXT: RUN 3 — planned, NOT started (resume here)

The user will **clear context**, then start an **unattended run** against the new `TODO.md` backlog. A fresh
zero-context session resumes from this section. **Confirm the two open decisions with the user before
building** (don't assume), then set up a `QUEUE.md` (archive/replace the Run-2 queue below), branch, baseline-green,
and run the loop per `README.md`.

**The `TODO.md` backlog + honest unattended-ability (agreed 2026-07-01):**
| Item | Unattended? | Notes |
|---|---|---|
| **Microsoft Test Platform + xUnit** | ✅ full | Pure code+config; build/test gate catches it. Best first item. |
| **Azure deploy via IaC** | ⚠️ author-only | Write + lint Bicep/Terraform + `what-if`; **no live deploy** unattended (needs the user's Azure account, creds, cost-limit setup — their listed prereq). Deploy = supervised. |
| **Support chatbot + MFA + Azure AI Foundry** | ⚠️ partial | Chat **module/UI + a mockable AI backend** is unattended-safe; **Foundry wiring + MFA** need cloud creds/decisions → supervised. |

**Recommended scope:** unattended-safe slice = (1) MTP/xUnit end-to-end; (2) **author** the Azure IaC +
scaffold the chatbot module against a **mocked** AI backend — all gate-verifiable — leaving cloud/credential
wiring teed up for a supervised session.

**Open decisions to confirm at run start:**
1. **Scope:** the full unattended-safe slice above, or **just MTP/xUnit first** as a clean single-item run?
2. **Branch base:** off `main` (clean; these items are largely independent of the storefront work) or off the
   current unmerged `feat/storefront-checkout-diagrams`? (Run 2 is still unmerged — see below.)

**Also still pending from Run 2:** review + merge `feat/storefront-checkout-diagrams` → `main` (all A–G
done + live-verified). And `HANDOFF.md` retirement was deferred (6 docs link to it — fold content + fix links first).

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
