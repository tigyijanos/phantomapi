# Framework Structure

PhantomAPI is organized as a framework plus multiple apps.

Framework folder:

- `instructions/framework/engine.md`
- `instructions/framework/structure.md`
- `instructions/framework/app-package.md`
- `instructions/framework/feature-catalog.md`
- `instructions/framework/capability-model.md`
- `instructions/framework/contract-discipline.md`
- `instructions/framework/request-governance.md`
- `instructions/framework/error-policy.md`
- `instructions/framework/request-lifecycle.md`
- `instructions/framework/generic-runtime.md`
- `instructions/framework/generic-security.md`
- `instructions/framework/rate-limits.md`
- `instructions/framework/observability.md`
- `instructions/framework/reliability.md`
- `instructions/framework/self-healing.md`
- `instructions/framework/errors/*.md`

App folder shape:

- `instructions/apps/<app>/app.md`
- `instructions/apps/<app>/entities/*.md`
- `instructions/apps/<app>/storage/*.md`
- `instructions/apps/<app>/config/*.md`
- `instructions/apps/<app>/endpoints/**/*.md`
- `instructions/apps/<app>/.examples/*.json`

How to interpret this structure:

- framework files describe cross-app behavior and conventions
- app files describe a specific software product
- entity files explain domain types and app-level rules attached to those types
- endpoint files explain request handling, endpoint-level security, and the response contract
- storage files explain where real state lives and how it may be changed
- storage repair policy files explain which repairs are safe and which state is authoritative
- config files hold app options such as rate limits and capabilities
- example files are request examples local to one app and should stay next to that app
