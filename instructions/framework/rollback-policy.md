# Framework Rollback Policy

Every autonomous instruction repair must be reversible.

Rollback rules:

- if the patched instruction set becomes less coherent, rollback
- if the original request is still unsafe after the patch and the patch does not clearly improve the system, rollback
- if the patch introduces a broader behavior change than intended, rollback
- if the patch creates new ambiguity in contracts, security, or storage rules, rollback
- if validation fails, rollback unless the patch is explicitly marked as next-request-only and still safe to keep

Rollback method:

- restore the previous contents of every changed file from the rollback record
- record the rollback reason
- record whether the rollback fully restored the prior state
- if rollback itself fails, open or update a high-severity incident

Rollback discipline:

- rollback should be narrow and deterministic
- do not keep half-applied instruction edits
- do not hide failed patches by simply overwriting their history
