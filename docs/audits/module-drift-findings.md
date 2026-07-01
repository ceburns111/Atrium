# Module drift audit — findings

Date: 2026-07-01. A read-only drift audit run over every Atrium module, service, and the shared
contracts, checking each against its reference implementation and against its siblings. **Nothing here
is changed** — this is a report.

"Drift" here means divergence from the canonical shape (the reference module/service the skills point
at) or inconsistency between siblings that should match. Each raw finding was re-verified by hand
against the code **and** the guardrails before landing here; two flagged items were **rejected on
verification** (see _Rejected_ below) so you don't act on a bad call.

Scope audited: `Atrium.Modules.{Storefront,Admin,Reports}`, `Atrium.Services.{Catalog,Storefront}`,
`Atrium.Contracts`, and the `Atrium.Portal` shell.

Legend — Severity: High / Med / Low. Effort: S/M/L.

## Headline

**The codebase is in good shape.** The largest previously-known drift — the HTTP-client duplication
(OE-2 / SL-1 / SL-2 in `cleanup-findings.md`, five typed clients each hand-rolling token attachment and
401-logging) — appears **resolved**: all five clients now share `request.Authorize(tokens)` and
`response.LogIfUnsuccessful(logger, request)` extensions from `Atrium.Design`, call
`ThrowIfSessionExpired()` before `EnsureSuccessStatusCode()`, and none use a `DelegatingHandler`. No
`IModule`, gateway-routing, feature-folder, Dapper/sproc/Mapperly, or auth-wiring violations were found.

What remains is three **Low**-severity cosmetic inconsistencies. No High or Med genuine drift.

---

## Findings

### D-1 · `OrdersClient` uses `message` where every sibling uses `request`
- **Location:** `src/Atrium.Modules.Storefront/Orders/OrdersClient.cs:24,28,30,38,41`
- **Severity:** Low · **Effort:** S
- The reference client (`Catalog/CatalogClient.cs`) and the other three clients name the
  `HttpRequestMessage` variable `request`; `OrdersClient` alone names it `message`. Purely a naming
  inconsistency — behavior is identical.
- _Secondary observation:_ `CatalogClient` factors the send/log/throw/ensure sequence into a private
  `GetAsync<T>` helper (`CatalogClient.cs:34`); `OrdersClient` inlines that boilerplate twice. A shared
  GET helper wouldn't cover its POST, so this is marginal — noting only for consistency.
- **Action:** Rename `message` → `request`. Trivial.
- [ ] Approve · [ ] Deny

### D-2 · Contracts: `*Contracts.cs` naming vs the stated "requests-only" rule
- **Locations:** `src/Atrium.Contracts/ReportContracts.cs`, `src/Atrium.Contracts/OrderContracts.cs`
- **Severity:** Low · **Effort:** S
- The atrium-contracts guardrail states the convention as "a `*Dto.cs` for the read shape and a
  `*Contracts.cs` for the request records," but two files don't fit that split:
  - `ReportContracts.cs` contains **only** read shapes (`CategorySalesDto`, `SalesReportDto`) and zero
    request records — by the rule it would be `ReportDto.cs`.
  - `OrderContracts.cs` **mixes** request records (`OrderItemRequest`, `CreateOrderRequest`) with read
    shapes (`OrderLineDto`, `OrderDto`) in one file.
- **Important caveat:** the guardrail *itself cites both files as reference examples of "the current
  layout."* So this is a **rule-vs-examples inconsistency in the convention**, not code that drifted
  away from a clean baseline. The honest fix is to pick one and align the other: either (a) relax the
  rule to "one file per feature's contracts, reads + requests together" (matches `OrderContracts.cs`),
  or (b) split the files and rename `ReportContracts.cs` → `ReportDto.cs` to match the strict rule.
  Recommend (a) — it's less churn and the mixed-file grouping reads fine.
- **Action:** Reconcile the skill wording with the files; only rename/split if you choose (b).
- [ ] Approve · [ ] Deny

### D-3 · Storefront `.csproj` omits the embedded-SQL explanatory comment Catalog has
- **Location:** `src/Atrium.Services.Storefront/Atrium.Services.Storefront.csproj` (EmbeddedResource item)
- **Severity:** Low · **Effort:** S
- Both services embed their SQL identically (`<EmbeddedResource Include="Data\Scripts\**\*.sql" />`), but
  `Atrium.Services.Catalog.csproj` carries an explanatory comment on the glob and Storefront's does not.
  Documentation-only; no functional difference.
- **Action:** Copy the comment across for parity, or drop it from both.
- [ ] Approve · [ ] Deny

---

## Accepted divergences (intentional — do not "fix")

- **Admin `AdminCatalogClient.WriteAsync` calls `LogIfUnsuccessful` conditionally** (not the reference's
  single unconditional call): deliberate — it suppresses logging for *expected* 400/403/404 responses
  that surface friendly user messages. Documented with inline comments; its `GetAsync` follows the
  reference pattern. `AdminCatalogClient.cs`.
- **`DatabaseInitializer.cs` duplicated verbatim** across both services: the guardrail mandates this
  duplication (each service ships self-contained). Confirmed identical.

## Rejected on verification (flagged by a raw pass, then disproven)

- **~~Catalog declares its service-root group in `CatalogEndpoints.cs`, not `Program.cs`~~** — **not
  drift.** The route-nesting guardrail (ADR-0009) explicitly blesses this: "A single-feature core
  service is the degenerate case — its one group *is* the service root, **as in `CatalogEndpoints.cs`**."
  Storefront, a multi-feature vertical, correctly declares `/storefront` once in `Program.cs:109` and
  maps `Orders`/`Reports` as relative subtrees. Both follow the pattern as written.
- **~~`CategoryDto.cs` is an "orphaned" read shape with no companion `CategoryContracts.cs`~~** — **not
  drift.** Categories are read-only by design (the Catalog service exposes only `usp_Category_GetList`;
  there is no create/update path), so there are correctly no category request records.

---

## Coverage note

Portal shell (`ModuleLoader`/`ModuleCatalog`/`Routes.razor`/`Program.cs`) was checked and is **clean**:
all three modules are discovered by reflection over `Atrium.Modules.*.dll`, registered uniformly, and
referenced as plain `<ProjectReference>` entries — nothing hardcoded or special-cased.
