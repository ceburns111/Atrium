# STATUS — read me first

**Updated:** 2026-07-01 (overnight run resumed; baseline green at `335b2a2`).

## Where we are

Run resumed on branch `overnight/2026-07-01`. Baseline green (build 0W/0E, tests 22/22, Docker up).
**ALL DOCS DONE** (10/2/3/1). **CODE phase:** #4 OpenAPI+Redoc (`bc7afd7`) + #6 OTel/Serilog (`d51a902`)
DONE — both Tier-1 APPROVE WITH NOTES, live-check deferred. Original auto-run queue **DRAINED**.

**Continuing unsupervised (user said keep going with anything confidently doable, 2026-07-01).** Next:
**Item #7 (application logging)** — user-added; the repo has ~zero deliberate logging. Deterministic,
build+test-verifiable, no live stack → confidently doable. Tier-1 applies (touches client/auth-adjacent
paths + broad surface). Then attempt the low/tomorrow items I can do confidently (csharpier fluent-chain
FEASIBILITY check — likely not a supported option; a static UI ungraceful-scenario AUDIT as a report). The
`testuser`→customer-login item needs a Keycloak realm change + volume reset + live verify → SUPERVISED,
leave for morning. Dialog aesthetic polish is subjective + needs the user's eye → SUPERVISED, leave.

When out of confidently-doable work: write a prominent **root-level wake-up summary doc** (user request)
and stop cleanly, STATUS pointing at the supervised remainder.

### Skill-authoring capability (NEW, 2026-07-01)
User authorized unattended skill authoring under `.claude/skills/**` — grant in `.claude/settings.local.json`
(`autoMode.allow`, scoped to skills only; gitignored). Every auto-authored skill MUST be logged in
`docs/progress/SKILL-REVIEW.md` for the user's morning keep/discard review. The grant does NOT extend to
`settings.json`, hooks, or other `.claude` config — those stay supervised.

## Next concrete action

1. `git switch -c overnight/2026-07-01` (off `main`) — all autonomous commits go here, not `main`.
2. Resume hygiene: `git status` clean? `dotnet build` + `dotnet test` green baseline? Docker up? Record
   the start commit in `LOG.md`.
3. Start item **10**: read `docs/adr/` (esp. 0004, 0007) + the four "done this session" commits, then
   add/update ADRs for graceful session-expiry handling, service-root route nesting, the native-`<dialog>`
   Dialog primitive, and the co-located repository interfaces. Keep `docs/adr/README.md` in sync. Gate =
   build-clean + accuracy check (docs). Commit, advance to item 2.

## Autonomy boundary

**Run the whole queue, unattended, don't stop at code.** One atomic commit per item; docs (10,2,3,1)
first, then code (6, then low/tomorrow). **After item 1 commits, record its hash here + in LOG as
`SAFE-REVERT-POINT` before starting any code** — that's the user's clean revert line. Skip item 4 (needs
their viewer pick). See `README.md` for the full rule.

`SAFE-REVERT-POINT (last docs commit):` **`a25c62f`** — all docs (items 10/2/3/1) are at or before this
commit. To discard the entire code phase and keep every doc, the user can `git reset --hard a25c62f` on
`overnight/2026-07-01`. Everything after this is code (items 4, 6, …).

## Stack / environment

- **Docker must be up** (integration tests use Testcontainers). The **aspire stack is NOT required**
  overnight — the loop runs deterministic gates only; all live/browser/trace checks are the supervised
  morning pass's job.
- Docs items need neither Docker nor the stack.
- Branch: `overnight/2026-07-01` (create on first resume). `main` stays pristine.

## Blockers

- None. Item 4 (API docs) is **decided** (2026-07-01): built-in OpenAPI + Redoc, per service, no
  Swashbuckle/Scalar — it runs in the code phase. Nothing is parked.

## Pending (supervised, for when the user is back)

- Browser-verify the session-expired panel (#1) + the Admin modal on a fresh build.
- `frontend-design` polish on the Dialog (user wants centered/cute/better-spaced).
- Item 6 (OTel/Serilog) and the low/tomorrow code items.

## How to resume

Say "resume the Atrium overnight run"; the agent reads this file, starts a `/loop`, and works `QUEUE.md`
in order under the gate in `README.md`.
