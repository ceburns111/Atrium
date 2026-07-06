# Atrium — handoff / current state

**As of 2026-07-03.** This is the "where we are and how to pick up" note. For how the system fits
together read [ARCHITECTURE.md](ARCHITECTURE.md); for *why* each choice was made, the
[ADRs](adr/README.md) (0001–0013); for what was deliberately scoped out,
[BEYOND-THE-DEMO.md](BEYOND-THE-DEMO.md). Build history lives in git and
[docs/archive/](archive/) — not here.

## Where we are

- **Core platform** (portal + 3 modules + gateway + 2 services + Keycloak + Aspire) is complete and
  browser-verified. Rebuild of CozenDemo (still at `/Users/ted/code/CozenDemo`, reference only).
- **Full audit + remediation** (2026-07-03): a codebase-wide audit
  ([audits/2026-07-02-full-audit.md](audits/2026-07-02-full-audit.md)) found no criticals; all
  fix-disposition findings landed (user-scoped race-safe order idempotency, full-transcript fail-closed
  guardrail, host-boilerplate consolidation into `ServiceDefaults`, the known chat-dialog and
  focus-ring bugs, shared money/typed-client helpers). Gate at close: build 0 warnings / 0 errors,
  unit 97/97, integration 13/13.
- **Nothing is mid-flight.** Candidate next work: token-store option "B" (ADR-0004),
  BEYOND-THE-DEMO items, and the user's running list in `TODO.md`.

## How to run

**Full stack** (Docker required):

```bash
cd src/Atrium.AppHost && aspire run
```

Aspire assigns **dynamic ports each run** — find the Portal with
`lsof -iTCP -sTCP:LISTEN -P -n | grep Atrium.Po` and open `https://localhost:<portal-port>/`.
Keycloak is fixed at `https://localhost:8080`. **Login:** `testuser` / `password` (customer) or
`admin` / `password` (admin).

**Gate + tests** (from the repo root; CSharpier check runs on build, so format first):

```bash
dotnet csharpier format . && dotnet build Atrium.slnx -v q   # expect 0 warnings / 0 errors
dotnet test tests/Atrium.UnitTests                            # fast, no Docker
dotnet test tests/Atrium.IntegrationTests                     # real SQL Server via Testcontainers (Docker)
```

## Known limitations (deliberate for a demo)

- **No token refresh.** The access token is captured at login; after expiry (~5 min) a 401 maps to the
  typed `SessionExpiredException` and the shell shows a "session expired — sign in again" panel
  (ADR-0008). Expiry itself is unfixed; prod fix is `Duende.AccessTokenManagement` (ADR-0004 option B).
- **Token rides in the auth cookie** as a custom claim — the accepted demo shortcut, documented with
  its exit in ADR-0004.
- **Stale cookie across restarts** can 500 module pages: hit `/account/logout`, sign in again.
- **Realm changes need a volume reset.** `WithRealmImport` only creates; to re-import:
  `docker volume ls -q | grep keycloak | xargs docker volume rm` (Aspire stopped). Note the realm
  export was trimmed on 2026-07-03 (unused `atrium-catalog` client removed) — a volume wipe applies it.
- **Sign-out is a GET** (POST + antiforgery deliberately skipped; commented in Portal `Program.cs`).
## Gotchas that cost time (avoid re-hitting)

- **Role-based auth needs `MapInboundClaims = false`** — otherwise the inbound flat `role` claim is
  renamed and `RequireRole("admin")` 403s everyone, admins included. Now set once in
  `AddAtriumJwtAuth()` (`Atrium.ServiceDefaults`).
- A routable component whose class name equals an injected member triggers **CS0542** — hence
  `CartPage.razor` / `OrdersPage.razor`.
- `aspire run` uses dynamic ports; always re-discover via `lsof`. Keycloak stays on 8080.
- The cart is circuit-scoped: in browser tests, navigate via in-app links, not full page loads.
- Service stdout lands in the DCP `*_out` files under `$TMPDIR/aspire-dcp*/`, not `~/.aspire/logs`.
