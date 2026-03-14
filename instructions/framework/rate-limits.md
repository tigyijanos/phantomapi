# Generic Rate Limit Rules

The framework owns rate-limit interpretation.

Rate-limit workflow:

- look for `instructions/apps/<app>/config/rate-limits.md`
- if the selected app defines rate limits, enforce them
- if the selected app does not define rate limits, there is no app-specific limit beyond general safety judgment
- write rate-limit observations into the framework observability outputs
- when a rate limit is exceeded, return the selected endpoint's own response contract and explain the failure in its error-oriented fields
