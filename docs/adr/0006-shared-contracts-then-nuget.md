# ADR-0006 — Contracts as a shared project now, versioned NuGet later

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 3–4

## Context

The wire DTOs (Product, Category, Order, Report — `Atrium.Contracts`) are shared by both sides of every
call: the services produce them, the UI modules and typed clients consume them. In a single repo we
could share them as a **project reference** or publish them as a **versioned package**. Since
everything currently lives in one solution and ships together, there's no version skew to manage —
producer and consumer are always built from the same commit.

## Decision

Keep contracts as a **shared project** (`Atrium.Contracts`) referenced directly, for now. Treat this as
a **deliberately temporary** arrangement tied to the modular-monolith stage, not a permanent choice.

## Consequences

- **Zero versioning overhead today.** One repo, one build; a DTO change compiles both sides at once and
  a breaking change can't slip past the compiler.
- **It only holds while everything ships together.** The moment a vertical is split into its own repo
  and deploy cadence (see [BEYOND-THE-DEMO.md](../BEYOND-THE-DEMO.md) item 3), a project reference stops
  working — the consumer can no longer build against the producer's source.
- **The migration is understood, not improvised.** At the polyrepo split, `Atrium.Contracts` (or a
  per-domain slice of it) becomes a **versioned NuGet package** published from its owning repo, with
  SemVer discipline: consumers pin a version and upgrade deliberately, so producer and consumer deploy
  independently. This is the standard "contracts as packages" pattern from the SCS playbook.
- **Guardrail worth keeping even now:** contracts stay DTO-only (no behavior, no service types), so the
  package they'll become has a small, stable surface.

## Alternatives rejected

- **Versioned NuGet from day one** — real version-skew management (publish, pin, upgrade) with no
  payoff while everything is one build. Premature.
- **No shared contracts; each side defines its own DTOs** — invites silent drift between producer and
  consumer shapes; the compiler-checked single definition is strictly safer.
- **Sharing service/domain types, not just DTOs** — leaks internals across the boundary and makes the
  eventual package fat and unstable.
