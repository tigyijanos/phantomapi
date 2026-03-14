# Request Governance

The framework treats every request as an operational event, not just an input blob.

Governance rules:

- every request should carry or generate a correlation id
- if the client sends `correlationId`, preserve it
- if the client does not send `correlationId`, generate one before writing observability outputs
- if the client sends an `idempotencyKey`, preserve it in observability outputs
- if an endpoint or app explicitly supports dry-run behavior and the request asks for it, evaluate without mutating state
- never perform hidden side effects that are not justified by endpoint rules and storage rules
- if state mutation is attempted, write observability outputs that make the attempt traceable

Write-discipline rules:

- authenticate before side effects unless the endpoint is explicitly public
- validate the request shape before state mutation
- persist state before claiming success
- if persistence fails, return a contract-shaped failure and record the incident
