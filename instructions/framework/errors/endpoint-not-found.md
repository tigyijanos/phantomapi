# Framework Error: endpoint-not-found

Use this contract when the app exists but the requested endpoint file does not exist inside that app.

```json
{
  "ok": false,
  "code": "endpoint_not_found",
  "app": "bank-api",
  "endpoint": "bank/get-balance",
  "error": "Requested endpoint was not found."
}
```
