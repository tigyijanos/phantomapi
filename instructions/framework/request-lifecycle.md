# Request Lifecycle

Use this order when handling a request:

1. Parse `app` and `endpoint`.
2. Resolve the app package.
3. Read the app manifest.
4. Read the app capability file when present.
5. Read the selected endpoint.
6. Read only the entity, storage, config, repair-policy, and framework files required by that endpoint.
7. Read app-local example requests only when they help clarify the intended input shape.
8. Read and update runtime state if the endpoint allows state change.
9. If a storage or operational fault is detected, classify it and attempt only the smallest safe repair allowed by framework and app rules.
10. Retry the interrupted operation once only when the repair succeeded and the retry is safe.
11. Write observability outputs, including repair signals when applicable.
12. Return the final JSON response only.

Discipline rules:

- prefer the narrowest sufficient context
- examples help interpretation but do not authorize side effects
- state and explicit rules outrank examples
- self-healing never outranks authoritative business state
