# Use Cases

## Use Case 1: Banking Operations On A Dynamic API Runtime

The `bank-api` package defines:

- login
- balance lookup
- deposit
- withdrawal
- transfer

```text
Client
  |
  | { app: bank-api, endpoint: bank/transfer, ... }
  v
POST /dynamic-api
  |
  v
Framework engine
  |
  | resolve app package + endpoint contract
  v
bank-api package
  |
  | entities + endpoint rules + storage + rate limits
  v
Codex CLI
  |
  | resolve policy, read state, write state, return
  v
JSON response + observability
```

This enables a banking-style backend package to expose account operations without re-implementing a conventional controller and service stack for each capability.

## Use Case 2: Task Management On The Same Framework

The `task-board` package defines:

- login
- list tasks
- create task

```text
Client
  |
  | { app: task-board, endpoint: tasks/create, ... }
  v
POST /dynamic-api
  |
  v
Same framework
  |
  | different app package
  v
task-board package
  |
  v
JSON response + logs + traces + metrics
```

The important point is not the task domain itself.
The important point is that the same framework semantics, observability model, and contract discipline can host a completely different application package.

## Use Case 3: Shared Platform For Many Internal APIs

PhantomAPI can present itself as:

- one runtime
- one operational model
- many app packages
- consistent contract handling
- consistent observability surfaces

That makes it relevant for teams that want a common backend operating layer across many internal services without rebuilding the same runtime assumptions repeatedly.

## Use Case 4: Instruction-Defined Delivery Workflow

A new system can be introduced by adding:

- app manifest
- entity files
- storage rules
- endpoint contracts
- app-local example requests

That creates a delivery model where application onboarding is mostly package assembly:

```text
new app request
    |
    v
create app package
    |
    +-- app.md
    +-- entities/
    +-- endpoints/
    +-- config/
    +-- storage/
    +-- .examples/
    |
    v
runtime recognizes package
    |
    v
app becomes addressable through /dynamic-api
```

## Use Case 5: Contract-Governed AI Execution

PhantomAPI is also useful when a team wants AI-mediated execution but still wants a strict outer boundary:

- request arrives as raw JSON
- endpoint contract declares the expected response shape
- API layer rejects structurally invalid output
- framework emits operational signals for every request path

This gives the system a stronger engineering posture than a simple free-form agent wrapper.

## Use Case 6: Autonomous Instruction Repair

PhantomAPI can also treat defects in the instruction package as first-class runtime issues:

```text
request fails
    |
    v
diagnosis identifies instruction drift
    |
    v
minimal patch is applied
    |
    v
validation loop runs
    |
    +-- success ----------> request retried once
    |
    +-- failure ----------> rollback + incident
```

This is the point where PhantomAPI stops behaving like a thin agent wrapper and starts behaving like a self-governing backend framework.
