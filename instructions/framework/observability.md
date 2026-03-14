# Framework Observability

The framework owns logs, traces, and metrics across all apps.

Use these locations:

- `data/framework/logs/agent.log`
- `data/framework/traces/events.jsonl`
- `data/framework/metrics/counters.json`
- `data/framework/audit/security.jsonl`
- `data/framework/requests/ledger.jsonl`
- `data/framework/incidents/open.json`

Observability rules:

- log one concise line per request outcome
- add one trace event per request with app, endpoint, result, timing, and correlation data when available
- write one request-ledger event per request with app, endpoint, correlation id, and request outcome
- write one audit event for authentication, authorization, token, rate-limit, and other security-significant outcomes
- update counters for total requests, successful requests, failed requests, rate-limit failures, auth failures, and storage failures
- open or update an incident record when recovery fails or a state inconsistency is detected
- never break the main response because an observability write failed

Preferred fields:

- logs: `timestamp`, `app`, `endpoint`, `result`, `correlationId`, `detail`
- traces: `timestamp`, `app`, `endpoint`, `result`, `correlationId`, `durationMs`, `stepSummary`
- request ledger: `timestamp`, `app`, `endpoint`, `correlationId`, `authenticated`, `result`
- audit events: `timestamp`, `app`, `endpoint`, `correlationId`, `eventType`, `actor`, `result`
- metrics counters: `totalRequests`, `successfulRequests`, `failedRequests`, `rateLimitFailures`, `authFailures`, `storageFailures`
