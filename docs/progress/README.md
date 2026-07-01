# Atrium progress system — runbook for resumable (overnight) work

This folder lets an agent execute a queue of work **across sessions and context clears**. Everything
needed to resume lives here on disk (git-tracked) + one memory entry — so a cold session with zero
conversation context can pick up deterministically.

## Files

- **`STATUS.md`** — the single source of truth for *where we are right now*: current item, next concrete
  action, attempt count, branch, stack state, blockers. **Read this first.** Keep it current after every
  step (it is the only durable state across a context clear).
- **`QUEUE.md`** — the ordered backlog with checkboxes. Done items stay, ticked, with their commit hash.
- **`LOG.md`** — append-only history: timestamp · item · what changed · commit · how verified · any
  assumptions/deviations. Never rewrite; only append.
- **`README.md`** — this runbook.

## Execution model — thin orchestrator, one subagent per item

The loop session is a **thin orchestrator**. It must **not** implement items in its own context (that
exhausts context over a long run and degrades quality). Per iteration:

1. Read `STATUS.md` → current item + attempt count. Read the item's spec in `QUEUE.md`.
2. **Dispatch a fresh implementer subagent** (Agent tool) to do *just that item*: make the change,
   run the gate locally, and report back a concise result — files changed, gate results, a
   **confidence** (high/med/low), and any assumptions it had to make. Give it the doc-style guardrails
   (below) for doc items.
3. **Escalate to review only if warranted** (see Escalation ladder). Skip review otherwise — don't burn
   tokens reviewing a trivial README.
4. **Orchestrator re-runs the authoritative gate itself** (`csharpier` + `build`, plus `dotnet test` for
   code items) — never commit on the subagent's word alone.
5. Green → **commit** (atomic, single item, `Co-Authored-By` trailer) → update `STATUS.md`, append
   `LOG.md`, tick `QUEUE.md` → reset attempt count → next item.
6. Red → the **Failure protocol**.

## The Gate (authoritative check the orchestrator runs before every commit)

Always: `dotnet csharpier format .` → `dotnet build Atrium.slnx -v q` = **0 warnings, 0 errors**.

**The overnight gate is deterministic only** — the agent does *not* drive the browser or the live stack
unattended (that's fragile and wastes the night). All live verification is deferred to the supervised
morning pass (see `QUEUE.md`).

- **Code items** (`.cs`/`.razor`/config affecting runtime): `dotnet test Atrium.slnx` — all unit +
  integration green (**integration needs Docker running**; the aspire stack is *not* required). Then
  commit with a **"live-verification deferred to morning"** line in the body + `LOG.md` naming what to
  check live (e.g. item 4 → `/openapi/v1.json` + Redoc render; item 6 → traces/logs emit).
- **Docs items** (Markdown only): nothing to test — substitute **build-clean + accuracy check**: confirm
  every path, command, class, and route the doc names actually exists in the code.

## Escalation ladder (elevation) — spend effort in proportion to risk

Default is cheap; escalate only on a trigger.

- **Tier 0 (default):** implementer subagent + the orchestrator's authoritative gate. No separate
  reviewer. This is the common path for low-risk docs and mechanical changes.
- **Tier 1 (adversarial review):** dispatch a **separate reviewer subagent** *only* when any trigger
  fires — (a) the item touches **auth/security/tokens/roles**; (b) it's a **code** item with a runtime
  surface (items 4, 6); (c) the implementer reported **low confidence** or a material assumption; (d) the
  item **failed a prior attempt**. The reviewer adversarially checks the work (docs: grep the code for
  every claim; code: review the diff + gate). Real findings → one repair attempt.
- **Tier 2 (backoff):** if it still fails after the repair attempt → Failure protocol. Don't keep
  hammering.

On **retry**, elevate: give attempt 2 more care / higher reasoning effort than attempt 1.

## Failure protocol (never stall, never leave a red tree)

- **Max 2 attempts per item.** If attempt 2 still fails the gate: **revert this item's working-tree
  changes** (`git checkout -- .` / `git reset --hard HEAD` — nothing for this item is committed yet) so
  the tree returns to the last green commit, mark the item **`BLOCKED: <reason>`** in `STATUS.md` +
  `QUEUE.md`, append to `LOG.md`, and **skip to the next item**. A blocked item must never leave a broken
  build/test for the next item to inherit.
- **Circuit breaker:** if **2 consecutive items** go BLOCKED, **halt the run** and write a prominent
  `⚠ RUN HALTED` note at the top of `STATUS.md` with the pattern — something systemic is likely wrong
  (build env, Docker, stack). Don't churn the rest of the queue failing.

## How to resume (a fresh session starts here)

1. **Branch.** Ensure you're on the run branch: `git switch overnight/2026-07-01` (create with
   `git switch -c overnight/2026-07-01` off `main` if it doesn't exist). Autonomous commits go **only**
   here; `main` stays pristine for the user to review/merge in the morning.
2. **Resume hygiene.** Run `git status` — if the tree is **dirty**, a prior loop died mid-item: decide
   finish-or-reset (default: `git reset --hard HEAD` and redo the item). Then run `dotnet build` +
   `dotnet test` to confirm a **green baseline**; record the start commit in `LOG.md`. If the baseline is
   already red, that's a circuit-breaker halt.
3. **Start a self-paced `/loop`** so the cadence survives usage-limit pauses; use `ScheduleWakeup` to
   continue. (Skip for a single supervised pass.)
4. Run the **Execution model** loop over `QUEUE.md` in order.
5. Stop when the queue is drained, the circuit breaker trips, or capacity runs out. Leave `STATUS.md`
   pointing cleanly at the next action.

## Autonomy boundary (current) — run the whole queue, on the branch, revertible

Run the **entire** queue in order, unattended, committing each item **to `overnight/2026-07-01`**. No
stop-before-code pause. Safety net = git hygiene:

1. **One atomic, single-purpose commit per item** — never bundle two items.
2. **Docs before code** (10 → 2 → 3 → 1, then code). After the last docs item (1) commits, write its hash
   into `LOG.md` + `STATUS.md` as `SAFE-REVERT-POINT` before touching any code item.
3. **Item 4** = built-in OpenAPI + Redoc (decided). **Item 6** = OTel + Serilog. Both are code → Tier-1
   review applies.
4. The **Dialog "cute"/spacing polish** is subjective — a best-effort `frontend-design` pass is fine
   (reversible CSS), but flag it in `LOG.md` for the user's eye; don't mark it "done."
5. **Use the project skills** the same way an interactive session would: `atrium-ui` + `frontend-design`
   for any UI, `systematic-debugging` when the gate goes red, `context7` for framework/library specifics.

## Cadence & limits (honest)

- No way to poll a numeric usage quota — the durable files are the real safety net, so keep them current
  after *every* step, not just at commit time.
- **Docker must be running** (integration tests use Testcontainers). Verify early; if Docker is down,
  integration tests can't run — commit code items unit-verified with a note, or treat repeated failures
  as a circuit-breaker halt.
- The **aspire stack is not required overnight** — no unattended `aspire run` / Playwright. Live checks
  (browser + traces) are the morning supervised pass's job, not the loop's.

## Relationship to other docs

`docs/HANDOFF.md` is the durable "state of the project" note. This folder is the **live work queue** on
top of it. When the queue drains, fold anything lasting into `HANDOFF.md` and let this folder go quiet.
