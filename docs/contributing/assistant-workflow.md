# Assistant Workflow

This is the default repo workflow for Copilot, Codex, or any assistant making changes in this repository.

## Default Flow

1. Start from fresh `main`.
   Run:

   ```powershell
   git fetch origin
   git switch main
   git pull --ff-only origin main
   ```

2. Create a feature branch for the task.
   Example:

   ```powershell
   git switch -c codex/<task-name>
   ```

3. Do the work on the feature branch only.
   Keep backend code under `src/PhantomApi`, helper automation under `scripts`, and runtime assets under repo-root `instructions/` plus `data/`.

4. Validate before committing.
   Default backend verification:

   ```powershell
   dotnet build src/PhantomApi/PhantomApi.csproj
   ```

   If the change touches scripts, Docker, or docs, check those surfaces too.

5. If `main` moved while you were working, merge it into the branch before updating the PR.
   Preferred flow:

   ```powershell
   git fetch origin
   git merge origin/main
   ```

6. Commit with a clear message.
   If an assistant materially contributed, include a co-author trailer.

   For Codex:

   ```text
   Co-authored-by: Codex <codex@openai.com>
   ```

7. Push the branch and open or update the PR.

8. Repeat the same cycle if review feedback arrives:
   update from `origin/main` when needed, change only the branch, re-verify, commit, push.

## Rules Of Thumb

- Do not work directly on `main`.
- Avoid `force push`, `reset --hard`, or other history rewrites on published branches unless a maintainer explicitly asks for it.
- Prefer merge from `origin/main` over rebasing a branch that already has an open PR or shared review state.
- Use non-interactive git commands.
- Keep repo workflow instructions separate from backend runtime law.

## What Lives Where

- `src/PhantomApi/`
  backend application code, project file, appsettings, Dockerfile
- `instructions/`
  backend runtime law and app packages
- `data/`
  runtime state and observability output
- `scripts/`
  local helper scripts and benchmark automation
- `.github/copilot-instructions.md`
  short repo-wide assistant guidance
- `AGENTS.md`
  repo workflow rules plus the scoped runtime-agent section

## When To Deviate

Break the default flow only when the task explicitly calls for it.

Examples:

- hotfix or emergency branch naming conventions
- maintainer-requested rebase or force-push
- release or versioning work with a separate branch model

If the task is unusual, document the deviation in the PR body.
