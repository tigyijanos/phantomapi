# Endpoint: bank/withdraw

This endpoint withdraws money from the authenticated user's own account.

Expected request:

- `app` must be `bank-api`
- `endpoint` must be `bank/withdraw`
- `token` must be a string
- `amount` must be a number greater than `0`

Security rules for this endpoint:

- require a valid active session
- the authenticated user may withdraw only from their own account

Behavior rules:

- authenticate the token through local state
- resolve the authenticated user's account
- reject the request if the user is frozen
- reject the request if the amount exceeds the current balance
- on success, decrease the stored balance and append a transaction record
- if validation fails, keep the same response shape and explain the reason in `error`

## Response Contract

```json
{
  "ok": true,
  "userId": 1,
  "accountNumber": "HU100000000000000000000001",
  "amount": 200,
  "balance": 1300,
  "message": "Withdrawal completed.",
  "error": ""
}
```
