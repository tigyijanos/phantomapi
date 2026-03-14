# Framework Features

## Base Framework Feature Catalog

PhantomAPI now defines a more complete base-framework envelope than a simple "engine + a few rules" setup.

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

### Security And Governance

- generic authentication posture
- endpoint-specific authorization
- request governance rules
- correlation-id handling
- idempotency-key preservation when present
- dry-run convention when explicitly supported

### Operational Safety

- safe write discipline
- app-local storage interpretation
- controlled recovery and repair rules
- incident recording when safe recovery is not possible

### Observability

- logs
- traces
- metrics
- audit events
- request ledger
- incident surface

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

Without these framework features, the runtime is just a vague "agent reads some files" story.
With them, the system starts to look like a real instruction-native platform with named operational guarantees and a repeatable app model.
