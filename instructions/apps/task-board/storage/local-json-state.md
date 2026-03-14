# Storage: Local JSON State

Use `data/apps/task-board/state.json` as the authoritative state of this app.

This state stores:

- users
- sessions
- tasks

Storage rules:

- read the current file before deciding on a stateful operation
- keep the JSON valid after every write
- on successful login, persist a new session
- on successful task creation, append the new task
- on failed requests, do not mutate state
