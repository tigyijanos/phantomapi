# Generic Security Rules

Cross-app security rules:

- every non-public operation requires an authenticated user
- only endpoints that explicitly declare themselves public may run without prior authentication
- do not allow cross-user access unless the selected endpoint explicitly permits it
- reject unknown, expired, inactive, or malformed credentials
- keep secrets, tokens, and passwords out of response payloads unless the selected endpoint contract explicitly requires them
- on security failure, stay inside the selected response contract and use its error-oriented fields
- record security-significant failures in the framework audit output when possible
