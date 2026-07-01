# Atrium.Design

## What it is
The shared design-system Razor Class Library: design tokens, the base stylesheet, and a small set of UI primitives every screen builds from. It also carries the scoped `AccessTokenHolder` used to carry the signed-in user's bearer token into typed clients.

## Role in the topology
**Design system.** Referenced by `Atrium.Portal` and every `Atrium.Modules.*` module so the whole UI stays visually consistent from one place instead of each module reinventing colors, spacing, and controls.

## Key types
- Primitives: `Button`, `Card`, `Badge`, `PageHeader`, `Field`, `ToastHost`, `Dialog` (built on the native `<dialog>` element).
- `AccessTokenHolder` — scoped carrier for the access token (populated by the Portal's `MainLayout`).
- `Toasts` / `Enums.cs` — toast service and shared variant enums; `SessionExpiredException` — thrown on a 401 for graceful re-login.
- `wwwroot/css` (`tokens.css`, `atrium.css`) + `wwwroot/js`.

## Run / test
Not run standalone; it loads as an RCL inside the Portal via `cd src/Atrium.AppHost && aspire run`. `SessionExpiredException` behavior is covered by `tests/Atrium.UnitTests/SessionExpiredTests.cs`.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Solution layout" and the token-propagation flow.
- [ADR-0010](../../docs/adr/0010-native-dialog-primitive.md) — native `<dialog>` primitive.
- [ADR-0008](../../docs/adr/0008-graceful-session-expiry-handling.md) — session-expiry handling.
