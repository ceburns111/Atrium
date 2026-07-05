# Live verification — end-of-run smoke

The autonomous run's gate is **deterministic** (csharpier + build + test) and never drives a browser. This
folder is the other half: a **live smoke** an agent drives through the **Playwright MCP** against the
running Aspire stack, once, at the end of a run — the step that used to be a manual click-through.

It exists because unit/integration tests can't see what a human sees: an invisible button in dark mode, a
cart that silently empties across a full-page OIDC login, a role-gated card that leaks to the wrong user.
Those are exactly the bugs this smoke is built to catch.

## How to run it

1. Bring the stack up: `cd src/Atrium.AppHost && aspire run` (needs Docker). Wait until the portal answers
   at `https://localhost:7001` and Keycloak's realm is imported.
2. Drive the playbook below via the Playwright MCP (the browser tolerates the dev self-signed cert; the
   `atrium-portal` client's redirect URIs are `*` in dev, so OIDC round-trips work). Logins:
   `testuser`/`password` (customer) and `admin`/`password` (admin) — from the realm export.
3. Capture a screenshot at each ✅ step into `verification/<date>/`.

## Playbook (steps · expected result)

| # | Step | Expected |
|---|------|----------|
| 1 | Anonymous → `/` | Home shows **only** the Storefront card; a "Sign in" link; dark-mode toggle present. |
| 2 | Anonymous → `/storefront` | Catalog loads **without** login (category filters + product grid); products show `ProductThumb` placeholders. |
| 3 | Add an item → `/storefront/cart` | Cart lists the item + total, and shows a **"Sign in to check out"** notice (no checkout button) with `returnUrl=%2Fstorefront%2Fcart`. |
| 4 | Click Sign in → log in as `testuser` | Returns to the cart; **the item is still there** (survived the full-page OIDC round-trip); the gate is now a **"Proceed to checkout"** button. |
| 5 | Proceed to checkout | `/storefront/checkout` shows order summary + the simulated-payment card form. |
| 6 | Pay with `4000 0000 0000 0002` | **"Payment declined"**; form retained for retry; **no order placed**. |
| 7 | Pay with `4242 4242 4242 4242` | **"Order confirmed"** with a real order # + total + `AUTH-…` reference. |
| 8 | Toggle dark mode | Palette flips; the toggle choice persists (localStorage). |
| 9 | Sign out; log in as `admin` → `/` | Home shows **all three** cards (Admin / Storefront / Reports). |
| 10 | `/admin` → Edit a product | Modal opens; in dark mode the **Save** button label is legible (regression guard for the dark-mode contrast fix). |

A run **passes** only if every row matches. Any mismatch is a finding — fix it and re-run, exactly like the
deterministic gate.

## Results

- **[2026-07-01](2026-07-01/)** — all 10 steps ✅ (Run 2, branch `feat/storefront-checkout-diagrams`).
  Notable: step 4 confirmed the cart-persistence repair, step 6/7 the payment decline/approve split (real
  order `#7002` placed), step 10 the dark-mode Save-button fix. Screenshots `01–10` in the dated folder.

## Honest limits (see `docs/agentic-workflow.md` → Next steps)

This is a **prototype**: the agent drives the Playwright MCP through the playbook by hand. It is **not** yet
a headless, self-asserting Playwright suite in CI, and it runs **end-of-run**, not per-item. Hardening it
(scripted assertions, dynamic Aspire endpoint discovery, run it in CI) is documented as the next step.
