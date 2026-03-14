# Storage Repair Policy

This file defines the only safe self-healing actions for `task-board`.

Authoritative state:

- `users`
- `tasks`

Reconstructible state:

- `sessions`

Allowed repairs:

- if the JSON file is valid and the top-level `sessions` field is missing, create it as an empty array
- if the JSON file is valid and the top-level `sessions` field is not an array, replace it with an empty array
- if the JSON file is valid and a session record is malformed, remove only the malformed session record

Forbidden repairs:

- do not recreate a missing full state file
- do not recreate missing `users` or `tasks`
- do not invent task ownership, task content, or user identity
- do not continue a state-changing task operation if authoritative state is missing or malformed

Failure handling:

- if the state file is missing, return a storage-shaped failure and open or update an incident
- if the whole JSON document is malformed and cannot be safely interpreted, return a storage-shaped failure and open or update an incident
