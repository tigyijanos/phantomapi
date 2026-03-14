# Entity: User

The `User` entity represents a person who can authenticate and work on tasks.

Fields and meaning:

- `userId`: stable numeric identifier
- `fullName`: display name
- `email`: login identifier
- `password`: password for the app login flow
- `isActive`: whether the user can use the app

Rules tied to this entity:

- inactive users cannot authenticate
