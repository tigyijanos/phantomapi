# Framework Error Policy

The framework should classify failures explicitly.

Failure classes:

- routing failure
- contract failure
- authentication failure
- authorization failure
- validation failure
- rate-limit failure
- storage failure
- observability failure
- recovery failure
- repair-blocked failure

Error policy rules:

- use framework error contracts for framework-level routing failures
- use endpoint contracts for app-level failures
- keep failures machine-readable through stable codes when possible
- do not leak secrets or internal stack traces in response payloads
- if an internal failure prevents a safe business response, return the best contract-shaped error available and record the event in logs, traces, and incidents when applicable
- if self-healing was attempted and failed, record that explicitly and do not continue with speculative business output
