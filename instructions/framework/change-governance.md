# Framework Change Governance

PhantomAPI may repair its own instruction system when the most likely root cause lives in the instruction system.

Instruction-root-cause signals:

- repeated failure with the same request shape
- contradictory rules across endpoint, entity, storage, or config files
- missing edge-case handling that blocks an otherwise valid request
- missing security or validation instruction that makes the runtime ambiguous
- response contract instructions that are internally inconsistent with the rest of the selected app package
- framework loading or package-structure gaps that prevent correct interpretation

Change classes:

- localized clarification
- localized policy correction
- localized contract correction
- framework convention correction
- blocked high-risk change

Governance rules:

- prefer the smallest file set that can plausibly fix the root cause
- prefer selected app files before generic framework files
- prefer endpoint files before entity, storage, config, or app manifest files when the problem is endpoint-local
- keep wording changes minimal and operationally clear
- preserve backward-compatible response contracts whenever possible
- do not modify unrelated apps
- do not modify documentation outside the runtime instruction system during hot-path self-healing
- do not modify `zerobackend-phantomapi-concept.md`
- do not perform broad rewrites when a local patch is sufficient

High-risk change rules:

- changing an endpoint response contract is high risk and should happen only when the contract is the likely root cause and no narrower fix exists
- if a contract is changed during a request, remember that the API layer already resolved the contract before the CLI call, so the new contract affects later requests, not the current guard
- do not rewrite generic security posture, authentication model, or money-movement invariants from one ambiguous failure
- if a required change is too broad or too uncertain, record the diagnosis and fail safely instead of improvising

Patch discipline:

- capture the pre-change file contents in the self-healing rollback record before editing
- record why the selected files were changed
- state the expected effect of the patch
- after the patch, run the validation loop before trusting the new instructions
