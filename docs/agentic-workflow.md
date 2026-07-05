# How Atrium was built: a disciplined agentic workflow

> **Thesis:** an LLM writes most of the code in this repo. That is not the interesting part — anyone can
> get an LLM to emit code. The interesting part is the **harness around it**: an independent gate,
> adversarial review, revertible per-item commits, and automated browser verification. *AI writes the
> code; the harness is what makes the output trustworthy.*

This document explains that harness — how a feature actually lands in Atrium, why each layer exists, and
the evidence that it catches real defects. It's written for someone evaluating how I work, not just what I
shipped.

## The problem it solves

The failure mode of coding with an LLM isn't gibberish — it's **confident, plausible, subtly-wrong code**,
delivered with the same tone as correct code. A subagent will tell you "gate green, 0 warnings, done" while
having quietly added a vulnerable dependency. It will reason that a cart "survives sign-in" without ever
loading the page. Trust can't come from the model's own say-so. It has to come from **structure the model
can't talk its way around**.

So the rule is simple: **nothing is trusted because an agent asserted it.** Every claim is re-checked by
something that doesn't share the agent's incentives — a gate the orchestrator runs itself, a skeptic
subagent, a browser, a human at the end.

## The harness, in layers

### 1. Encoded knowledge — the agent starts from house rules, not a blank page
- **Custom skills** (`.claude/skills/atrium-*`) encode this codebase's real patterns as guardrails an
  implementer *must* follow: how a service wires auth, how a module's typed client handles token + session
  expiry, the design-system tokens, and — after a real bug — *never hard-code `#fff` on a themed fill*.
  When a run teaches us something (see the dark-mode bug below), it goes back into a skill so it's never
  re-learned.
- **`context7` MCP** gives the agent current library docs on demand, so API usage is checked against
  reality instead of hallucinated from training data.

### 2. Orchestration — a thin orchestrator, one subagent per item
The main loop is deliberately **thin**: it plans a queue, then dispatches a **fresh implementer subagent
per item** so no single context accumulates the whole run (which degrades quality over a long session). The
orchestrator's own context stays small — it holds the *conclusions*, not the file dumps.

On risky items (anything touching **auth, tokens, roles, money, or a runtime surface**) it escalates to an
**adversarial reviewer** subagent whose job is to *refute* the work — verify the security claims against the
code, not nod along.

### 3. The independent gate — the orchestrator re-runs it *itself*
This is the load-bearing rule. Before **every** commit, the orchestrator runs the authoritative gate in its
own hands — `dotnet csharpier` + `dotnet build` (0 warnings, 0 errors) + `dotnet test` — and **never commits
on the subagent's word**. A subagent reporting "green" is a hypothesis; the orchestrator's own run is the
test.

### 4. Revertible state — atomic commits + a resumable paper trail
- **One atomic, single-purpose commit per item**, so any item can be reverted in isolation, plus a
  **SAFE-REVERT-POINT** marking the end of the low-risk phase (`git reset --hard` there drops the whole
  risky phase and keeps the safe work).
- The run's state lives in **git-tracked files** (`docs/archive/runs/STATUS.md` / `QUEUE.md` / `LOG.md`), not in
  chat. That's what makes a run **resumable across context clears / machines / days**: a cold session reads
  `STATUS.md` and picks up deterministically. Subjective/asset work (dark mode, images) is committed but
  flagged `[~]` — *never* declared "done" unattended.

### 5. Live verification — a browser closes the loop
The deterministic gate can't see what a human sees. So a run ends with a **Playwright-MCP smoke** against
the running stack — the agent drives the real app (anonymous browse → sign-in → checkout → payment; role
gating; dark mode) and captures screenshots as evidence. See
[`docs/archive/runs/verification/`](archive/runs/verification/). This is the layer that catches the
class of bug tests structurally miss.

## Evidence it works (real moments from the runs)

Not hypotheticals — these happened, and are in `docs/archive/runs/LOG.md`:

- **The gate caught a vulnerability a subagent waved off.** An implementer added a test dependency that
  transitively pulled a **vulnerable `Microsoft.OpenApi` 2.0.0** (NU1903), taking the build from 0 → 2
  warnings — and reported it as "pre-existing." The orchestrator's own gate run said otherwise; root-caused
  it (the vuln rode in on `Mvc.Testing`), pinned the patched version, back to 0 warnings. *The subagent's
  word would have shipped a known-vulnerable dependency.*
- **Adversarial review caught a broken promise.** The spec said "the cart survives sign-in." The reviewer
  reasoned that a per-circuit scoped cart empties across a full-page OIDC redirect — a real UX gap the
  implementer had glossed. It was repaired (localStorage persistence + hydrate on checkout entry) **before**
  the commit, then **proven live** (verification step 4, screenshot 04).
- **A visual bug became a permanent guardrail.** Dark mode shipped with a Save button that was white text on
  a near-white fill — invisible. The fix (theme-flipping `--paper` / a new `--on-accent` token) went into
  the code *and* into the `atrium-ui` skill as a rule, so the next agent can't reintroduce it. The fix is
  verified live in verification step 10 (screenshot 10).
- **The whole feature set, verified in a browser.** The end-of-run smoke drove all seven items of the
  latest run through a real browser and screenshotted each — anonymous browsing, the sign-in gate, cart
  survival, payment decline/approve (a real order placed), role-gated cards, dark mode. Ten screenshots,
  every step green.

## One item, end to end

```
plan → dispatch implementer subagent (uses skills + context7)
     → [risky?] dispatch adversarial reviewer → apply one repair
     → orchestrator RE-RUNS the gate itself (csharpier + build + test)
     → green? atomic commit + tick QUEUE/STATUS/LOG   red? revert-to-green, mark BLOCKED, move on
→ (end of run) Playwright-MCP smoke on the live stack → screenshots
```

Each arrow is a place the model's self-assessment is replaced by an independent check. That's the whole idea.

## Honest limits & next steps

A portfolio piece that only lists wins is a sales pitch. The real state:

- **Live verification is a prototype.** The agent drives the Playwright MCP through a
  [playbook](runs/verification/README.md) by hand, **end-of-run**, not as a headless self-asserting suite
  in CI and not per-item. Hardening it — scripted assertions, dynamic Aspire endpoint discovery, wire it
  into CI — is the clear next step.
- **The gate is deterministic-only unattended.** Bringing the full Aspire stack up for every item would be
  slow and flaky, so live checks are batched to the end. That's a deliberate trade, not an oversight.
- **Best-effort items still need a human eye.** Dark mode and imagery were shipped flagged `[~]`; a person
  confirmed the look. The harness is honest about what it *can't* self-verify.

## Where to look

- The run system + runbook: [`docs/runs/`](runs/) (`README.md` = the loop; `STATUS/QUEUE/LOG` = state).
- The live smoke + evidence: [`docs/runs/verification/`](runs/verification/).
- The encoded house rules: [`.claude/skills/atrium-*`](../.claude/skills/).
- The architecture the harness builds against: [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) +
  [`docs/diagrams/`](diagrams/).
