# Self-Healing Profile

This file defines how `task-board` may heal its own instruction package.

Profile:

- healing mode: governed autonomous instruction repair
- preferred patch scope: selected endpoint first, then relevant entity or storage file, then app config, then app manifest
- state repair reference: `instructions/apps/task-board/storage/repair-policy.md`

Allowed automatic instruction changes:

- clarify endpoint validation rules
- add missing task ownership or session requirements
- clarify task entity rules when endpoint behavior and entity language drift apart
- clarify storage interpretation or repair policy when the existing text is incomplete
- clarify app config such as rate limits or capability descriptions when they are contradicted by the endpoint package

High-risk automatic changes:

- changing response contracts
- changing login behavior
- changing ownership semantics across all tasks

Forbidden automatic changes:

- do not edit unrelated apps
- do not edit framework files unless the fault is clearly generic and not app-specific
- do not edit historical task data to make instructions appear correct
- do not weaken authentication or authorization requirements to make a request succeed

Validation hints:

- created tasks must remain attributable to one authenticated user
- task listing and task creation rules should stay coherent around ownership
- if a contract change was necessary, treat it as next-request-effective unless the current hard guard still matches
