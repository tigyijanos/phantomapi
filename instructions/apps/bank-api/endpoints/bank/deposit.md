# Endpoint: bank/deposit

This endpoint deposits money into the authenticated user's own account.

Expected request:

- `app` must be `bank-api`
- `endpoint` must be `bank/deposit`
- `token` must be a string
- `amount` must be a number greater than `0`

Security rules for this endpoint:

- require a valid active session
- the authenticated user may deposit only into their own account

Behavior rules:

- authenticate the token through local state
- resolve the authenticated user's account
- check the account balance ceiling before applying the deposit
- on success, increase the stored balance and append a transaction record
- if validation fails, keep the same response shape and explain the reason in `error`

## Response Contract

```json
{
  "ok": true,
  "userId": 1,
  "accountNumber": "HU100000000000000000000001",
  "amount": 200,
  "balance": 1700,
  "message": "Deposit completed.",
  "error": ""
}
```
