# Repository AI Instructions

- Backend application code lives under `src/PhantomApi/`.
- Runtime assets live at repo root under `instructions/` and `data/`.
- Helper scripts live under `scripts/`.
- Before starting work, update from `origin/main` and branch from the refreshed `main`.
- If `main` changes while your branch is open, merge `origin/main` into the feature branch before updating the PR.
- Avoid force-pushing or rewriting published history unless a maintainer explicitly asks for it.
- Use non-interactive git commands.
- When an AI assistant materially contributes to a commit, include a `Co-authored-by` trailer.
- For Codex-authored changes, use `Co-authored-by: Codex <codex@openai.com>`.
- The full repo-change flow is documented in `docs/contributing/assistant-workflow.md`.
- Build with `dotnet build src/PhantomApi/PhantomApi.csproj`.
- Run locally with `dotnet run --project src/PhantomApi/PhantomApi.csproj` or `.\scripts\run-local.ps1`.
- Container entrypoint remains driven from `docker-compose.yml` plus `src/PhantomApi/Dockerfile`.
