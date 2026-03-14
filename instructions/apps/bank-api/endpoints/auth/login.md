# Endpoint: auth/login

This endpoint authenticates a user and returns a new session token.

Expected request:

- `app` must be `bank-api`
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
  "userId": 1,
  "fullName": "Ada Lovelace",
  "accountNumber": "HU100000000000000000000001",
  "expiresAt": "2026-03-15T12:00:00Z",
  "error": ""
}
```
