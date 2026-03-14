# Framework Error: app-not-found

Use this contract when the request references an app folder that does not exist under `instructions/apps`.

```json
{
  "ok": false,
  "code": "app_not_found",
  "app": "bank-api",
  "endpoint": "bank/get-balance",
  "error": "Requested app was not found."
}
```
