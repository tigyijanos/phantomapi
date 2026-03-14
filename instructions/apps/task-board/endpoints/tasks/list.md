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
  "ok": true,
  "userId": 10,
  "tasks": [
    {
      "taskId": 100,
      "title": "Prepare backlog",
      "description": "Collect the next iteration items.",
      "status": "open"
    }
  ],
  "error": ""
}
```
