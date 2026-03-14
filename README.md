![PhantomAPI](docs/assets/branding/logo_full.png)

# PhantomAPI

**Documentation-Driven, AI-Supported, Autonomous Backend Framework**

PhantomAPI is a documentation-native backend runtime for people who want to act like the instruction layer is the product, the framework, the platform, the operating model, and the orchestration brain all at once.

There is still one real HTTP endpoint.
There is still a real runtime.
There is still real state.
But the behavioral center of gravity is intentionally moved into instructions, app packages, contracts, and operational conventions.

## Elevator Pitch

PhantomAPI presents itself as:

- a documentation-driven backend platform
- an instruction-first application framework
- a multi-app autonomous runtime
- a contract-disciplined AI orchestration layer
- an observability-aware agent backend fabric

If the framework is explicit, the app package is coherent, and the contracts are disciplined, the runtime should be able to do the rest.

## Core Architecture

```text
                +-----------------------------------------+
Client JSON --->| POST /dynamic-api                       |
                +-------------------+---------------------+
                                    |
                                    v
                +-----------------------------------------+
                | PhantomAPI Framework                    |
                | engine + governance + contracts         |
                | capabilities + observability + errors   |
                +-------------------+---------------------+
                                    |
                             app + endpoint routing
                                    |
                                    v
                +-----------------------------------------+
                | App Package                              |
                | app.md + entities + endpoints           |
                | storage + config + .examples            |
                +-------------------+---------------------+
                                    |
                                    v
                +-----------------------------------------+
                | Codex CLI Runtime                        |
                | reads docs, resolves state, performs    |
                | writes, emits observability, returns    |
                +-------------------+---------------------+
                                    |
                                    v
                +-----------------------------------------+
                | JSON response + logs + traces + metrics |
                | + audit + request ledger + incidents    |
                +-----------------------------------------+
```

## What The Framework Actually Owns

The framework layer is no longer just a few generic notes.
It now defines a more serious runtime envelope:

- engine boot order
- app-package convention
- feature catalog
- capability model
- contract discipline
- request governance
- generic security posture
- rate-limit interpretation
- observability surfaces
- reliability and safe repair posture
- framework error policy

Framework instruction files live under [instructions/framework](C:/Users/tigyi/Documents/GitHub/temp/PhantomAPI/instructions/framework).

## Feature Surface

PhantomAPI now advertises a more robust base framework feature list:

- multi-app routing through `app` plus `endpoint`
- app capability manifests
- framework-level routing failure contracts
- endpoint-owned response contracts
- contract-shaped failures
- endpoint-level authentication and authorization rules
- app-local rate-limit policies
- app-local example requests for input-shape guidance
- correlation-id aware request handling
- request-ledger output
- audit event output
- logs, traces, metrics, and incident surfaces
- safe recovery and repair conventions
- explicit write discipline around state mutation

This is the important split:

```text
Framework owns: how the runtime behaves
App owns:       what the software does
API owns:       transport + contract guard
```

## Multi-App Model

PhantomAPI is organized as:

- `instructions/framework`
  cross-app runtime conventions
- `instructions/apps/<app>`
  one concrete software system
- `data/apps/<app>`
  app runtime state
- `data/framework/*`
  shared observability and operational outputs

Current app packages:

- `bank-api`
  authentication, balance query, deposit, withdrawal, transfer
- `task-board`
  authentication, task listing, task creation

App-local example requests live with the package itself:

- `instructions/apps/bank-api/.examples/*.json`
- `instructions/apps/task-board/.examples/*.json`

## Observability Story

The framework now defines a broader observability surface than before:

- `data/framework/logs/agent.log`
- `data/framework/traces/events.jsonl`
- `data/framework/metrics/counters.json`
- `data/framework/audit/security.jsonl`
- `data/framework/requests/ledger.jsonl`
- `data/framework/incidents/open.json`

Representative outputs:

```text
2026-03-14T13:33:59Z app=bank-api endpoint=auth/login result=success correlationId=9dcfc751461a43b187bd59738c5f1cfd detail="login succeeded"
```

```json
{"timestamp":"2026-03-14T13:36:35Z","app":"bank-api","endpoint":"bank/get-balance","result":"success","correlationId":"4c599fd2b0294f7b872b3a5f9964906a","durationMs":812,"stepSummary":"resolved active session and returned current balance"}
```

```json
{"timestamp":"2026-03-14T13:37:58Z","app":"unknown-app","endpoint":"any/test","correlationId":"2b70f714e55647a5b3c853afa8ecce83","eventType":"routing_failure","actor":"anonymous","result":"failure"}
```

```json
{"totalRequests":6,"successfulRequests":5,"failedRequests":1,"rateLimitFailures":0,"authFailures":0,"storageFailures":0}
```

## Why This Is Entertainingly Serious

PhantomAPI is useful as:

- a documentation-first backend experiment
- a satire of AI-native software delivery language
- a testbed for instruction-driven app packaging
- a platform narrative for autonomous backend orchestration

It is trying to sound like a serious category while fully exposing how much of the system is held together by instruction quality and runtime discipline.

## Additional Docs

- [docs/positioning.md](C:/Users/tigyi/Documents/GitHub/temp/PhantomAPI/docs/positioning.md)
  platform-style framing and narrative
- [docs/use-cases.md](C:/Users/tigyi/Documents/GitHub/temp/PhantomAPI/docs/use-cases.md)
  use cases and ASCII flow charts
- [docs/operating-model.md](C:/Users/tigyi/Documents/GitHub/temp/PhantomAPI/docs/operating-model.md)
  responsibility split and runtime model
- [docs/framework-features.md](C:/Users/tigyi/Documents/GitHub/temp/PhantomAPI/docs/framework-features.md)
  expanded base feature catalog
- [docs/observability.md](C:/Users/tigyi/Documents/GitHub/temp/PhantomAPI/docs/observability.md)
  observability model, event shapes, and operational surfaces

## Configuration

Environment variables:

- `Phantom__CliCommand`
  default is `codex.cmd` on Windows and `codex` elsewhere
- `Phantom__CliArgumentsTemplate`
  default is `--dangerously-bypass-approvals-and-sandbox exec --skip-git-repo-check --output-last-message {output} -`
- `Phantom__CliTimeoutSeconds`
  default is `300`

## Quick Start

```bash
dotnet run
```

PowerShell example:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5000/dynamic-api `
  -ContentType "application/json" `
  -Body (Get-Content instructions/apps/bank-api/.examples/login.json -Raw)
```
