# Atrium.Design

## What it is
The shared design-system Razor Class Library: design tokens, the base stylesheet, UI primitives, and the cross-cutting helpers every module and the portal shell use — access-token carriage, HTTP client extensions, money formatting, and the AG-UI chat surface.

## Role in the topology
**Design system.** Referenced by `Atrium.Portal` and every `Atrium.Modules.*` module. Also references `Atrium.Contracts` (for `FeedbackDto`, the one shared wire type the feedback client and the Storefront endpoint both compile against).

## Key types
- **Primitives:** `Button`, `Badge`, `PageHeader`, `Field`, `Notice`, `Dialog` (native `<dialog>`), `ToastHost`, `AgentChat` (the AG-UI chat surface with thumbs feedback).
- **Token carriage:** `AccessTokenHolder` — scoped carrier for the signed-in user's bearer token, populated by `MainLayout`.
- **HTTP helpers (`HttpClientExtensions.cs`):** `TypedClientSendExtensions.SendForJsonAsync` — the one mandated request pipeline for module typed clients (authorize → send → log → `ThrowIfSessionExpired` → `EnsureSuccessStatusCode` → deserialize); `HttpRequestAuthorizationExtensions.Authorize`; `HttpResponseLoggingExtensions.LogIfUnsuccessful`.
- **AG-UI chat plumbing:** `BearerTokenHandler` — a `DelegatingHandler` composed in circuit scope so it reads the live `AccessTokenHolder` (not the factory scope — ADR-0011); `AgentChatClientFactory` / `IAgentChatClientFactory` — builds the `AGUIChatClient` with the circuit-scoped bearer; `FeedbackClient` / `IFeedbackClient` — posts thumbs feedback to the gateway endpoint; `AgentChatServiceCollectionExtensions.AddAgentChat` — registers all three.
- **`Money.cs`** — canonical `$1,234.56` formatter (en-US, always with cents) shared across modules.
- **`Toasts` / `Enums.cs`** — toast service and shared variant enums; `SessionExpiredException` — thrown on a 401 for graceful re-login.
- **`wwwroot/css`** (`tokens.css`, `atrium.css`) + `wwwroot/js`.

## Run / test
Not run standalone; it loads as an RCL inside the Portal via `cd src/Atrium.AppHost && aspire run`. `SessionExpiredException` and `BearerTokenHandler` behavior are covered by `tests/Atrium.UnitTests`.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Solution layout" and the token-propagation flow.
- [ADR-0004](../../docs/adr/0004-token-propagation-and-option-b.md) — token-in-claim (module typed clients).
- [ADR-0008](../../docs/adr/0008-graceful-session-expiry-handling.md) — session-expiry handling.
- [ADR-0010](../../docs/adr/0010-native-dialog-primitive.md) — native `<dialog>` primitive.
- [ADR-0011](../../docs/adr/0011-circuit-scoped-bearer-handler.md) — circuit-scoped bearer for AG-UI.
