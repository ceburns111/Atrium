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
Nothing started in the queue. Next: item 10 (ADR sweep). Autonomy boundary = docs only (10,2,3,1).
