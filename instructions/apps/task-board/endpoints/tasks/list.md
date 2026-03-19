# Endpoint: tasks/list

This endpoint lists the authenticated user's own tasks.

Expected request:

- `app` must be `task-board`
- `endpoint` must be `tasks/list`
- `token` must be a string

Security rules for this endpoint:

- require a valid active session
- the authenticated user may read only their own tasks

Behavior rules:

- authenticate the token through local state
- return only tasks whose `ownerUserId` matches the authenticated user
- if authentication fails, keep the same response shape and explain the reason in `error`

## Response Contract

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "ok": { "type": "boolean" },
    "userId": { "type": "integer" },
    "tasks": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "taskId": { "type": "integer" },
          "title": { "type": "string" },
          "description": { "type": "string" },
          "status": { "type": "string" }
        },
        "required": ["taskId", "title", "description", "status"],
        "additionalProperties": false
      }
    },
    "error": { "type": "string" }
  },
  "required": ["ok", "userId", "tasks", "error"],
  "additionalProperties": false
}
```
