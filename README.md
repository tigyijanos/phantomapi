![PhantomAPI](docs/assets/branding/logo_full.png)

# PhantomAPI

**Documentation-Driven, AI-Native, Slightly Unreasonable Backend Framework**

*A backend framework where the documentation finally becomes the runtime.*

PhantomAPI combines a thin `.NET` transport layer with an instruction-driven runtime. Framework instructions define cross-app rules, app packages define domain behavior, and the API boundary enforces the response contract.

In practical terms: one HTTP endpoint, many app packages, live state on disk, and an AI runtime that is required to return contract-shaped JSON.

Part backend platform, part docs-as-runtime experiment.
The code path is real either way.

## Start Here

If you want Copilot to perform the opening ritual, start with:

```text
Clone https://github.com/tigyijanos/phantomapi, figure out how to start the app locally, use port 5050 if 5000 is taken, and tell me which URL to hit when the backend becomes sentient.
```

If you prefer the manual old-school route, start here:

- [Getting Started](docs/getting-started.md)
  local run, Docker, Codex auth, and configuration
- [Operating Model](docs/operating-model.md)
  framework, app, and API responsibility split
- [Runtime Acceleration](docs/runtime-acceleration.md)
  eager startup, caches, warm metadata, and session reuse
- [Framework Features](docs/framework-features.md)
  capability surface of the platform
- [Observability](docs/observability.md)
  logs, traces, metrics, audit, incidents, and journals

## What It Is

PhantomAPI treats its instruction system as the executable source of truth for backend behavior.

- one HTTP ingress: `POST /dynamic-api`
- one framework under `instructions/framework`
- many app packages under `instructions/apps/<app>`
- live state under `data/apps/<app>`
- hard response validation at the API boundary
- observability emitted as part of runtime behavior

The runtime stays intentionally thin.
The framework surface stays explicit.
The application model stays package-oriented.

## Why It Exists

Modern backend engineering suffers from a fundamental problem:

developers keep writing code.

PhantomAPI explores what happens when documentation becomes the system of record and the runtime simply interprets that declared intent.

Some of it is serious platform work.
Some of it is clearly a bit of a joke.
The code path is real either way.

## Where It Fits

Traditional backend:

```text
code defines behavior, docs explain after the fact
```

PhantomAPI:

```text
instructions define behavior, runtime executes, contracts verify
```

That shifts the ownership model:

- the framework owns cross-app policy and governance
- the app package owns domain behavior and response contracts
- the API layer owns transport and output guardrails

## Use It When

- you want one runtime to host multiple backend packages
- you want contracts and operational rules to live next to endpoint instructions
- you want AI-mediated execution without giving up response validation
- you want logs, traces, metrics, audit, and repair artifacts to be part of the framework model
- you are exploring instruction-defined delivery, not just agent demos
- you are comfortable with a backend that may either solve the request or leave behind an unusually well-documented crime scene

## Architecture At A Glance

```text
Client JSON
    |
    v
+---------------------------+
| POST /dynamic-api         |
| Minimal API transport     |
+-------------+-------------+
              |
              v
+---------------------------+
| Framework Instructions    |
| routing + contracts       |
| governance + observability|
+-------------+-------------+
              |
              v
+---------------------------+
| App Package               |
| endpoints + entities      |
| storage + policies        |
+-------------+-------------+
              |
              v
+---------------------------+
| Codex CLI Runtime         |
| resolve, decide, act      |
+-------------+-------------+
              |
              v
+---------------------------+
| JSON response             |
| logs + traces + metrics   |
+---------------------------+
```

## Quick Start

Run locally:

```bash
dotnet run --project src/PhantomApi/PhantomApi.csproj
```

If port `5000` is already busy:

```powershell
.\scripts\run-local.ps1 -PreferredPort 5050
```

Example request:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5050/dynamic-api `
  -ContentType "application/json" `
  -Body (Get-Content instructions/apps/bank-api/.examples/login.json -Raw)
```

Container path:

```powershell
docker compose up --build -d
```

For the full setup flow, Codex authentication, and configuration knobs, see [Getting Started](docs/getting-started.md).

## What You Get

- multi-app routing through `app` plus `endpoint`
- endpoint-owned response contracts with hard output validation
- app-scoped storage, rate-limit, and repair rules
- framework-owned observability surfaces
- governed self-healing for operational data and instructions
- runtime acceleration through eager app-server startup, route caches, and `codex exec resume`

The runtime acceleration layer is documented separately in [Runtime Acceleration](docs/runtime-acceleration.md).

## Repository Layout

- `instructions/framework`
  cross-app runtime law
- `AGENTS.md`
  repo workflow rules plus scoped runtime-agent rules
- `.github/copilot-instructions.md`
  repo-level Copilot/Codex workflow guidance
- `src/PhantomApi`
  backend application code, project file, Dockerfile, and app config
- `instructions/apps/<app>`
  one packaged software system
- `data/apps/<app>`
  live app state
- `scripts/*`
  local helper and benchmark automation
- `benchmarks/*`
  microbenchmarks for deterministic C# hotspots
- `data/framework/*`
  shared logs, traces, metrics, audit, and repair journals
- `docs/*`
  product, runtime, and operating documentation

Current app packages:

- `bank-api`
  login, balance, deposit, withdrawal, transfer
- `task-board`
  login, task listing, task creation, task lifecycle handling

## Documentation Map

- [docs/getting-started.md](docs/getting-started.md)
  local run, Docker Compose, Codex auth, configuration
- [docs/contributing/assistant-workflow.md](docs/contributing/assistant-workflow.md)
  default assistant and contributor workflow for repo changes
- [docs/positioning.md](docs/positioning.md)
  platform thesis, industry context, and product framing
- [docs/use-cases.md](docs/use-cases.md)
  concrete backend package patterns
- [docs/operating-model.md](docs/operating-model.md)
  runtime ownership and lifecycle
- [docs/framework-features.md](docs/framework-features.md)
  feature catalog for the framework surface
- [docs/runtime-acceleration.md](docs/runtime-acceleration.md)
  warm runtime strategy and prewarm metadata
- [docs/benchmarking.md](docs/benchmarking.md)
  BenchmarkDotNet microbenchmarks plus end-to-end latency harness
- [docs/observability.md](docs/observability.md)
  operational surfaces and event shapes
- [docs/self-healing.md](docs/self-healing.md)
  repair, validation, rollback, and mutation governance
- [docs/system-summary.md](docs/system-summary.md)
  concise platform summary, platform claims, and architectural philosophy
- [docs/zerobackend-phantomapi-concept.md](docs/zerobackend-phantomapi-concept.md)
  concept sketch and earlier framing notes

## Status

PhantomAPI is still experimental, but it is designed to behave like a real backend platform:

- explicit contracts
- explicit package ownership
- explicit runtime observability
- explicit operational doctrine

**strategically experimental but architecturally inevitable**

<sub>The framework remains fully enterprise-ready in theory.</sub>

## Disclaimer

No backend engineers were harmed in the development of this framework.

Several may become unnecessary.

[LICENSE](LICENSE)

<sub>[legal](docs/legal/README.md)</sub>
