---
warmStart: cache-only
warmupRequest: .examples/login.json
readOnlyWarmup: false
---

# Endpoint: auth/login

This endpoint authenticates a user and returns a new session token.

Expected request:

- `app` must be `task-board`
- `endpoint` must be `auth/login`
- `email` must be a string
- `password` must be a string

Security rules for this endpoint:

- this endpoint is public
- do not leak stored passwords

Behavior rules:

- find the user by email in local state
- the password must match the stored value for that user
- the user must be active
- if login succeeds, create a new active session token with an expiry time and persist it
- if login fails, keep the same response shape and explain the reason in `error`

## Response Contract

```json
{
  "ok": true,
  "token": "session_123",
  "userId": 10,
  "fullName": "Taylor Example",
  "expiresAt": "2026-03-15T12:00:00Z",
  "error": ""
}
```
