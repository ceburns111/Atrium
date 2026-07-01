# Atrium progress system — runbook for resumable (overnight) work

This folder lets an agent execute a queue of work **across sessions and context clears**. Everything
needed to resume lives here on disk (git-tracked) + one memory entry — so a cold session with zero
conversation context can pick up deterministically.

## Files

- **`STATUS.md`** — the single source of truth for *where we are right now*: current item, the next
  concrete action, stack state, and any blockers. **Read this first.** Keep it short and current.
- **`QUEUE.md`** — the ordered backlog with checkboxes. Done items stay, ticked, with their commit hash.
- **`LOG.md`** — append-only history: timestamp · item · outcome · commit. Never rewrite; only append.
- **`README.md`** — this runbook.

## How to resume (a fresh session starts here)

1. Read `STATUS.md` → the current item and next action. Read `QUEUE.md` for the ordered plan.
2. **Start a self-paced `/loop`** so the cadence survives usage-limit pauses within the session
   (see "Cadence" below). Skip if running a single supervised pass.
3. Do the next increment for the current item, subject to **The Gate** below.
4. On green: **commit** (with the `Co-Authored-By` trailer), then update `STATUS.md` (advance),
   append to `LOG.md`, tick `QUEUE.md`. Then move to the next item.
5. Stop at the **autonomy boundary** (below) or when out of queue / capacity.

## The Gate (must pass before every commit)

Always: `dotnet csharpier format .` → `dotnet build Atrium.slnx -v q` must be **0 warnings, 0 errors**.

Then, by item type:

- **Code items** (anything changing `.cs`/`.razor`/config that affects runtime): `dotnet test
  Atrium.slnx` — **all** unit + integration green (integration needs Docker). Then a Playwright
  agent-driven QA pass, screenshot-verified, **if the stack is up**. If the stack can't be started, do
  **not** block: commit anyway with an explicit **"browser-unverified — stack unavailable"** line in the
  commit body + `LOG.md`. Honest notes beat a stalled queue (the user reverts if needed).
- **Docs items** (Markdown only, no runtime change): nothing to unit- or Playwright-test — substitute
  **build-clean + an accuracy self-review**: re-read the real code the doc describes and confirm every
  path, command, class, and route named actually exists. Then commit.

> These guardrail tests exist mainly to keep autonomous agents honest; they may be pruned later. Bias
> toward a few high-signal checks over exhaustive coverage.

## Autonomy boundary (current) — run the whole queue, keep it revertible

Run the **entire** queue in order, unattended, committing each item. There is **no stop-before-code**
pause. The safety net is git hygiene, so it is mandatory:

1. **One atomic, single-purpose commit per item** — never bundle two items. So any item reverts alone.
2. **Docs before code** (the queue order already does this). Do **all** of 10 → 2 → 3 → 1 first.
3. **Record the clean revert point.** After the last docs item (1) commits, write its hash into `LOG.md`
   and `STATUS.md` as `SAFE-REVERT-POINT (last docs commit)` *before* touching any code item. That's the
   hash the user resets to if the autonomous code is jacked up but the docs are good.
4. **Do not auto-implement item 4** (API docs) — it needs the user's viewer choice; present the
   recommendation in `QUEUE.md` and skip it.
5. The **Dialog "cute"/spacing polish** is subjective — a best-effort `frontend-design` pass is fine
   (reversible CSS), but flag it in `LOG.md` for the user's eye; don't treat it as done.

## Cadence & limits (honest)

- Within a session, drive with a self-paced `/loop`; use `ScheduleWakeup` to continue across pauses.
  There is **no way to poll a numeric usage quota** — the durable files are the real safety net, so keep
  them current after *every* step, not just at commit time.
- Playwright/OTel work needs the **local Aspire stack up with Docker** (`cd src/Atrium.AppHost &&
  aspire run`; ports are dynamic — rediscover via `lsof`). If it's down and can't be started, record a
  blocker in `STATUS.md` and skip to the next item that doesn't need it rather than spinning.

## Relationship to other docs

`docs/HANDOFF.md` is the durable "state of the project" note (architecture, how-to-run, gotchas). This
folder is the **live work queue** layered on top. When the queue is drained, fold anything lasting into
`HANDOFF.md` and let this folder go quiet.
