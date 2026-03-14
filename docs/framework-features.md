# Framework Features

## Base Framework Feature Catalog

PhantomAPI defines a full framework envelope rather than a minimal wrapper around a CLI.

### Routing And Packaging

- one transport endpoint
- `app` plus `endpoint` routing
- app-package discovery
- app manifest convention
- app capability manifest convention
- app-local example request convention

### Contract Discipline

- endpoint-owned response contract
- framework-owned routing failure contracts
- exact property-set expectation
- contract-shaped errors
- no hidden response expansion
- explicit response ownership at endpoint scope

### Security And Governance

- generic authentication posture
- endpoint-specific authorization
- request governance rules
- correlation-id handling
- idempotency-key preservation when present
- dry-run convention when explicitly supported
- framework-owned error semantics
- stable request-governance vocabulary

### Operational Safety

- safe write discipline
- app-local storage interpretation
- controlled recovery and repair rules
- instruction-defined self-healing boundaries
- autonomous instruction repair with governance
- incident recording when safe recovery is not possible
- bounded framework behavior for routing failures
- explicit separation between generic and app-specific rules

### Observability

- logs
- traces
- metrics
- audit events
- request ledger
- incident surface
- repair attempt visibility
- instruction patch visibility

### Extensibility

- multi-app package model
- app-local capabilities
- app-local rate limits
- app-local entity system
- app-local storage contract
- app-local repair policy
- app-local self-healing profile
- framework-level conventions that remain reusable across domains

## ASCII Feature Stack

```text
PhantomAPI
  |
  +-- Routing
  +-- Contracts
  +-- Governance
  +-- Security
  +-- Reliability
  +-- Observability
  +-- App Packaging
```

## Why This Matters

Without these framework features, the runtime collapses into a generic agent wrapper.
With them, it becomes a repeatable instruction-native platform with named guarantees, reusable conventions, and a clear application packaging model.
