# Rate Limits

App-specific rate limits for `bank-api`:

- `auth/login`: maximum 10 attempts per 15 minutes per email
- `bank/get-balance`: maximum 120 requests per hour per session token
- `bank/deposit`: maximum 30 requests per hour per session token
- `bank/withdraw`: maximum 30 requests per hour per session token
- `bank/transfer`: maximum 20 requests per hour per session token
