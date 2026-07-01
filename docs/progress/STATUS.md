# STATUS — read me first

**Updated:** 2026-07-01 (end of interactive session, before user walked away / `/clear`).

## Where we are

The progress system is set up. **No queue item has been started yet.** The next item to execute is
**#10 (ADR sweep)** — the first of the auto-run docs batch (10 → 2 → 3 → 1).

## Next concrete action

Start item **10**: read `docs/adr/` (esp. 0004, 0007) and the four "done this session" commits, then
add/update ADRs for: graceful session-expiry handling, service-root route nesting, the native-`<dialog>`
Dialog primitive, and the co-located repository interfaces. Keep `docs/adr/README.md` in sync. Gate =
build-clean + accuracy self-review (docs item). Commit, then advance to item 2.

## Autonomy boundary

Auto-run + commit the **docs** items only (10, 2, 3, 1). **STOP before item 6** and all code/supervised
items. See `README.md`.

## Stack / environment

- An Aspire stack was running during the session but on the **pre-modal build** — a restart is required
  before any browser-verify. Not needed for the docs batch.
- Docs items need no stack and no Docker.

## Blockers

- None for the docs batch.
- Item 4 (API docs) is **parked pending a user decision** on Scalar vs alternatives (see `QUEUE.md`).

## Pending (supervised, for when the user is back)

- Browser-verify the session-expired panel (#1) + the Admin modal on a fresh build.
- `frontend-design` polish on the Dialog (user wants centered/cute/better-spaced).
- Item 6 (OTel/Serilog) and the low/tomorrow code items.

## How to resume

Say "resume the Atrium overnight run"; the agent reads this file, starts a `/loop`, and works `QUEUE.md`
in order under the gate in `README.md`.
