# Endpoint: tasks/create

This endpoint creates a new task for the authenticated user.

Expected request:

- `app` must be `task-board`
- `endpoint` must be `tasks/create`
- `token` must be a string
- `title` must be a non-empty string
- `description` may be an empty string or a string with content

Security rules for this endpoint:

- require a valid active session
- the authenticated user may create tasks only for themselves

Behavior rules:

- authenticate the token through local state
- validate that `title` is not empty
- generate a new unique `taskId`
- persist the new task in local state
- if validation fails, keep the same response shape and explain the reason in `error`

## Response Contract

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "ok": { "type": "boolean" },
    "taskId": { "type": "integer" },
    "userId": { "type": "integer" },
    "title": { "type": "string" },
    "description": { "type": "string" },
    "status": { "type": "string" },
    "error": { "type": "string" }
  },
  "required": ["ok", "taskId", "userId", "title", "description", "status", "error"],
  "additionalProperties": false
}
```
