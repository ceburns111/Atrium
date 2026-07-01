---
name: atrium-contracts
description: >-
  Use whenever adding or editing shared wire DTOs in src/Atrium.Contracts — the single project of
  DTO-only records that both a backend service (producer) and a UI module + its typed client (consumer)
  reference. Enforces the contract guardrails: keep it DTO-only with no behavior, use sealed record
  types matching the existing ProductDto / CreateProductRequest / UpdateProductRequest conventions, and
  remember a breaking change fails the build on both sides. Trigger this for "add a DTO", "add a
  request/response record", or any .cs work under src/Atrium.Contracts.
---

# Atrium contracts — shared-DTO guardrails

The always-loaded rules for the shared wire contracts. For where contracts sit in the end-to-end flow
follow **[docs/guides/wire-up-a-new-app.md](../../../docs/guides/wire-up-a-new-app.md) §2** — this skill
is the checklist; don't restate the guide.

`Atrium.Contracts` is a **single shared project** consumed by **both** the service that produces a
payload and the module + typed client that consumes it
([ADR-0006](../../../docs/adr/0006-shared-contracts-then-nuget.md)). Because both sides reference it, a
breaking DTO change fails the build on both — that's the intended safety net.

## Rules

- **DTO-only, no behavior.** No logic, no dependencies on service or module code, no data-access
  concerns — just the wire shape. Row types and mapping stay inside the service (Mapperly maps
  row → DTO); this project holds only the public contract.
- **Follow the existing conventions** as they actually appear in `src/Atrium.Contracts`:
  `namespace Atrium.Contracts;` (file-scoped), `public sealed record` types with positional parameters.
  Reference: `ProductDto.cs`
  (`public sealed record ProductDto(int Id, string Name, string Category, decimal Price, string Blurb)`)
  and `ProductContracts.cs` (`CreateProductRequest` / `UpdateProductRequest`). A create/update request
  omits the id (it comes from the route); the response DTO includes it.
- **One concern per file**, matching the current layout: a `*Dto.cs` for the read shape and a
  `*Contracts.cs` for the request records (see `ProductDto.cs` + `ProductContracts.cs`,
  `OrderContracts.cs`, `ReportContracts.cs`, `CategoryDto.cs`).
- Add a short `<summary>` doc comment on request records where intent isn't obvious (as
  `ProductContracts.cs` does).

## After the work

Run the gate from the repo root: `dotnet csharpier format . && dotnet build Atrium.slnx -v q`
(0W/0E) — a contract change must keep both the producing service and the consuming module building.
