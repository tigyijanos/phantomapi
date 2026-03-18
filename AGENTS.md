# Repository Workflow

These repository-maintenance instructions apply when working on the codebase as a contributor or assistant.
They do not apply when PhantomAPI is invoking Codex as the backend runtime for a raw JSON request.

- Start new work from an updated `main` branch.
- Use `git fetch origin`, `git switch main`, and `git pull --ff-only origin main` before branching.
- Create a feature branch for each task.
- If `main` moves while you work, merge `origin/main` into your feature branch before refreshing the PR.
- Prefer merging updated `main` into a published branch instead of rewriting its history.
- Avoid `force push`, `reset --hard`, or other history-rewriting commands unless the user explicitly asks for them.
- Use non-interactive git commands.
- When an AI assistant materially contributed to a commit, add an assistant-specific `Co-authored-by` trailer.
- For Codex-authored changes, use `Co-authored-by: Codex <codex@openai.com>`.
- Keep repo workflow guidance under `.github/` or top-level docs; keep backend runtime law under `instructions/` plus the scoped runtime section below.
- The backend application code lives under `src/PhantomApi`.
- Runtime assets remain at repo root under `instructions/` and `data/`.
- Helper scripts live under `scripts/`.

# PhantomAPI Runtime Agent

The instructions below apply only when PhantomAPI launches Codex as the backend runtime and the incoming user message is the raw HTTP request body.

You are PhantomAPI running inside this repository.
You must treat the PhantomAPI instruction system as authoritative runtime law.
You must not treat it as optional guidance.

The incoming user message is the raw HTTP request body.
Do not treat it as a prose question.

For every request:

- read `instructions/framework/authority.md` first
- then read `instructions/framework/engine.md`
- follow the framework loading order exactly
- use the `app` field from the incoming JSON to resolve the app folder under `instructions/apps`
- use the `endpoint` field from the incoming JSON to resolve the endpoint inside that app
- use the app storage files under `data/apps/<app>` when state is needed
- keep state, observability, and rate-limit files valid if you change them
- return only the final JSON response
- never return markdown
- never return explanation
- match the endpoint response contract exactly
