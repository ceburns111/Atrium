# Atrium.Design

## What it is
The shared design-system Razor Class Library: MudBlazor components wired to the shared `AtriumTheme` (`MudTheme`), the domain-specific `ProductThumb` component, and the cross-cutting helpers every module and the portal shell use — access-token carriage, HTTP client extensions, and money formatting.

## Role in the topology
**Design system.** Referenced by `Atrium.Portal` and every `Atrium.Modules.*` module.

## Key types
- **`AtriumTheme`** — the shared `MudTheme` carrying brand identity (teal accent, neutral ramp, status colors, Roboto/Mono typography, 8px spacing scale) in `PaletteLight` and `PaletteDark` blocks. Single source of truth for all colors and spacing. ([ADR-0014](../../docs/adr/0014-adopt-mudblazor.md))
- **`ProductThumb`** — deterministic placeholder image with an `ImageUrl` param as the one-spot seam for real photos. The one custom visual component not covered by MudBlazor.
- **Token carriage:** `AccessTokenHolder` — scoped carrier for the signed-in user's bearer token, populated by `MainLayout`.
- **HTTP helpers (`HttpClientExtensions.cs`):** `TypedClientSendExtensions.SendForJsonAsync` — the one mandated request pipeline for module typed clients (authorize → send → log → `ThrowIfSessionExpired` → `EnsureSuccessStatusCode` → deserialize); `HttpRequestAuthorizationExtensions.Authorize`; `HttpResponseLoggingExtensions.LogIfUnsuccessful`.
- **`Money.cs`** — canonical `$1,234.56` formatter (en-US, always with cents) shared across modules.
- **`SessionExpiredException`** — thrown on a 401 for graceful re-login.

## Run / test
Not run standalone; it loads as an RCL inside the Portal via `cd src/Atrium.AppHost && aspire run`. `SessionExpiredException` behavior is covered by `tests/Atrium.UnitTests`.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Solution layout" and the token-propagation flow.
- [ADR-0004](../../docs/adr/0004-token-propagation-and-option-b.md) — token-in-claim (module typed clients).
- [ADR-0008](../../docs/adr/0008-graceful-session-expiry-handling.md) — session-expiry handling.
- [ADR-0014](../../docs/adr/0014-adopt-mudblazor.md) — MudBlazor adoption; supersedes ADR-0010.
