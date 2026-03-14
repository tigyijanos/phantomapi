# Entity: User

The `User` entity represents a person who can authenticate and own one bank account.

Fields and meaning:

- `userId`: stable numeric identifier
- `fullName`: display name
- `email`: login identifier
- `password`: password for the app login flow
- `isActive`: whether the user can use the app
- `isFrozen`: whether money movement must be blocked
- `accountNumber`: the single bank account owned by the user

Rules tied to this entity:

- every user owns exactly one bank account
- inactive users cannot authenticate
- frozen users cannot move money
