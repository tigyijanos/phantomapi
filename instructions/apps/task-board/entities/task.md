# Entity: Task

The `Task` entity represents one work item.

Fields and meaning:

- `taskId`: unique identifier
- `title`: short title
- `description`: longer description
- `status`: current state such as `open` or `done`
- `ownerUserId`: user that owns the task
- `createdAt`: creation time

Rules tied to this entity:

- a task must have a non-empty title
- users may list only their own tasks unless an endpoint explicitly says otherwise
- users may create tasks only for themselves
