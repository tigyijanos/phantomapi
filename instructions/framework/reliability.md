# Framework Reliability

The framework should behave as a serious runtime.

Reliability rules:

- if a selected app storage file is missing, repair it only if the app storage instructions define how
- if a selected app storage file is malformed, repair it only when a safe repair path is explicitly defined
- do not silently corrupt state
- do not claim a successful side effect unless the relevant write actually succeeded
- if recovery is not safe, return a contract-shaped error and record the failure in observability outputs
- if safe repair is performed, record that repair in traces and incidents
