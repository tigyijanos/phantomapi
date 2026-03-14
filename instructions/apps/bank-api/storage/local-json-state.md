# Storage: Local JSON State

Use `data/apps/bank-api/state.json` as the authoritative state of this app.

This state stores:

- users
- accounts
- sessions
- transactions

Storage rules:

- read the current file before deciding on a stateful operation
- keep the JSON valid after every write
- if a storage anomaly is detected, read `instructions/apps/bank-api/storage/repair-policy.md`
- on successful login, persist a new session
- on successful deposit, withdraw, or transfer, persist the balance change and the transaction record
- on failed requests, do not mutate state
