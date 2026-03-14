# Endpoint: bank/get-balance

This endpoint returns the current balance of the authenticated user's own account.

Expected request:

- `app` must be `bank-api`
- `endpoint` must be `bank/get-balance`
- `token` must be a string

Security rules for this endpoint:

- require a valid active session
- the authenticated user may read only their own account

Behavior rules:

- authenticate the token through local state
- resolve the authenticated user's account
- return the current stored balance
- if authentication fails or the account cannot be resolved, keep the same response shape and explain the reason in `error`

## Response Contract

```json
{
  "ok": true,
  "userId": 1,
  "accountNumber": "HU100000000000000000000001",
  "currency": "HUF",
  "balance": 1500,
  "error": ""
}
```
