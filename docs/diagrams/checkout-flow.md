# Checkout UI flow (end-to-end)

The real storefront journey as of the checkout work: anonymous browse → cart → sign-in gate →
checkout → **simulated** payment → order placement → confirmation. Every route, guard, and service
below exists in code (`src/Atrium.Modules.Storefront/` and `src/Atrium.Services.Storefront/`).

What makes this accurate rather than aspirational:

- **Browsing is anonymous.** `Shop.razor` (`@page "/storefront"`) calls `CatalogClient`, whose
  `Authorize(tokens)` attaches a bearer **only if one exists**; the Catalog reads are
  `.AllowAnonymous()`, so a signed-out visitor sees products.
- **The cart survives sign-in.** `CartService` is circuit-scoped; `CartPersistence` mirrors a minimal
  `{ProductId, Quantity}` snapshot to `localStorage` (`wwwroot/js/cart-storage.js`) on every mutation
  and re-hydrates (re-pricing from the live catalog) on the first interactive render.
- **The gate is `<AuthorizeView>` in `CartPage.razor`** (`@page "/storefront/cart"`): anonymous users
  get a "Sign in to check out" `Notice` linking to `/account/login?returnUrl=%2Fstorefront%2Fcart`;
  signed-in users get "Proceed to checkout" → `/storefront/checkout`.
- **Checkout is `[Authorize]`** (`Checkout.razor`, `@page "/storefront/checkout"`).
- **Payment is simulated.** `PaymentService.AuthorizeAsync` does a `Task.Delay(600)` and returns
  approve/decline — no gateway, network, or SDK. A card number ending in `0002` (`DeclineSuffix`) is
  declined; anything else is approved with an `AUTH-…` reference.
- **On approval**, `OrdersClient.CreateAsync` does `POST /storefront/orders` (JWT-gated); the service
  **re-prices every line from Catalog** (`OrderPricing`) and never trusts client prices. A per-attempt
  idempotency key makes retries safe: on a same-user replay the service re-reads and returns the already-committed
  order; a cross-user key collision returns **409 Conflict** rather than leaking another user's order.

```mermaid
flowchart TD
    Browse["Browse /storefront<br/>Shop.razor · anonymous catalog GET"]
    Add["Add to cart<br/>CartService.Add → persisted to localStorage"]
    Cart["/storefront/cart<br/>CartPage.razor"]
    Gate{"AuthorizeView<br/>signed in?"}
    SignIn["Notice: 'Sign in to check out'<br/>→ /account/login?returnUrl=%2Fstorefront%2Fcart"]
    OIDC["OIDC round-trip (Keycloak)"]
    Rehydrate["Back at cart<br/>rehydrated from localStorage"]
    Checkout["/storefront/checkout<br/>Checkout.razor · [Authorize]"]
    PayForm["Payment form (card fields, component state only)"]
    Pay{"PaymentService.AuthorizeAsync<br/>SIMULATED"}
    Decline["Declined (card ends 0002)<br/>Notice: 'Payment declined' — retry"]
    Order["POST /storefront/orders<br/>OrdersClient · JWT-gated"]
    Reprice["Storefront re-prices from Catalog<br/>OrderPricing (client prices ignored)"]
    Confirm["Confirmation<br/>order #, total, AUTH-… reference"]

    Browse --> Add --> Cart --> Gate
    Gate -- "anonymous" --> SignIn --> OIDC --> Rehydrate --> Gate
    Gate -- "authorized" --> Checkout --> PayForm --> Pay
    Pay -- "declined" --> Decline --> PayForm
    Pay -- "approved" --> Order --> Reprice --> Confirm
```

> Payment is a mock authorizer — there is **no** external payment gateway. See
> `Checkout/PaymentService.cs`. Order writes go through the YARP gateway (pass-through) to the
> Storefront vertical, which validates the JWT; route nesting under `/storefront` follows
> [ADR-0009](../adr/0009-service-root-route-nesting.md).
