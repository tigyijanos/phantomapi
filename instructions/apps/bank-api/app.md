# App: bank-api

This app is a banking system handled through PhantomAPI.

App identity:

- app id: `bank-api`
- storage model: local JSON state
- state path: `data/apps/bank-api/state.json`
- currency: `HUF`
- account model: one bank account per user
- public endpoint: `auth/login`
- example requests: `instructions/apps/bank-api/.examples/*.json`

App reading hints:

- read the selected endpoint first
- then read only the entities and storage files needed for that endpoint
- account balance rules live with the bank account and transaction entities
- endpoint-level authentication and authorization rules live inside each endpoint file
