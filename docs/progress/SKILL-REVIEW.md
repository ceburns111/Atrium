# Skill review ledger — keep or discard

Autonomous overnight runs may author skills under `.claude/skills/` (a user-authorized capability,
granted 2026-07-01 via `.claude/settings.local.json`, scoped to `.claude/skills/**` only). Because a
skill's `description` decides when it auto-triggers and its body becomes standing instructions in every
future session, **each auto-authored skill is a draft until you review it here.** This ledger is your
morning approval gate.

## The morning ritual

1. **See exactly what the run added/changed** under `.claude/skills/` on the run branch vs `main`:

   ```sh
   git diff --stat main...overnight/2026-07-01 -- .claude/skills/
   git diff        main...overnight/2026-07-01 -- .claude/skills/     # full content
   ```

2. For each row in the table below, open the skill, read its **`description:`** (the trigger — the part
   most likely to misfire) and its body, then decide **Keep** or **Discard**.
3. **Keep** → set Status to `kept`, done. **Discard** → run the remove command in that row, then set
   Status to `discarded`.
4. Anything you're unsure about → leave `pending` and it stays a draft (skills still load, so discard if
   a bad trigger would be disruptive in the meantime).

To discard a skill:

```sh
git rm -r .claude/skills/<name>          # if already committed on the branch
# or, if uncommitted: rm -r .claude/skills/<name>
```

## Ledger

| # | Skill (`.claude/skills/<name>`) | Trigger (`description:` gist) | Authored | Commit | Status |
|---|---------------------------------|-------------------------------|----------|--------|--------|
| 1 | `atrium-service` | Building/editing an `Atrium.Services.*` backend service — feature folders, `Map*Endpoints`+tags+auth, Dapper/sprocs/DbUp/Mapperly, co-located repo iface + integration test. Triggers on any `.cs` under `src/Atrium.Services.*`. | 2026-07-01 (item 3) | `f30e9ae` | kept |
| 2 | `atrium-module` | Building/editing an `Atrium.Modules.*` UI module — `IModule`, typed client w/ token attach + `ThrowIfSessionExpired`, auto `@page` routes. Defers visuals to `atrium-ui`. Triggers on `.cs`/`.razor` under `src/Atrium.Modules.*`. | 2026-07-01 (item 3) | `f30e9ae` | kept |
| 3 | `atrium-contracts` | Adding/editing shared DTOs in `src/Atrium.Contracts` — DTO-only sealed records, breaking change fails both sides. Triggers on `.cs` under `src/Atrium.Contracts`. | 2026-07-01 (item 3) | `f30e9ae` | kept |

### Review 2026-07-01 (morning gate)

All 3 run-authored skills verified against source and **kept**. Every referenced ADR (0001–0009),
the guide (`docs/guides/wire-up-a-new-app.md`), and every cited file exist; spot-checked claims match
reality (repo iface co-located `CatalogRepository.cs:10`; `ProductDto` signature verbatim;
`ThrowIfSessionExpired()` precedes `EnsureSuccessStatusCode()` `CatalogClient.cs:49-50`). Triggers are
path-scoped and won't misfire. One precision fix applied to `atrium-service`: the role-claim gotcha now
points at `Atrium.Services.Catalog/Program.cs` (the real role-gated example) rather than implying
Storefront role-gates. Note: `atrium-ui` exists on disk but is **not** run-authored (committed Phase 0,
`25eb940`, already on `main`) — out of scope for this ledger.

## Notes

- The grant is **skills-only**. The run cannot modify `settings.json`, hooks, or other `.claude/` config
  unattended — those remain supervised. See `.claude/settings.local.json` (`autoMode.allow`).
- `.claude/settings.local.json` is gitignored (personal, machine-local) — it does not travel to a
  teammate's checkout, so the grant is yours alone.
