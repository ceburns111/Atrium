# Good morning — Run 3 (support chatbot) is done

**Branch `feat/support-chatbot`** (off `main`; `main` untouched). Queue drained, gate green throughout —
final **build 0W/0E, `dotnet test` 81/81**, csharpier clean. Nothing to babysit. Details:
`STATUS.md` (source of truth), `LOG.md` (per-item), `RUN3-SUPPORT-CHATBOT.md` (the spec).

## What you asked for → what shipped
A **customer-support chatbot** on the mandated **Microsoft Agent Framework + AG-UI** stack, with **step-up
MFA**, wired the same way every other Atrium capability is (service owns the runtime, module lights up a
surface, shell renders it):

- **`Atrium.Services.Storefront/Support/`** — a MAF **Order Support** agent with two real tools:
  `GetOrderStatus` (user-scoped — a new `usp_Order_GetById` filters by owner, so you can't read someone
  else's order) and `FindProduct` (via the Catalog client). Honest status only — no invented
  Shipped/Delivered (the store has no status column).
- **Config-driven model** (`IChatClient`): **Fake** (Dev default — boots with zero AI config), **Foundry
  Local** (dev), **Azure AI Foundry** (cloud). Provider swap = config only.
- **AG-UI SSE** endpoint at **`/storefront/agent`**, behind a config-driven **step-up-MFA** policy
  (`amr` for Entra / `acr` for Keycloak, dev-simulate). Existing gateway catch-all already proxies it.
- **`<AgentChat>`** primitive in `Atrium.Design` + an **assistant launcher** in the shell top bar that the
  Storefront module lights up via the new `IModule.AgentSurfaces` seam.

Plus the two smaller TODO items: **NavMenu now shows "N of M modules visible"** (was misleading for
customers/anon), and **MTP+xUnit was already done** → verified + ticked, dropped from the queue.
**Azure deploy stayed deferred** (supervised; agreed direction captured in the spec).

## Commits (A + C0–C5, atomic)
setup `2a995eb` · A `ff6c019` · C0 `aa01623` · C1 `1b99ff9` · C2a `3ba8300` · C2b `1437496` ·
C3 `873a8a2` · C4 `c91663f` · C5 `9ff317c`. C0 also cleared a newly-surfaced NU1903 advisory repo-wide.

## What's NOT verified (deliberately — deterministic gate only)
**C4 (`<AgentChat>`) and C5's launcher are `[~]` best-effort** — they compile and their logic is
unit-tested (bearer handler, step-up policy, surface declaration), but **live SSE streaming, rendering,
and the token-flow are unproven without a running circuit + model.** That's the supervised pass.

## ▶ Your move
1. **Live pass:** follow **[`verification/RUN3-support-chatbot.md`](verification/RUN3-support-chatbot.md)**.
   Start with check #2 (the Fake provider proves the whole transport + auth without a model); the **#1
   thing to confirm is that the streamed request carries your bearer**. Then wire Foundry Local for real
   replies, and flip `StepUp:Enabled` to exercise the gate.
2. **Try it:** `aspire run` → dashboard **https://atrium.dev.localhost:17250** → `portal` → sign in
   (`testuser`/`password`) → **"Order Support"** in the top bar.
3. **Review + merge** `feat/support-chatbot` → `main` when you're happy.

---

_(Prior wake-up summaries: Run 2 = storefront/checkout/diagrams, merged to `main` at `09b42b8`; Run 1 =
overnight docs/observability, merged. Their detail lives in `LOG.md`.)_
