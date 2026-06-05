# AI PR Reviewer

**Date:** 2026-06-05  
**Status:** Approved — ready to implement

---

## Overview

Add a tool-using LLM agent that reviews a GitHub PR against an issue's acceptance criteria and posts the review as a comment authored by a reserved AI system user.

**Endpoint:** `POST /api/projects/{slug}/issues/{number}/ai/review-pr`  
**Body:** `{ pullRequestUrl: string }`  
**Response:** `{ commentId: string, body: string }`

---

## Files to Create / Modify

| # | File | What |
|---|------|------|
| 1 | `Common/Identity/SystemUsers.cs` | `AiUserId = 1L` constant |
| 2 | `Data/AppDbContext.cs` | `HasData` seed for AI user |
| 3 | Migration | EF migration for seed |
| 4 | `Integrations/GitHub/GitHubOptions.cs` | Optional PAT config |
| 5 | `Integrations/GitHub/GitHubClient.cs` | 4 GitHub REST methods |
| 6 | `Features/Ai/ReviewPullRequest.cs` | Full vertical slice |
| 7 | `Program.cs` | Register GitHub HttpClient |
| 8 | BFF route | Passthrough route handler |
| 9 | `IssueDetail.tsx` | Review PR button + inline form |
| 10 | `CommentsSection.tsx` | `refreshKey` prop |

---

## Section 1 — Structure & AI System User

### AI System User Seeding

The `Comment.AuthorId` FK requires a real `User` row. A reserved "AI" user is seeded via EF Core `HasData` in `OnModelCreating` — applied once by the migration, never checked at runtime.

```csharp
// Common/Identity/SystemUsers.cs
public static class SystemUsers
{
    public const long AiUserId = 1L;
}
```

`HasData` in `AppDbContext.OnModelCreating`:
```csharp
b.HasData(new User {
    Id = SystemUsers.AiUserId,
    Email = "ai@system",
    Name = "AI",
    PasswordHash = "!",   // non-matchable sentinel — login blocked
    CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
});
```

The handler sets `AuthorId = SystemUsers.AiUserId` directly — no DB lookup.

**Why `HasData` over raw SQL:**
- EF tracks the row in the model snapshot — changes generate update migrations automatically
- Down migration is auto-generated
- Compile-time safe (strongly typed C# properties)

---

## Section 2 — GitHub Client & Tools

### `Integrations/GitHub/GitHubOptions.cs`

Optional PAT for private repos (user secrets / env var). Unauthenticated by default (public repos only in MVP).

### `Integrations/GitHub/GitHubClient.cs`

Typed `HttpClient` registered in DI. Parses the PR URL:
```
https://github.com/{owner}/{repo}/pull/{number}
```

Four methods → four GitHub REST API calls:

| Method | GitHub endpoint | Notes |
|--------|----------------|-------|
| `FetchMetadataAsync(url)` | `GET /repos/{owner}/{repo}/pulls/{pr}` | title, description, author, base/head refs |
| `FetchDiffAsync(url)` | Same URL, `Accept: application/vnd.github.v3.diff` | unified diff, capped at 50 KB |
| `FetchChangedFilesAsync(url)` | `GET /repos/{owner}/{repo}/pulls/{pr}/files` | file paths + statuses |
| `FetchFileContentAsync(url, path)` | `GET /repos/{owner}/{repo}/contents/{path}?ref={sha}` | file content, capped at 20 KB |

Each method returns a plain `string` the model can read. Truncated content includes a notice rather than silently cutting off.

GitHub errors (404, rate limit, network failure) throw `BadGatewayException` with error code `ai:pr_review:github:fetch_failed`.

### Tool-calling Agent

The four methods become four `AIFunction` tools via `AIFunctionFactory.Create`.

The handler wraps the injected `IChatClient` locally with function invocation middleware:
```csharp
var agentClient = chatClient.AsBuilder().UseFunctionInvocation().Build();
```

`MaximumIterations = 10` caps runaway loops.

System prompt anchors the review to `issue.AcceptanceCriteria`. If none, the model reviews against general code quality instead. Output is a markdown review that explicitly maps findings to each criterion.

---

## Section 3 — Endpoint, Response & Comment Persistence

### Vertical Slice: `Features/Ai/ReviewPullRequest.cs`

**Handler flow:**
1. Load project + issue (need `AcceptanceCriteria`)
2. Parse and validate GitHub PR URL
3. Build four `AIFunction` tools using `GitHubClient`
4. Wrap `IChatClient` with `UseFunctionInvocation()` + `MaximumIterations = 10`
5. Run the agent — it fetches what it needs, produces a markdown review
6. Persist review as a `Comment` with `AuthorId = SystemUsers.AiUserId`
7. Return `ReviewDto(string CommentId, string Body)`

**Validation error codes:**
- `ai:pr_review:pull_request_url:required` — missing URL
- `ai:pr_review:pull_request_url:invalid` — not a valid GitHub PR URL
- `ai:pr_review:github:fetch_failed` — GitHub API error
- `ai:pr_review:llm:unavailable` — LLM timeout or error

---

## Section 4 — Frontend

### BFF Route

`app/api/projects/[slug]/issues/[number]/ai/review-pr/route.ts` — plain passthrough, same pattern as all other BFF routes.

### UI Changes

**`IssueDetail.tsx`** — no new component. In the right sidebar, below the "Edit" button:

- "Review PR" button that expands an inline form with a URL input + "Run review" button
- While running: spinner + "Reviewing…", input disabled
- On success: collapse the form, increment `commentsRefreshKey` so `CommentsSection` re-fetches and shows the new AI comment immediately
- On error: inline error message below the input

**`CommentsSection.tsx`** — add `refreshKey: number` prop. When it changes, the component re-fetches the comment list.
