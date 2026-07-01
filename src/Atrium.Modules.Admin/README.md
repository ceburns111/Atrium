# Atrium.Modules.Admin

## What it is
The back-office UI module: a products table with inline edit and create. Indigo accent. Writes are admin-gated server-side (the Catalog service enforces the `admin` policy).

## Role in the topology
**UI module.** A self-contained Razor Class Library the Portal discovers by reflection via `IModule`. Its client calls the **gateway** `/catalog` routes. References `Atrium.Abstractions`, `Atrium.Design`, and `Atrium.Contracts`.

## Key types
- `AdminModule` — the `IModule` implementation.
- `AdminCatalogClient` — typed client for reading/creating/updating products via the gateway; attaches the bearer token and calls `ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`.
- `Pages/Products.razor` — the products table with inline edit + create dialog.

## Run / test
Not run standalone; it loads in the Portal via `cd src/Atrium.AppHost && aspire run`. Product write authorization is enforced by the Catalog service and covered by `tests/Atrium.IntegrationTests/CatalogRepositoryTests.cs`.

## See also
- [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) — "Auth model" (admin-gated writes).
- [ADR-0003](../../docs/adr/0003-yarp-keycloak-auth.md) · [ADR-0010](../../docs/adr/0010-native-dialog-primitive.md).
- [docs/guides/wire-up-a-new-app.md](../../docs/guides/wire-up-a-new-app.md).
