# Runtime Acceleration

## Overview

PhantomAPI includes a layered warm runtime so repeated requests do not always pay the same setup cost.

The current strategy separates:

- static route metadata that is safe to cache
- reusable runtime processes and sessions
- live business state that must still be read at request time

## Current Runtime Shape

```text
process start
    |
    +-- eager app-server startup
    +-- scan endpoint warmStart metadata
    +-- prewarm cache-only endpoints
    +-- prewarm exec-session endpoints only when explicitly readonly-safe

request
    |
    +-- contract/schema cache
    +-- instruction-bundle cache
    +-- warm app-server attempt
    +-- cold exec resume fallback
    +-- fresh exec only when no reusable session exists
```

## Acceleration Layers

### Eager Warm App-Server Startup

When warm mode is enabled, PhantomAPI starts `codex app-server --listen stdio://` during app startup instead of waiting for the first request.

This removes part of the first-hit process startup cost.
The startup warm path is now hosted through the standard ASP.NET Core background-service lifecycle instead of ad hoc lifetime callbacks.

### Compiled Instruction-Bundle Cache

The runtime compiles a route-specific instruction bundle per `app:endpoint`.

That bundle can include:

- framework runtime law
- app instructions
- endpoint instructions
- route-relevant static metadata

This avoids rediscovering the same static structure on every request.

### Contract And Derived Schema Cache

PhantomAPI caches the endpoint response contract and the derived output schema per route.

That means the API boundary does not need to repeatedly reparse the endpoint markdown just to recover the same contract shape.

### Persistent Exec Session Pool

Successful `codex exec` runs can be stored and later resumed through `codex exec resume`.

The reusable session key includes route and runtime profile information so incompatible sessions are not mixed together.

### Endpoint-Declared Warm Metadata

Endpoint markdown can declare startup warm behavior in frontmatter.

Example:

```md
---
warmStart: cache-only
warmupRequest: .examples/login.json
readOnlyWarmup: false
---
```

Supported warm modes:

- `cache-only`
  precompile route metadata and instruction context without executing a real request
- `exec-session`
  create a reusable `codex exec resume` session only when the endpoint explicitly declares a safe readonly warmup request

## Safety Rule: Cache Static Shape, Not Live State

Instruction files define:

- rules
- contracts
- operational policy
- response shape

Concrete persisted facts still must be read from live state under `data/apps/<app>`.

That means:

- entities and stored business data are not treated as preloadable truth
- write endpoints should generally stay on `cache-only`
- readonly `exec-session` warmup should be opt-in and explicit

## Practical Flow

```text
startup
  -> warm app-server process
  -> discover warmStart metadata
  -> prewarm safe route metadata

request
  -> use cached contract/schema when available
  -> use cached instruction bundle when available
  -> try warm app-server
  -> if needed, resume existing exec session
  -> fall back to fresh exec only as a last resort
```

## Related Docs

- [Operating Model](operating-model.md)
- [Framework Features](framework-features.md)
- [Getting Started](getting-started.md)
