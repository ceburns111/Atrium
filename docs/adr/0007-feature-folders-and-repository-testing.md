# ADR-0007 — Organize service internals by feature; keep repository interfaces and integration-test them

**Status:** Accepted · **Deciders:** Atrium build · **Context phase:** 7 (post-tests refactor)

> **Amended 2026-07-03:** the "keep `DatabaseInitializer` duplicated" clause is superseded — the
> copies drifted in practice, and the runner now lives once in `Atrium.ServiceDefaults`.
> [ADR-0012](0012-shared-deployment-infrastructure.md) records the sharpened rule (domain/data code is
> never shared between services; deployment-infrastructure defaults may be). The feature-folder and
> repository-testing decisions in this ADR stand untouched.

## Context

The two backend services (`Atrium.Services.Catalog`, `Atrium.Services.Storefront`) had grown to ~15–20
files each sitting **flat** in the project root — endpoints, repositories, interfaces, row types, and a
report builder all in one directory. Nothing was wrong functionally, but the flat layout stopped
telling you what the service *does*: Orders code and Reports code were interleaved alphabetically.

Two questions came up while cleaning this up:

1. **How should the files be organized** — by technical layer (Endpoints/, Repositories/, Models/) or
   by feature (Orders/, Reports/)?
2. **Do the single-implementation repository interfaces earn their keep**, given the data layer is
   tested with Testcontainers rather than by mocking the repositories?

## Decision

### Feature folders, nested namespaces

Organize each service's internals **by feature (vertical slice)**, not by layer. A feature folder holds
everything for that slice — endpoint, handler logic, repository (+ its interface), and row types —
side by side. Namespaces follow the folders.

```
Atrium.Services.Storefront/
  Program.cs
  Orders/    OrdersEndpoints, OrderPricing, OrderRepository (+ IOrderRepository), OrderRow
  Reports/   ReportsEndpoints, SalesReportBuilder, ReportRepository (+ IReportRepository), ProductSalesRow
  Catalog/   StorefrontCatalogClient          ← the shared slice→core client, used by both features
  Data/      DbUp initializer + embedded SQL scripts

Atrium.Services.Catalog/
  Program.cs
  Catalog/   CatalogEndpoints, CatalogRepository (+ ICatalogRepository), CatalogMapper, ProductRow
  Data/      DbUp initializer + embedded SQL scripts
```

> **Update (post-reorg):** the repository interfaces are no longer separate files. Each single-implementation
> interface (`ICatalogRepository`, `IOrderRepository`, `IReportRepository`) is now **co-located directly
> above its implementing class** in `CatalogRepository.cs` / `OrderRepository.cs` / `ReportRepository.cs`.
> The DIP seam is kept (interface still declared); the standalone interface file — pure ceremony for a
> one-implementation seam — is gone. Pure move, no behaviour change.

Guidelines applied, in the spirit of "**not strict**":

- **Feature over layer.** You read a slice top-to-bottom in one folder instead of hopping between an
  `Endpoints/` and a `Repositories/` tree.
- **Shared-up, sparingly.** Only genuinely cross-feature code moves to a shared spot: Storefront's
  `StorefrontCatalogClient` (used by both Orders pricing and Reports) sits in a `Catalog/` folder, not
  duplicated into each feature.
- **Tolerate small duplication over premature abstraction.** `Data/DatabaseInitializer.cs` is
  byte-identical in both services. It stays **duplicated** rather than extracted into a shared data
  library — a shared lib would couple two services that are meant to be independently deployable
  ([ADR-0006](0006-shared-contracts-then-nuget.md), and see BEYOND-THE-DEMO §3) to save ~40 lines. A
  little copy-paste is cheaper than the wrong coupling.
- **Endpoints were already right.** The endpoint style — an extension method
  (`MapOrderEndpoints(this IEndpointRouteBuilder)`), one `MapGroup` with shared auth/tags, handlers as
  named static methods, no auto-registration package — already matches the pattern in
  [Tess Ferrandez's "Organizing minimal APIs"](https://www.tessferrandez.com/blog/2023/10/31/organizing-minimal-apis.html).
  This change was folders, not a rewrite.

### Keep the repository interfaces; integration-test the implementations

`ICatalogRepository` / `IOrderRepository` / `IReportRepository` each have exactly one implementation.
We **keep** them, and we test the concrete repositories against a **real database** (Testcontainers,
[see the test suite](../../tests)), not by mocking.

The reasoning is the part worth writing down, because "keep the interface for mockability" is the
*weak* justification here:

- **Mocking a repository tests the mock, not the SQL.** A `Mock<IOrderRepository>` set up to return two
  rows proves your handler calls the repo and shapes the result — it proves nothing about whether
  `usp_Order_GetList`, the transaction, or the flat-rows→`OrderDto` regrouping is correct. That is the
  entire job of a repository, and a mock can't exercise it.
- **Testcontainers tests the thing that can actually break** — real sprocs, real Dapper, real Mapperly
  mapping, against a real SQL Server. For the data layer this is strictly higher-fidelity than a mock,
  so using the **concrete** repository in those tests is correct, not a smell.
- **The logic that benefits from isolated unit tests was extracted out of the handlers** into pure
  functions (`OrderPricing`, `SalesReportBuilder`) and is unit-tested with no repository at all. So the
  handlers are thin glue; the payoff of faking a repo to test them is small.

Given that, why keep the interfaces at all? Two honest reasons — neither of which is "mockability":

- **Dependency inversion + convention.** Handlers depend on an abstraction, not on Dapper. This is the
  shape most .NET developers expect, and it keeps the door open to a decorator (caching, logging) or a
  second implementation without touching call sites.
- **Optionality at ~zero cost.** If a handler ever grows real branching that we want to unit-test
  without a database (e.g. a 404 path), the seam to hand-roll a fake is already there.

## Consequences

- **The code reads as what it does.** Opening `Orders/` shows the whole slice; a new feature is a new
  folder, reinforcing the same "extract when it hurts" grain as the module and service boundaries.
- **Behaviour-preserving.** Pure move + namespace change; `git` tracked every file as a rename (history
  intact) and the full suite still passes (20/20, incl. the Testcontainers lane).
- **Interview-defensible testing story.** The position is: *repositories are integration-tested against
  a real database because a mocked repository only proves my code calls it, not that the SQL is
  correct; the business logic is extracted into pure functions and unit-tested directly; the interface
  stays as the DIP seam and keeps faking cheap if a handler ever needs it.* That pre-empts the "these
  aren't mockable" critique — they are (the interfaces are kept); we deliberately chose a
  higher-fidelity test for the data layer. The one boundary where a fake genuinely earns its keep is
  the **outbound HTTP** dependency (`StorefrontCatalogClient`), and even that composition logic was
  extracted into a pure function to test directly.
- **Cost accepted:** two identical `DatabaseInitializer` copies, and slightly deeper namespaces.

## Alternatives rejected

- **Organize by layer (Endpoints/ + Repositories/ + Models/).** Scatters a single feature across three
  trees; the opposite of the vertical-slice grain the rest of the system uses.
- **Drop the repository interfaces.** Defensible on "less abstraction" grounds, but breaks the
  convention most .NET reviewers expect and removes the fake-a-repo option for near-zero real gain —
  and co-locating each interface above its class (see the update note above) already removes the only
  concrete cost, the extra file.
- **Extract a shared `DatabaseInitializer` library.** Couples two independently-deployable services to
  remove ~40 duplicated lines; wrong trade at this size.
- **Mock repositories in unit tests instead of Testcontainers.** Lower-fidelity — tests the mock, not
  the SQL. See above.
