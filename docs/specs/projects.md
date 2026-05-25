# Projects

Manages projects, the top-level namespace for issues and labels. Any authenticated user can create a project; only the owner can update or delete it. Projects are identified publicly by a URL-friendly slug.

## Data Model

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| Project | `Id: long`, `Slug`, `Name`, `Description?`, `OwnerId: long`, `CreatedAt`, `UpdatedAt` | Unique index on `Slug`. `Name` max 100. `Description` max 2000. Public `Id` is Crockford Base32. Owns `Issues` and `Labels` collections. |

## API Endpoints

All endpoints require `RequireUser` authorization (`X-Bff-Secret` + `X-User-Id`).

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/projects` | Create a new project. Returns 201 with the project resource. |
| GET | `/api/projects` | List all projects with token pagination and optional prefix search. |
| GET | `/api/projects/{slug}` | Get a single project by slug. Returns 404 if not found. |
| GET | `/api/projects/slug-availability` | Check whether a slug is valid and not yet taken. |
| PUT | `/api/projects/{slug}` | Update `name` and `description`. Owner only. |
| DELETE | `/api/projects/{slug}` | Delete a project. Owner only. Returns 204. |

## Key Behaviors

- `Slug` is normalized to lower-case on create and is immutable; only `Name` and `Description` can be updated.
- Slug format: lowercase alphanumeric, hyphens allowed in the interior, 3–50 characters total (regex `^[a-z0-9](?:[a-z0-9-]{1,48}[a-z0-9])?$`).
- Creating a project with a slug that is already taken returns 409 with `projects:project:slug:already_exists`.
- Update and Delete are restricted to the project owner; any other authenticated user receives 403.
- `Name` and `Description` values are trimmed on write.
- List is ordered by `CreatedAt DESC`, then `Id DESC` as a tiebreaker.
- List supports a prefix search (`q`) matched case-insensitively against both `Name` and `Slug`.
- `slug-availability` returns `{ slug, available: bool, reason?: "invalid_format" | "taken" }` — never 4xx; validation errors surface in the `reason` field.

## Validation Rules

| Field | Create | Update |
|-------|--------|--------|
| `name` | required, ≤100 chars | required, ≤100 chars |
| `slug` | required, valid format | — (immutable) |
| `description` | optional, ≤2000 chars | optional, ≤2000 chars |

## Error Codes

| Code | Status | Endpoint |
|------|--------|----------|
| `projects:project:name:required_or_too_long` | 400 | Create, Update |
| `projects:project:slug:invalid_format` | 400 | Create |
| `projects:project:description:too_long` | 400 | Create, Update |
| `projects:project:slug:already_exists` | 409 | Create |
| `projects:project:not_found` | 404 | Get, Update, Delete |
| `projects:project:edit:forbidden` | 403 | Update |
| `projects:project:delete:forbidden` | 403 | Delete |
