# Architecture Decision Records

Short records of the decisions that shaped Atrium — the *why* behind the code, so a reviewer doesn't
have to reverse-engineer intent. Each is one page: context, the decision, consequences, and the
alternative we rejected. Format is a trimmed [MADR](https://adr.github.io/madr/).

| # | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith with reflection-discovered `IModule` UI modules | Accepted |
| [0002](0002-dapper-sprocs-dbup.md) | Dapper + stored procedures + DbUp + Mapperly, not EF Core | Accepted |
| [0003](0003-yarp-keycloak-auth.md) | YARP gateway + Keycloak (OIDC for the portal, JWT for services) | Accepted |
| [0004](0004-token-propagation-and-option-b.md) | Access token via claim into the Blazor circuit (with option B as the exit) | Accepted (with known debt) |
| [0005](0005-slice-calls-core.md) | App verticals compose core services over HTTP with bearer relay | Accepted |
| [0006](0006-shared-contracts-then-nuget.md) | Contracts as a shared project now, versioned NuGet later | Accepted |
| [0007](0007-feature-folders-and-repository-testing.md) | Organize service internals by feature; keep repository interfaces (now co-located), integration-test them | Accepted |
| [0008](0008-graceful-session-expiry-handling.md) | Map 401 to a typed `SessionExpiredException`; a shell `SessionErrorBoundary` prompts re-login instead of crashing the circuit | Accepted |
| [0009](0009-service-root-route-nesting.md) | Nest routes under one service-root group; features map relative subtrees | Accepted |
| [0010](0010-native-dialog-primitive.md) | Build the modal `Dialog` on the native `<dialog>` element (`showModal()`), not a hand-rolled overlay | Accepted |
| [0011](0011-circuit-scoped-bearer-handler.md) | The AG-UI chat client's bearer rides a `DelegatingHandler` composed manually in the circuit scope — the one sanctioned exception to ADR-0004's rule | Accepted |
| [0012](0012-shared-deployment-infrastructure.md) | Deployment infrastructure (telemetry, JWT auth, api docs, DbUp runner) is shared via `ServiceDefaults`; domain code is never shared | Accepted |

These records are point-in-time. If the code and an ADR disagree, the code wins — open a new ADR that
supersedes the old one rather than editing history.
