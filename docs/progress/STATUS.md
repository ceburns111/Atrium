# STATUS — read me first

**Updated:** 2026-07-01 (Run 2 set up; green baseline confirmed).

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

**Items A–D DONE** (`afb89eb` / `7ab96e5` / `da4abba` / `6c015d0`). The four "core" items are complete.
**Now the "## Last" bucket — best-effort + flag (subjective):** **item E** dark mode, then **item F** store
images. Dispatch an implementer for E: add a `:root[data-theme="dark"]` (+ `prefers-color-scheme` fallback)
token override in `tokens.css` (colors only; never touch component CSS — they read the vars) + a small
theme-toggle primitive in `Atrium.Design` wired into the shell top-bar, persisting to `localStorage` via
prerender-guarded interop (ADR-0010). Gate = build + test green; the *look* is subjective → mark `[~]`, flag
for the user, do NOT declare "done." Then F (generated on-brand SVG placeholders; flag for real imagery).

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
