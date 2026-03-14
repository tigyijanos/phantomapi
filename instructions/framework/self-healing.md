# Framework Self-Healing

Self-healing is controlled recovery under explicit rules.
It includes both runtime repair and instruction repair.
It is not permission to invent missing business truth.

Self-healing goals:

- preserve request handling when safe recovery is possible
- restore non-authoritative operational surfaces automatically
- repair only reconstructible app artifacts
- diagnose when the real defect lives in the instruction system
- repair narrow instruction defects when governance allows it
- surface degraded or unrecoverable conditions through observability and contract-shaped errors

Healing classes:

- framework surface healing
- observability sink healing
- reconstructible app artifact healing
- app-declared storage repair
- instruction-system healing
- unrecoverable state protection

Framework healing rules:

- you may recreate missing framework directories and non-authoritative files under `data/framework`
- you may recreate missing `data/framework/incidents/open.json` as `{"openIncidents":[]}`
- you may recreate missing append-only observability files as empty files
- you may normalize `data/framework/metrics/counters.json` to the expected numeric shape when it is missing or malformed
- preserve prior observability history when possible instead of deleting it just to produce a cleaner file
- never rewrite authoritative app state from framework rules alone

Instruction healing rules:

- if repeated or blocking failure is more likely caused by insufficient, contradictory, misplaced, or missing instructions, diagnose the instruction system itself
- when the root cause is most likely in the instructions, you may patch the selected app or the directly relevant framework files
- every instruction patch must be minimal, localized, reversible, and logged
- broad rewrites are not self-healing; they are redesign and should not happen implicitly during one request
- do not modify runtime instructions unless the expected benefit is clearer than the risk of drift

App healing rules:

- if a request reads or writes app state, read the app storage instructions and app repair policy when present
- if an app exposes app-level self-healing configuration, respect its mutation scope and patch preferences
- only perform app-state repairs that are explicitly declared in the selected app package
- only repair reconstructible fields or records, such as session collections or other disposable runtime artifacts
- never reconstruct balances, users, tasks, accounts, transactions, or other authoritative business records from guesswork
- never claim a successful business operation unless the relevant write succeeded after any permitted repair

Decision order:

1. classify the failure
2. determine whether the affected artifact is authoritative, reconstructible, or instructional
3. prefer the smallest safe repair over a broad rewrite
4. if instruction-root-cause is likely, read the change-governance, validation-loop, and rollback-policy rules
5. record the repair attempt in traces, metrics, incidents, and self-healing logs when applicable
6. retry the original operation once only when the repair succeeded and retry is safe
7. if the request is still unsafe, return the best contract-shaped error available

Degraded-mode rules:

- business responses should continue when a non-critical observability sink is unavailable and the main operation is still safe
- degraded operation must still be visible through the surviving observability surfaces
- self-healing must not add undeclared fields to the endpoint response
- if a repair changes only future behavior, do not pretend that the current request was fully healed
