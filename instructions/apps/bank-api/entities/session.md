# Entity: Session

The `Session` entity represents an authenticated login context.

Fields and meaning:

- `token`: opaque authentication token
- `userId`: authenticated user id
- `issuedAt`: token creation time
- `expiresAt`: token expiry time
- `isActive`: whether the session may still be used

Rules tied to this entity:

- only active, unexpired sessions may authorize requests
- a session authenticates exactly one user
