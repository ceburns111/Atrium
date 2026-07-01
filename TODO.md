# TODO



## Clean Up 
- [x] Update project specific tools/skills — `7351b0a` (atrium-ui: real primitives/tokens + dark-mode
      guardrail; atrium-module: RequiredRole; atrium-service: AllowAnonymous public-read pattern)


## ✅ New work — DONE (Run 2, branch `feat/storefront-checkout-diagrams`, not yet merged)
- [x] Hide app cards on home the user can't access (testuser no longer sees Admin/Reports cards) — `afb89eb`
- [x] Storefront visible to anonymous; checkout prompts sign-in, then testuser signs in + checks out — `7ab96e5`
- [x] Payment form / basic full checkout process (simulated) — `da4abba`
- [x] Architecture diagrams + UI flows (mermaid) — `6c015d0`
- [x] _(bonus)_ Dark mode + toggle + button-contrast fix — `cbf4fb2` / `b332388`
- [x] _(bonus)_ Store image placeholders (`ProductThumb`, swap-in seam for real photos) — `3ab697a`
      → full write-up in `GOOD-MORNING.md`; blow-by-blow in `docs/progress/`. **Live click-through + merge still pending.**

## Remaining
- [x] Make sure existing docs are up to date — `a132740` (fixed the one staleness: ARCHITECTURE.md auth
      model now reflects anonymous catalog browsing). Doc inventory: 49 MD files, all else current.
- [ ] **Clean out junk MD docs / hide from git** — ⚠️ decision needed (see below). The only real "junk"
      candidate is the root screenshot (already gone). The `docs/progress/*` + `GOOD-MORNING.md` files
      *look* like clutter but are the **git-tracked state of the resumable-run workflow** — gitignoring
      them breaks resume-from-cold-session by design. Recommend KEEP (optionally move under `docs/runs/`).


## ❓ Open question
- [ ] Document my own agentic coding workflow (the overnight tool), or bring it up for discussion?
