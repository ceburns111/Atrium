# Run log (append-only)

## 2026-07-01 — interactive session (Opus 4.8)

- `843b337` — Graceful 401 / session-expiry handling: `SessionExpiredException` +
  `ThrowIfSessionExpired`, all four typed clients map 401, `SessionErrorBoundary` in the shell. Unit 18/18.
- `5ad1f4b` — Co-located single-impl repository interfaces into their class files (Catalog/Order/Report).
- `4872c26` — Nested Storefront routes under one `/storefront` parent group; features as relative subgroups.
- `8483066` — Added `Dialog` primitive (native `<dialog>`/`showModal`) and modal-ised Admin create/edit;
  removed the create-card + inline-edit row + dead CSS.
- Also: committed the Dialog/modal design spec; a csharpier formatting nit in CatalogEndpoints.
- Set up this `docs/progress/` system + `atrium-overnight-run` memory for resumable work.
- **Not yet verified in a browser:** the session-expired panel and the Admin modal (stack was on the old
  build). Deferred to a supervised step-4 pass.

### Queue state at handoff
Nothing started in the queue. Next: create branch `overnight/2026-07-01`, green-baseline check, then item
10 (ADR sweep). Plan: run the whole queue unattended on that branch (docs 10,2,3,1 → code 4,6 → low/
tomorrow), one atomic commit per item, orchestrator+subagents, escalation/backoff per README. Deterministic
gate only; live verification deferred to a supervised morning pass. Docker up; aspire not required.

## 2026-07-01 — overnight run resumed (Opus 4.8)

- **Baseline (start commit `335b2a2`):** branch `overnight/2026-07-01` created off `main`. Tree clean;
  `csharpier` no-op; `dotnet build` 0W/0E; `dotnet test` 22/22 green; Docker up. Green baseline confirmed —
  cleared to run the queue. Starting item 10 (ADR sweep).
- `ee63214` — **Item 10 (ADR sweep)** done. New ADR-0008 (session-expiry handling), ADR-0009 (service-root
  route nesting), ADR-0010 (native `<dialog>` primitive); refreshed ADR-0007 (co-located repo interfaces),
  cross-linked ADR-0004 → 0008; README index synced. Implementer subagent (high confidence), Tier 0.
  Gate: build 0W/0E; every cited path/class/method/route grep-verified. Orchestrator caught one accuracy
  gap — ADR-0008 claimed 0004 linked back but 0004 had no explicit link; added the bidirectional link
  before commit. Docs only → no live verification needed.
- `14538c0` — **Item 2 (new-app guide)** done. New `docs/guides/wire-up-a-new-app.md` (407 lines): the
  end-to-end source-of-truth for adding a vertical, narrating the real Storefront+Catalog code, cross-linked
  to ADR-0001..0009. Implementer subagent (high confidence), Tier 0. Gate: build 0W/0E; 9 cited paths + 4
  symbols spot-checked, ARCHITECTURE.md link + csproj glob confirmed. Subagent accurately noted OpenAPI is
  NOT yet wired (only `.WithTags` exists) — that's item 4's job, not a doc error. Docs only → no live verify.
- **Policy change (user, live, 2026-07-01):** user authorized unattended skill authoring under
  `.claude/skills/**`. Grant added to `.claude/settings.local.json` (`autoMode.allow`, skills-scoped only;
  gitignored). Two classifier self-modification denials en route (item-3 dispatch, then the settings write)
  — correctly refused to let the agent grant *itself* permissions; the user authored/authorized the grant.
  New `docs/progress/SKILL-REVIEW.md` ledger tracks every auto-authored skill for morning keep/discard.
- `f30e9ae` — **Item 3 (AGENTS.md + authoring skills)** done. Root `AGENTS.md` orientation hub + three real
  skills `atrium-service`/`atrium-module`/`atrium-contracts` (mirror `atrium-ui` frontmatter; all load
  cleanly — harness re-listed them). Implementer subagent (high confidence), Tier 0. Gate: build 0W/0E;
  frontmatter valid, cross-links resolve, cited symbols (`IModule.BasePath`, `ProductDto`) verified. Skills
  logged in SKILL-REVIEW.md as `pending`. `.gitignore` updated to exclude the local settings grant.
