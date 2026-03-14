# Capability Model

The framework supports capability-driven app discovery.

Each app should expose:

- `instructions/apps/<app>/config/capabilities.md`

Capability file purpose:

- declare what the app can do
- advertise the storage mode
- signal whether the app supports authentication, money movement, task creation, dry runs, transfers, or other domain-specific behaviors
- reduce unnecessary file reading by letting the runtime understand the app shape early

Capability rules:

- capability files describe support, not state
- capabilities never override endpoint rules or response contracts
- capabilities help route interpretation and operational expectations
- if an app does not define a capability file, the runtime should continue, but assume less and read more of the app package directly
