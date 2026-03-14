# Entity: Transaction

The `Transaction` entity records a balance-changing bank operation.

Fields and meaning:

- `transactionId`: unique transaction id
- `type`: `deposit`, `withdraw`, or `transfer`
- `accountNumber`: primary account affected
- `targetAccountNumber`: secondary account for transfers
- `amount`: moved amount
- `createdAt`: timestamp
- `performedByUserId`: authenticated user that initiated the operation

Rules tied to this entity:

- deposits must have amount greater than `0`
- withdrawals must have amount greater than `0` and cannot exceed the current balance
- transfers must have amount greater than `0`, require a real target account, and cannot exceed the current balance of the source account
- successful balance changes should append a transaction record
