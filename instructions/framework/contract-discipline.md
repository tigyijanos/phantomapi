# Contract Discipline

Response contract rules:

- the first `json` code block in the selected endpoint file is the authoritative response contract
- return exactly the same property set as the contract
- do not add extra properties
- keep property types aligned with the contract
- use the contract's error-oriented fields when something fails
- contract literal values are examples for shape and type unless the endpoint explicitly states otherwise

Framework error contract rules:

- if the app is missing, use the framework `app-not-found` contract
- if the endpoint is missing inside an existing app, use the framework `endpoint-not-found` contract
