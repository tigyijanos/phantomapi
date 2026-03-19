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
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "ok": { "type": "boolean" },
    "userId": { "type": "integer" },
    "accountNumber": { "type": "string" },
    "currency": { "type": "string" },
    "balance": { "type": "number" },
    "error": { "type": "string" }
  },
  "required": ["ok", "userId", "accountNumber", "currency", "balance", "error"],
  "additionalProperties": false
}
```
