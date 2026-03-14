# App Package Convention

Every app should be a self-contained package under `instructions/apps/<app>`.

Required files and folders:

- `app.md`
- `entities/*.md`
- `storage/*.md`
- `config/*.md`
- `endpoints/**/*.md`
- `.examples/*.json`

Package rules:

- app-local examples live inside the app package, not in a shared top-level examples folder
- app-specific business rules belong in app files, preferably close to the entity or endpoint they govern
- endpoint-specific security belongs in the endpoint file
- app packages should describe where their runtime state lives under `data/apps/<app>`
- when an app supports self-healing, the safe repair rules should live under `storage/*.md`
- when an app supports autonomous instruction repair, the mutation scope should be declared under `config/*.md`
