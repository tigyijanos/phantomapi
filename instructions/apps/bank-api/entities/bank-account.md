# Entity: Bank Account

The `Bank Account` entity stores the financial state of one user.

Fields and meaning:

- `accountNumber`: unique stable bank account number
- `ownerUserId`: user that owns the account
- `currency`: currency code
- `balance`: current available balance
- `status`: account state such as `active` or `blocked`

Rules tied to this entity:

- every account number must be unique
- the minimum allowed balance is `0`
- the maximum allowed balance is `1000000`
- no operation may move the balance below `0`
- no operation may move the balance above `1000000`
- blocked accounts cannot send or receive money
