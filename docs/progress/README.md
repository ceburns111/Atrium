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

- **Code items** (anything changing `.cs`/`.razor`/config that affects runtime): `dotnet test Atrium.slnx`
  — **all** unit + integration green (integration needs Docker). **AND** a Playwright agent-driven QA
  pass, screenshot-verified against the intended behavior, on the running stack. Only then commit.
- **Docs items** (Markdown only, no runtime change): there is nothing to unit- or Playwright-test —
  substitute **build-clean + an accuracy self-review**: re-read the real code the doc describes and
  confirm every path, command, class, and route named actually exists. Then commit.

> These guardrail tests exist mainly to keep autonomous agents honest; they may be pruned later. Bias
> toward a few high-signal checks over exhaustive coverage.

## Autonomy boundary (current)

**Auto-run + commit unattended: the docs items only (queue 10 → 2 → 3 → 1).** **STOP** before any
**code** item — item 6 (OTel/Serilog) and the low/tomorrow code items (csharpier config, testuser vs
customer) — and leave them for a supervised session. Also supervised (need the running stack): the
**browser-verify of #1 + the Admin modal**, and the **Dialog frontend-design polish** (user wants it
"centered + cute + better spacing"). Update this section if the user widens the boundary.

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
