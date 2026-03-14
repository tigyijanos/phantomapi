# App: task-board

This app is a collaborative task tracking system handled through PhantomAPI.

App identity:

- app id: `task-board`
- storage model: local JSON state
- state path: `data/apps/task-board/state.json`
- public endpoint: `auth/login`
- example requests: `instructions/apps/task-board/.examples/*.json`

App reading hints:

- read the selected endpoint first
- task ownership rules live with the task entity
- endpoint-level authentication and authorization rules live inside each endpoint file
