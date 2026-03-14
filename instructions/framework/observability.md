# Framework Observability

The framework owns logs, traces, and metrics across all apps.

Use these locations:

- `data/framework/logs/agent.log`
- `data/framework/traces/events.jsonl`
- `data/framework/metrics/counters.json`
- `data/framework/audit/security.jsonl`
- `data/framework/requests/ledger.jsonl`
- `data/framework/incidents/open.json`
- `data/framework/self-healing/diagnoses.jsonl`
- `data/framework/self-healing/patches.jsonl`
- `data/framework/self-healing/validations.jsonl`
- `data/framework/self-healing/rollbacks.jsonl`

Observability rules:

- log one concise line per request outcome
- add one trace event per request with app, endpoint, result, timing, and correlation data when available
- write one request-ledger event per request with app, endpoint, correlation id, and request outcome
- write one audit event for authentication, authorization, token, rate-limit, and other security-significant outcomes
- update counters for total requests, successful requests, failed requests, rate-limit failures, auth failures, storage failures, repair attempts, repair successes, repair failures, instruction repair attempts, instruction repair successes, instruction repair failures, validation loop failures, and rollback count
- open or update an incident record when recovery fails or a state inconsistency is detected
- record repair attempts in traces and incidents when a self-healing action was evaluated or executed
- record diagnoses, patches, validations, and rollbacks in the self-healing files when the instruction system was part of the healing path
- never break the main response because an observability write failed

Preferred fields:

- logs: `timestamp`, `app`, `endpoint`, `result`, `correlationId`, `detail`
- traces: `timestamp`, `app`, `endpoint`, `result`, `correlationId`, `durationMs`, `stepSummary`, `repairAction`, `repairOutcome`
- request ledger: `timestamp`, `app`, `endpoint`, `correlationId`, `authenticated`, `result`
- audit events: `timestamp`, `app`, `endpoint`, `correlationId`, `eventType`, `actor`, `result`
- metrics counters: `totalRequests`, `successfulRequests`, `failedRequests`, `rateLimitFailures`, `authFailures`, `storageFailures`, `repairAttempts`, `repairSuccesses`, `repairFailures`, `instructionRepairAttempts`, `instructionRepairSuccesses`, `instructionRepairFailures`, `validationLoopFailures`, `rollbackCount`
- incidents: `timestamp`, `app`, `endpoint`, `correlationId`, `incidentType`, `severity`, `status`, `repairAttempted`, `repairOutcome`, `detail`
- self-healing diagnoses: `timestamp`, `app`, `endpoint`, `correlationId`, `diagnosisType`, `suspectedRootCause`, `targetFiles`, `confidence`
- self-healing patches: `timestamp`, `app`, `endpoint`, `correlationId`, `patchId`, `changedFiles`, `reason`, `expectedEffect`
- self-healing validations: `timestamp`, `app`, `endpoint`, `correlationId`, `patchId`, `validationOutcome`, `detail`
- self-healing rollbacks: `timestamp`, `app`, `endpoint`, `correlationId`, `patchId`, `rollbackReason`, `restored`
