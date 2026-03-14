# Self-Healing Profile

This file defines how `bank-api` may heal its own instruction package.

Profile:

- healing mode: governed autonomous instruction repair
- preferred patch scope: selected endpoint first, then relevant entity or storage file, then app config, then app manifest
- state repair reference: `instructions/apps/bank-api/storage/repair-policy.md`

Allowed automatic instruction changes:

- clarify endpoint validation rules
- add missing endpoint-local authorization or precondition rules
- clarify business invariants in `entities/bank-account.md` or `entities/transaction.md`
- clarify storage interpretation or repair policy when the existing text is incomplete
- clarify app config such as rate limits or capability descriptions when they are contradicted by the endpoint package

High-risk automatic changes:

- changing response contracts
- changing login behavior
- changing money-movement invariants
- changing account identity or ownership rules

Forbidden automatic changes:

- do not edit unrelated apps
- do not edit framework files unless the fault is clearly generic and not bank-specific
- do not edit historical data to make instructions appear correct
- do not weaken authentication or authorization requirements to make a request succeed

Validation hints:

- balance may never fall below the declared minimum balance
- balance may never exceed the declared maximum balance
- transfer, withdrawal, and deposit rules must remain mutually coherent
- if a contract change was necessary, treat it as next-request-effective unless the current hard guard still matches
