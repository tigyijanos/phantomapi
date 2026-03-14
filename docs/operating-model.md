# Operating Model

## Runtime Responsibility Split

PhantomAPI works because the responsibility boundaries are explicit and stable.

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

## Runtime Sequence

```text
request received
    |
    v
framework resolves app package
    |
    v
endpoint contract is located
    |
    v
runtime executes from framework + app instructions
    |
    v
API validates response contract
    |
    v
observability surfaces are updated
```

## What This Enables

- a stable runtime frame across many apps
- consistent response discipline
- app-local packaging of domain intent
- reusable generic operational rules
- clearer governance around AI-assisted backend execution
- stronger separation between transport, policy, and domain behavior

## Operational Principles

- the framework defines cross-app semantics once
- each app package contributes only domain-specific behavior
- endpoint instructions remain the source of response truth
- observability is emitted as part of runtime behavior, not as a separate afterthought
- the HTTP layer stays intentionally small so policy drift does not split across code and instructions

## Constraints

- prompt quality still matters
- instruction quality still matters
- state quality still matters
- runtime latency still exists
- agent discipline still matters

These constraints do not weaken the operating model.
They define where the framework expects precision: in contracts, package structure, policy, and runtime discipline.
