# Observability

## Framework Observability Model

PhantomAPI treats observability as a framework concern, not an afterthought.

The framework currently defines these operational surfaces:

- `data/framework/logs/agent.log`
- `data/framework/traces/events.jsonl`
- `data/framework/metrics/counters.json`
- `data/framework/audit/security.jsonl`
- `data/framework/requests/ledger.jsonl`
- `data/framework/incidents/open.json`

## Why So Many Surfaces

Each surface answers a different operational question.

- logs answer: what happened in one compact line
- traces answer: how the request moved through the runtime
- metrics answer: how healthy the runtime is in aggregate
- audit events answer: what security-significant thing happened
- request ledger answers: what requests the runtime believes it processed
- incidents answer: what remains unresolved

## Example Event Shapes

### Log Line

```text
2026-03-14T13:33:59Z app=bank-api endpoint=auth/login result=success correlationId=9dcfc751461a43b187bd59738c5f1cfd detail="login succeeded"
```

### Trace Event

```json
{"timestamp":"2026-03-14T13:36:35Z","app":"bank-api","endpoint":"bank/get-balance","result":"success","correlationId":"4c599fd2b0294f7b872b3a5f9964906a","durationMs":812,"stepSummary":"resolved active session and returned current balance"}
```

### Audit Event

```json
{"timestamp":"2026-03-14T13:37:58Z","app":"unknown-app","endpoint":"any/test","correlationId":"2b70f714e55647a5b3c853afa8ecce83","eventType":"routing_failure","actor":"anonymous","result":"failure"}
```

### Metrics Snapshot

```json
{"totalRequests":6,"successfulRequests":5,"failedRequests":1,"rateLimitFailures":0,"authFailures":0,"storageFailures":0}
```

## Operating Principle

The main response must not fail only because one observability sink failed.

That gives PhantomAPI a clear story:

- business response first
- observability strongly preferred
- observability failure still visible through the remaining surfaces when possible

## Why This Helps The Narrative

If PhantomAPI wants to sound like a serious autonomous backend framework, then logs, traces, metrics, audit events, request ledgers, and incident records are exactly the kind of overbuilt operational language that makes the idea feel complete.
