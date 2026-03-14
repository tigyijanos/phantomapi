# Entity: Session

The `Session` entity represents an authenticated task-board session.

Fields and meaning:

- `token`: opaque authentication token
- `userId`: authenticated user id
- `issuedAt`: token creation time
- `expiresAt`: token expiry time
- `isActive`: whether the session may still be used
