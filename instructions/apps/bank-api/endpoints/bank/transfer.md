# Endpoint: bank/transfer

This endpoint transfers money from the authenticated user's account to another account.

Expected request:

- `app` must be `bank-api`
- `endpoint` must be `bank/transfer`
- `token` must be a string
- `targetAccountNumber` must be a string
- `amount` must be a number greater than `0`

Security rules for this endpoint:

- require a valid active session
- the authenticated user may transfer only from their own account

Behavior rules:

- authenticate the token through local state
- resolve the authenticated user's source account
- resolve the target account from local state
- reject the request if the source user is frozen
- reject the request if the source and target accounts are the same
- reject the request if the amount exceeds the current source balance
- on success, update both balances and append a transaction record
- if validation fails, keep the same response shape and explain the reason in `error`

## Response Contract

```json
{
  "ok": true,
  "userId": 1,
  "sourceAccountNumber": "HU100000000000000000000001",
  "targetAccountNumber": "HU100000000000000000000002",
  "amount": 200,
  "sourceBalance": 1300,
  "message": "Transfer completed.",
  "error": ""
}
```
