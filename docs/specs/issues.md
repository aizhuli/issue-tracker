# Issues

Issues are the primary work items in a project. Each issue belongs to a project and tracks a unit of work from creation through completion, with optional AI-assisted triage and PR review.

## Data Model

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| Issue | `id` (Base32), `number` (int), `projectId`, `title`, `description?`, `status`, `priority`, `assigneeId?`, `reporterId`, `acceptanceCriteria?`, `createdAt`, `updatedAt`, `closedAt?` | `number` is per-project sequential, starting at 0 |
| IssueLabel | `issueId`, `labelId` | Join table; M:N between Issue and Label |

**Enums:**
- `status`: `backlog` | `todo` | `in-progress` | `in-review` | `done`
- `priority`: `low` | `medium` | `high` | `urgent`

**Response shapes:**
- `IssueFull` — full detail: all fields including `description`, `acceptanceCriteria`, `reporter`, `labels[]`, `commentCount`, timestamps
- `IssueSummary` — list item: `id`, `number`, `displayKey`, `title`, `status`, `priority`, `assignee?`, `labels[]`, `commentCount`, `updatedAt`
- `displayKey` is formatted as `{slug}_{number}` (e.g. `myapp_5`)

## API Endpoints

| Method | Path | Authorization | Description |
|--------|------|---------------|-------------|
| `POST` | `/api/projects/{slug}/issues` | RequireUser | Create an issue; returns `IssueFull` (201) |
| `GET` | `/api/projects/{slug}/issues` | RequireUser | List issues with filters; returns `PageResponse<IssueSummary>` |
| `GET` | `/api/projects/{slug}/issues/{number}` | RequireUser | Get a single issue; returns `IssueFull` |
| `PUT` | `/api/projects/{slug}/issues/{number}` | RequireUser | Full update (all fields); returns `IssueFull` |
| `PATCH` | `/api/projects/{slug}/issues/{number}/status` | RequireUser | Change status only; returns `IssueFull` |
| `PATCH` | `/api/projects/{slug}/issues/{number}/assignee` | RequireUser | Change or clear assignee; returns `IssueFull` |
| `DELETE` | `/api/projects/{slug}/issues/{number}` | RequireUser | Delete issue; returns 204 |

### List query parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `status` | string (required) | Filter by status (exact match) |
| `priority` | string[] | Filter by one or more priorities |
| `assignee` | string[] | Filter by assignee Base32 IDs; use `"unassigned"` to include issues with no assignee |
| `q` | string | Case-insensitive substring search across `title` and `description` |
| `pageToken` | string | AIP-158 continuation token |
| `maxPageSize` | int | Max items per page |

List results are ordered by `createdAt DESC`, then `id DESC`.

## Key Behaviors

- New issues default to `status = backlog`, `priority = medium`, `assignee = null`.
- `reporter` is set from the authenticated user on creation and never changed.
- Issue number is per-project sequential, allocated atomically via `UPDATE … RETURNING`.
- `closedAt` is set to the current timestamp when transitioning into `done`; cleared on any transition out of `done`. This applies to both `PATCH /status` and `PUT`.
- Status transitions are unrestricted — any status can move to any other status (no FSM enforcement).
- Labels must belong to the issue's project; supplying an ID from another project returns `issues:issue:labels:not_in_project`.
- `PUT` replaces the full label set atomically (remove all, insert new).
- `PATCH /assignee` with `null` clears the assignee.
- Delete is restricted to the issue's reporter or the project owner; all other callers receive `issues:issue:delete:forbidden`.
- `title` and `description` values are trimmed of leading/trailing whitespace on write.

## Validation

| Field | Rule | Error code |
|-------|------|------------|
| `title` | Required, max 200 chars | `issues:issue:title:required_or_too_long` |
| `description` | Max 10 000 chars | `issues:issue:description:too_long` |
| `acceptanceCriteria` | Max 10 000 chars | `issues:issue:acceptance_criteria:too_long` |
| `status` | Must be a valid status value | `issues:issue:status:invalid` |
| `priority` | Must be a valid priority value | `issues:issue:priority:invalid` |
| `labelIds` | Max 20 labels | `issues:issue:labels:too_many` |
| `assigneeId` | Must decode as valid Base32 and resolve to an existing user | `issues:issue:assignee:not_found` |
