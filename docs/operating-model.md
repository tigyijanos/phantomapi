# Operating Model

## Runtime Responsibility Split

PhantomAPI works because the responsibility boundaries are explicit.

### Framework

The framework owns:

- loading order
- package conventions
- contract discipline
- generic security posture
- rate-limit interpretation
- observability surfaces
- reliability posture
- framework error contracts

### App Package

Each app package owns:

- domain entities
- app-specific rules
- endpoint behavior
- endpoint-level security
- storage interpretation
- app-local rate limits
- app-local example requests

### API Layer

The API layer owns:

- one transport endpoint
- raw request forwarding
- contract lookup
- output validation

## ASCII Responsibility Diagram

```text
             +---------------------------+
             | API Layer                 |
             | transport + contract guard|
             +-------------+-------------+
                           |
                           v
             +---------------------------+
             | Framework                 |
             | conventions + operations  |
             +-------------+-------------+
                           |
                           v
             +---------------------------+
             | App Package               |
             | behavior + state contract |
             +-------------+-------------+
                           |
                           v
             +---------------------------+
             | Codex CLI                 |
             | runtime execution         |
             +---------------------------+
```

## What This Enables

- a stable runtime frame across many apps
- consistent response discipline
- app-local packaging of domain intent
- reusable generic operational rules
- cleaner storytelling around AI-assisted backend execution

## What It Does Not Magically Solve

- prompt quality still matters
- instruction quality still matters
- state quality still matters
- runtime latency still exists
- agent discipline still matters

That limitation is part of the point.
