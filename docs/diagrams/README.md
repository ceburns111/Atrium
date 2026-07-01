# Diagrams

Mermaid diagrams for the Atrium platform. They render inline on GitHub. The container/topology diagram
lives in [ARCHITECTURE.md](../ARCHITECTURE.md); the flow diagrams live here.

| Diagram | What it shows | Ties to |
|---|---|---|
| [auth-sequence.md](auth-sequence.md) | OIDC login → token-in-claim → `AccessTokenHolder` → bearer → gateway → JWT validation | [ADR-0003](../adr/0003-yarp-keycloak-auth.md), [ADR-0004](../adr/0004-token-propagation-and-option-b.md) |
| [checkout-flow.md](checkout-flow.md) | Anonymous browse → cart → sign-in gate → checkout → **simulated** payment → order → confirmation | [ADR-0009](../adr/0009-service-root-route-nesting.md) |
| [module-discovery.md](module-discovery.md) | Reflection over `Atrium.Modules.*.dll` → `ModuleCatalog` → role-gated Home cards + NavMenu | [ADR-0001](../adr/0001-modular-monolith.md) |

Accuracy rule (same as the ADRs): if the code and a diagram disagree, the code wins — update the
diagram. Every node/edge here was grep-checked against `src/` when written.
