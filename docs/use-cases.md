# Use Cases

## Use Case 1: Banking Without Hand-Written Controllers

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
  | decide, read, write, return
  v
JSON response + observability
```

## Use Case 2: Task Tracking On The Same Runtime

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

## Use Case 3: Multi-App Platform Narrative

PhantomAPI can present itself as:

- one runtime
- one operational model
- many app packages
- consistent contract handling
- consistent observability surfaces

## Use Case 4: Instruction-First Delivery

A new system can be introduced by adding:

- app manifest
- entity files
- storage rules
- endpoint contracts
- app-local example requests

That creates a credible "documentation-driven delivery" story even when the actual runtime is still a very small amount of code.
