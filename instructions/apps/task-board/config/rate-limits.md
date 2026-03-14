# Rate Limits

App-specific rate limits for `task-board`:

- `auth/login`: maximum 10 attempts per 15 minutes per email
- `tasks/list`: maximum 120 requests per hour per session token
- `tasks/create`: maximum 40 requests per hour per session token
