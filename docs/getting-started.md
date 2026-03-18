# Getting Started

If you prefer to outsource the ceremonial act of starting the app to Copilot, try:

```text
Clone https://github.com/tigyijanos/phantomapi, figure out how to start the app locally, use port 5050 if 5000 is taken, and tell me which URL to hit when the backend becomes sentient.
```

## Local Run

The manual old-school way, for people who still insist on starting their own backend like it is 2024:

Start the API:

```bash
dotnet run
```

If port `5000` is already occupied, use the helper script:

```powershell
.\run-local.ps1 -PreferredPort 5050
```

The script checks whether the preferred port is free, selects the next available port when needed, and launches `dotnet run` there.

Example request:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5050/dynamic-api `
  -ContentType "application/json" `
  -Body (Get-Content instructions/apps/bank-api/.examples/login.json -Raw)
```

## Docker Compose

The repository includes [docker-compose.yml](../docker-compose.yml).

Default containerized startup:

```powershell
docker compose up --build -d
```

Useful follow-up commands:

```powershell
docker compose ps
docker compose logs -f
docker compose down
```

Runtime endpoint:

- `http://localhost:8080/dynamic-api`

Compose persistence mounts:

- `./data -> /app/data`
- `./instructions -> /app/instructions`
- `./AGENTS.md -> /app/AGENTS.md`
- `./.codex -> /root/.codex`

This keeps state, observability output, instruction changes, and Codex authentication across container restarts and rebuilds.

## Codex Authentication In The Container

The API service can start without Codex authentication, but request execution will fail until the `codex` CLI inside the container is logged in.

Preferred headless ChatGPT login:

```powershell
docker exec -it phantomapi codex login --device-auth
docker exec phantomapi codex login status
```

API key login:

```powershell
$env:OPENAI_API_KEY="sk-..."
docker exec -e OPENAI_API_KEY=$env:OPENAI_API_KEY phantomapi sh -lc "printenv OPENAI_API_KEY | codex login --with-api-key"
docker exec phantomapi codex login status
```

Auth persistence location:

- inside the container: `/root/.codex`
- on the host through compose: `./.codex`

Because `./.codex` is ignored by git, the container can persist local Codex credentials without committing them to the repository.

## Configuration

Preferred configuration lives in `appsettings.json` under the `Phantom` section.

Core runtime settings:

- `CliCommand`
  leave empty for the OS-specific default, or set it explicitly
- `Model`
  for example `gpt-5.4` or `gpt-5.3-codex-spark`
- `ReasoningEffort`
  `low`, `medium`, `high`, or `xhigh`
- `CliArgumentsTemplate`
  optional advanced override; if empty, PhantomAPI builds the Codex CLI arguments from `Model` and `ReasoningEffort`
- `CliTimeoutSeconds`
  default is `180`

Warm runtime settings:

- `UseWarmAppServer`
  eagerly starts `codex app-server --listen stdio://` on app startup and reuses it
- `UseExecSessionPool`
  stores successful `codex exec` sessions and later resumes them with `codex exec resume`
- `FallbackToColdExecution`
  falls back automatically when the warm path does not produce a final textual response
- `WarmTurnGraceSeconds`
  how long the warm path is allowed to recover before falling back

Fast-mode settings:

- `FastModeEnabled`
  default fast profile when the request does not explicitly set `fastMode` or `fast`
- `FastModeModel`
  model used for fast mode
- `FastModeReasoningEffort`
  reasoning effort used for fast mode
- `FastModeServiceTier`
  `fast` or `flex` when using the app-server fast path
- `NormalServiceTier`
  optional `fast` or `flex` tier for non-fast requests

Environment variable overrides still work:

- `Phantom__CliCommand`
- `Phantom__Model`
- `Phantom__ReasoningEffort`
- `Phantom__CliArgumentsTemplate`
- `Phantom__CliTimeoutSeconds`
- `Phantom__UseWarmAppServer`
- `Phantom__UseExecSessionPool`
- `Phantom__FallbackToColdExecution`
- `Phantom__WarmTurnGraceSeconds`
- `Phantom__FastModeEnabled`
- `Phantom__FastModeModel`
- `Phantom__FastModeReasoningEffort`
- `Phantom__FastModeServiceTier`
- `Phantom__NormalServiceTier`

Per-request fast-mode toggle:

- set `fastMode: true` in the request body
- or use the shorthand `fast: true`
