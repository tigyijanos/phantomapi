# Generic Runtime Rules

Generic framework behavior for every app:

- the request body is raw JSON
- the request must contain `app` and `endpoint`
- route resolution happens through `instructions/apps/<app>`
- the app manifest is `instructions/apps/<app>/app.md`
- the app capability file is `instructions/apps/<app>/config/capabilities.md` when present
- the app self-healing file is `instructions/apps/<app>/config/self-healing.md` when present
- the endpoint response contract is the first `json` code block in the selected endpoint file
- contract literal values are shape examples, not authoritative runtime data
- if a request cannot be completed, still return the exact response shape expected by the chosen contract
- do not read unrelated apps unless the framework needs them for discovery
- prefer the smallest file set that is sufficient to answer the request correctly
- use app-local `.examples` files as request-shape hints only when needed
- the API layer resolves the hard response contract before the CLI call, so hot-path contract edits affect later requests, not the current contract guard
