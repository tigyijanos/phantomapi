# Request Lifecycle

Use this order when handling a request:

1. Parse `app` and `endpoint`.
2. Resolve the app package.
3. Read the app manifest.
4. Read the app capability file when present.
5. Read the selected endpoint.
6. Read only the entity, storage, config, repair-policy, self-healing, and framework files required by that endpoint.
7. Read app-local example requests only when they help clarify the intended input shape.
8. Read live runtime state before deciding whenever current entity values matter, and update runtime state if the endpoint allows state change.
9. If a storage, operational, or instruction fault is detected, classify it and attempt only the smallest safe repair allowed by framework and app rules.
10. If the fault is likely caused by the instruction system, diagnose the root cause and patch only the smallest justified instruction surface.
11. Validate every instruction patch before trusting it.
12. Retry the interrupted operation once only when the repair succeeded and the retry is safe.
13. Write observability outputs, including repair signals when applicable.
14. Return the final JSON response only.

Discipline rules:

- prefer the narrowest sufficient context
- examples help interpretation but do not authorize side effects
- state and explicit rules outrank examples
- current persisted facts must come from live runtime files, not from instructional examples or prior turn memory
- self-healing never outranks authoritative business state
- instruction self-healing never outranks contract discipline
