# Framework Feature Catalog

PhantomAPI framework features are not hidden in code.
They are part of the runtime contract.

Core framework features:

- multi-app routing through `app` plus `endpoint`
- app-package discovery under `instructions/apps/<app>`
- contract-disciplined JSON responses
- framework-owned missing-app and missing-endpoint contracts
- generic authentication and authorization posture
- app-local rate-limit interpretation
- explicit observability outputs
- safe state mutation discipline
- controlled recovery and repair behavior
- self-healing with explicit repair boundaries
- governed autonomous instruction repair
- app-local examples for request-shape guidance
- capability-driven app discovery
- instruction-first runtime orchestration

Advanced framework features:

- correlation-id aware request handling
- request ledger output
- audit trail output for security-significant events
- metrics counters for runtime health
- trace events for request flow visibility
- incident recording when safe recovery is not possible
- repair-attempt metrics and incident visibility
- validation and rollback loops for instruction changes
- app capability manifests that describe what an app supports
- optional dry-run and idempotency conventions when an endpoint or app explicitly supports them

Design principle:

- if a feature is real, the framework should name it
- if a feature is named, the app package may rely on it
- if a feature is not named, the runtime should not pretend it exists
