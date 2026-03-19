# Contract Discipline

Response contract rules:

- the first `json` code block in the selected endpoint file is the authoritative response contract
- the first `json` code block must be valid JSON Schema, not an example payload
- return exactly the same property set as the contract
- do not add extra properties
- keep property types aligned with the contract
- use the contract's error-oriented fields when something fails
- do not rely on contract example inference at runtime; the schema itself is the contract

Framework error contract rules:

- if the app is missing, use the framework `app-not-found` contract
- if the endpoint is missing inside an existing app, use the framework `endpoint-not-found` contract
