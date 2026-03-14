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
- `data/framework/self-healing/diagnoses.jsonl`
- `data/framework/self-healing/patches.jsonl`
- `data/framework/self-healing/validations.jsonl`
- `data/framework/self-healing/rollbacks.jsonl`

## Observability Goals

The framework uses multiple surfaces because each one supports a different class of operational decision:

- logs support quick diagnosis
- traces support request-path reconstruction
- metrics support aggregate health analysis
- audit events support security and policy review
- request ledger supports operational accountability
- incident records support follow-up and remediation
- repair signals support recovery analysis
- self-healing journals support instruction-evolution analysis

## Why The Framework Owns It

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
{"totalRequests":6,"successfulRequests":5,"failedRequests":1,"rateLimitFailures":0,"authFailures":0,"storageFailures":0,"repairAttempts":1,"repairSuccesses":1,"repairFailures":0,"instructionRepairAttempts":1,"instructionRepairSuccesses":1,"instructionRepairFailures":0,"validationLoopFailures":0,"rollbackCount":0}
```

## Operating Principles

The main response must not fail only because one observability sink failed.
Observability should be strongly attempted, but business response integrity remains the primary concern.

The framework therefore assumes:

- response first
- best-effort fan-out across observability sinks
- visible degradation when an observability surface is unavailable
- retention of enough signals to reconstruct runtime behavior later

## Operational Story

If PhantomAPI is presented as a serious autonomous backend framework, then observability cannot stop at plain logs.
It needs a broader operational language:

- request correlation
- traceable execution
- measurable runtime health
- auditable security-relevant activity
- explicit incident surfaces
- explicit instruction mutation journals

That is what allows the platform to claim a stronger engineering posture around AI-mediated backend execution.
