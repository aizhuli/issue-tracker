# AI PR Reviewer

Tool-using LLM agent that reviews a GitHub pull request against an issue's acceptance criteria and persists the review as a comment authored by the reserved AI system user.

## AI System User

| Field | Value |
|-------|-------|
| `Id` | `1` (constant `SystemUsers.AiUserId`) |
| `Email` | `ai@system` |
| `Name` | `AI` |
| `PasswordHash` | `!` — sentinel; login is impossible |

The AI user is seeded via EF Core `HasData` and is never looked up at runtime. Handlers set `AuthorId = SystemUsers.AiUserId` directly.

## API Endpoints

| Method | Path | Authorization | Description |
|--------|------|---------------|-------------|
| `POST` | `/api/projects/{slug}/issues/{number}/ai/review-pr` | RequireUser | Run the agent and post the review as a comment |

### Request body

```json
{ "pullRequestUrl": "string" }
```

### Response body (`ReviewDto`)

```json
{ "commentId": "Base32", "body": "markdown string" }
```

## GitHub Tools

The agent has four tools backed by `GitHubClient`:

| Tool | GitHub API | Notes |
|------|-----------|-------|
| `fetch_pr_metadata` | `GET /repos/{owner}/{repo}/pulls/{pr}` | title, author, base/head refs, description; caches head SHA |
| `fetch_pr_diff` | Same URL, diff media type | Unified diff, capped at 50 KB; appends `[diff truncated]` |
| `fetch_changed_files` | `GET /repos/{owner}/{repo}/pulls/{pr}/files?per_page=100` | File paths + statuses; appends truncation notice at 100 files |
| `fetch_file_content` | `GET /repos/{owner}/{repo}/contents/{path}?ref={headSha}` | Base64-decoded file content, capped at 20 KB; reuses cached head SHA |

## Key Behaviors

- The project (by slug) and issue (by number) must exist; returns `projects:project:not_found` or `issues:issue:not_found` otherwise.
- The agent loop is bounded to 10 iterations.
- The LLM call is bounded by `Llm:TimeoutSeconds` (shared with Triage, default 30 s); a linked `CancellationTokenSource` enforces it.
- System prompt: when the issue has acceptance criteria, the prompt instructs the agent to evaluate each criterion explicitly. When criteria are absent, the prompt requests a general code quality review.
- Review text is trimmed and persisted as-is (no length cap) as a `Comment` with `AuthorId = SystemUsers.AiUserId`.
- `ListComments` includes `isAiGenerated: bool` on each `CommentDto`; it is `true` when `AuthorId == SystemUsers.AiUserId`.
- GitHub access is unauthenticated by default (public repos). An optional PAT via `GitHub:Token` enables private repo access.
- `GitHubClient` head SHA is cached in-instance after the first `fetch_pr_metadata` call to avoid a redundant API round-trip in `fetch_file_content`.

## Validation

| Field | Rule | Error code |
|-------|------|------------|
| `pullRequestUrl` | Required | `ai:pr_review:pull_request_url:required` |
| `pullRequestUrl` | Must match `https://github.com/{owner}/{repo}/pull/{number}` | `ai:pr_review:pull_request_url:invalid` |

## Error Codes

| Code | HTTP | Trigger |
|------|------|---------|
| `ai:pr_review:pull_request_url:required` | 400 | Empty or missing URL |
| `ai:pr_review:pull_request_url:invalid` | 400 | URL does not match GitHub PR pattern |
| `ai:pr_review:github:fetch_failed` | 502 | GitHub non-2xx response, network failure, or HTTP client timeout |
| `ai:pr_review:llm:unavailable` | 502 | LLM timeout, exception, or null response text |

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `Llm:TimeoutSeconds` | `30` | Per-request timeout (shared with Triage) |
| `GitHub:Token` | — | Optional PAT for private-repo access |

## BFF Route

`POST /api/projects/[slug]/issues/[number]/ai/review-pr` — plain passthrough. Requires an active session; attaches `X-Bff-Secret` + `X-User-Id` before forwarding to the backend.

## Frontend

- "Review PR" button in the issue detail sidebar expands an inline form with a GitHub PR URL input.
- While the agent runs: input and button are disabled, spinner + "Reviewing…" label shown.
- On success: form collapses and `CommentsSection` re-fetches (via `refreshKey` increment) to show the new AI comment immediately. When the issue detail is open in a modal, the page navigates to the full issue URL instead.
- On error: inline error message extracted from ProblemDetails response.
- AI-generated comments are rendered with a distinct left border and an `"AI"` badge next to the author name. When `CommentsSection` is in modal/compact mode (`hideAiComments`), AI comments are filtered and replaced with an `"AI review available"` link to the full page.
