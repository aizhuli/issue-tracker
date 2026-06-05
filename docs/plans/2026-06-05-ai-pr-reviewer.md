# Implementation Plan: AI PR Reviewer

Design: `docs/designs/2026-06-05-ai-pr-reviewer.md`

---

## Phase 1 — Foundations

*No dependencies. All tasks work on different files and can be executed in parallel.*

### Task 1.1: AI System User Seed

- [ ] Create `Common/Identity/SystemUsers.cs` with `public static class SystemUsers { public const long AiUserId = 1L; }`
- [ ] In `AppDbContext.OnModelCreating`, inside the `User` entity configuration block, call `b.HasData(new User { Id = SystemUsers.AiUserId, Email = "ai@system", Name = "AI", PasswordHash = "!", CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) })`
- [ ] Run `dotnet ef migrations add SeedAiSystemUser --project backend/src/AiIssueTracker.Api --startup-project backend/src/AiIssueTracker.Api` to generate the migration
- [ ] Verify the generated `Up` inserts the AI user row and `Down` deletes it

**Files:** `backend/src/AiIssueTracker.Api/Common/Identity/SystemUsers.cs`, `backend/src/AiIssueTracker.Api/Data/AppDbContext.cs`, generated migration file

---

### Task 1.2: GitHub Client

- [ ] Create `Integrations/GitHub/GitHubOptions.cs` with `SectionName = "GitHub"` and optional `Token` string property
- [ ] Create `Integrations/GitHub/GitHubClient.cs` as a typed `HttpClient` wrapper with:
  - A static `ParsePrUrl(string url)` method returning `(string owner, string repo, int number)` — throws `ValidationException` with error code `ai:pr_review:pull_request_url:invalid` if URL does not match `https://github.com/{owner}/{repo}/pull/{number}`
  - `FetchMetadataAsync(url)` — `GET /repos/{owner}/{repo}/pulls/{number}`, returns formatted string of title, description, author, base/head refs
  - `FetchDiffAsync(url)` — same endpoint with `Accept: application/vnd.github.v3.diff`, caps at 50 KB and appends `\n[diff truncated]` if over limit
  - `FetchChangedFilesAsync(url)` — `GET /repos/{owner}/{repo}/pulls/{number}/files`, returns formatted list of paths + statuses
  - `FetchFileContentAsync(url, path)` — `GET /repos/{owner}/{repo}/contents/{path}?ref={headSha}`, decodes base64 content, caps at 20 KB and appends `\n[content truncated]` if over limit
  - All GitHub HTTP errors (non-2xx) throw `BadGatewayException("GitHub API error.", "ai:pr_review:github:fetch_failed")`
  - Network/timeout errors also throw `BadGatewayException("GitHub is unreachable.", "ai:pr_review:github:fetch_failed")`
- [ ] In `Program.cs`, register `GitHubOptions`: `builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName))`
- [ ] In `Program.cs`, register the typed HttpClient: `builder.Services.AddHttpClient<GitHubClient>((sp, client) => { client.BaseAddress = new Uri("https://api.github.com"); client.DefaultRequestHeaders.Add("User-Agent", "ai-issue-tracker"); var token = sp.GetRequiredService<IOptions<GitHubOptions>>().Value.Token; if (!string.IsNullOrEmpty(token)) client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}"); })`

**Files:** `backend/src/AiIssueTracker.Api/Integrations/GitHub/GitHubOptions.cs`, `backend/src/AiIssueTracker.Api/Integrations/GitHub/GitHubClient.cs`, `backend/src/AiIssueTracker.Api/Program.cs`

---

### Task 1.3: BFF Passthrough Route

- [ ] Create `frontend/app/api/projects/[slug]/issues/[number]/ai/review-pr/route.ts`
- [ ] Implement a `POST` handler that checks for an active session (return 401 `MISSING_SESSION_RESPONSE` if absent), reads the raw request body, calls `serverFetch` at `/api/projects/${slug}/issues/${number}/ai/review-pr`, and returns `passthrough(upstream)` — mirror the triage route exactly

**Files:** `frontend/app/api/projects/[slug]/issues/[number]/ai/review-pr/route.ts`

---

### Task 1.4: CommentsSection `refreshKey` Prop

- [ ] Add optional `refreshKey?: number` to `CommentsSectionProps`
- [ ] Add `refreshKey` to the dependency array of the initial fetch `useEffect` (the one that calls `fetchComments()` with no page token) — this causes the component to re-fetch from page 1 whenever the key increments
- [ ] Update the call site in `IssueDetail.tsx` where `<CommentsSection ... />` is rendered to accept the new optional prop (no functional change yet — that comes in Phase 2)

**Files:** `frontend/components/issues/CommentsSection.tsx`, `frontend/components/issues/IssueDetail.tsx`

---

## Phase 2 — Feature Slice + Review UI

*Depends on Phase 1. Tasks 2.1 and 2.2 touch different files and can run in parallel.*

### Task 2.1: ReviewPullRequest Vertical Slice

Follow the `vertical-slice-architecture` skill.

- [ ] Create `Features/Ai/ReviewPullRequest.cs` with the standard slice layout (`Endpoint`, `Request`, `ReviewDto`, `RequestValidator`, `RequestHandler`)
- [ ] `Endpoint`: `MapPost("/api/projects/{slug}/issues/{number}/ai/review-pr")`, body param `Body(string PullRequestUrl)`, `.RequireAuthorization(AuthPolicies.RequireUser)`, `.WithTags("Ai")`, `.WithName("ReviewPullRequest")`
- [ ] `Request(string Slug, int Number, string PullRequestUrl)` implements `IRequest<ReviewDto>`; `ReviewDto(string CommentId, string Body)`
- [ ] `RequestValidator`: `PullRequestUrl` not empty (error code `ai:pr_review:pull_request_url:required`), matches regex `^https://github\.com/[^/]+/[^/]+/pull/\d+$` (error code `ai:pr_review:pull_request_url:invalid`)
- [ ] `RequestHandler` constructor: `(AppDbContext db, IChatClient chatClient, GitHubClient gitHub, IdFactory idFactory)`
- [ ] In `Handle`: load project by slug → 404 `projects:project:not_found`; load issue by `ProjectId + Number` → 404 `issues:issue:not_found`
- [ ] Build 4 `AIFunction` tools via `AIFunctionFactory.Create`, each wrapping the corresponding `GitHubClient` method and forwarding `request.PullRequestUrl`; give each tool a clear name and description matching the design
- [ ] Wrap `chatClient` locally: `var agent = chatClient.AsBuilder().UseFunctionInvocation(o => o.MaximumIterations = 10).Build()`
- [ ] Build `ChatOptions` with `Tools` set to the 4 functions
- [ ] Write system prompt: if `issue.AcceptanceCriteria` is not null, anchor the review to each criterion explicitly; otherwise instruct a general code quality review; ask for markdown output
- [ ] Call `agent.GetResponseAsync(messages, options, ct)` wrapped in a try/catch that maps `OperationCanceledException` (timeout) and other exceptions to `BadGatewayException("LLM service unavailable.", "ai:pr_review:llm:unavailable")`
- [ ] Persist the result: create `Comment { Id = idFactory.Create(), IssueId = issue.Id, AuthorId = SystemUsers.AiUserId, Body = reviewText.Trim()[..Math.Min(10_000, reviewText.Trim().Length)], CreatedAt = now, UpdatedAt = now }`, add to db, save
- [ ] Return `new ReviewDto(IdEncoding.Encode(comment.Id), comment.Body)`

**Files:** `backend/src/AiIssueTracker.Api/Features/Ai/ReviewPullRequest.cs`

---

### Task 2.2: IssueDetail Review PR UI

- [ ] Add state to `IssueDetail`: `prFormOpen` (bool), `prUrl` (string), `prReviewing` (bool), `prError` (string), `commentsRefreshKey` (number, starts at 0)
- [ ] Add `handleReviewPr` async function: POST `{ pullRequestUrl: prUrl }` to `/api/projects/${projectSlug}/issues/${issue.number}/ai/review-pr`; on success set `prFormOpen = false`, `prUrl = ""`, `prError = ""`, increment `commentsRefreshKey`; on failure extract error message from ProblemDetails and set `prError`
- [ ] In the right sidebar, add a "Review PR" button directly below the "Edit" button — same styling as the Edit button
- [ ] Clicking "Review PR" sets `prFormOpen = true`; when `prFormOpen`, render the inline form below the button: a text input for the GitHub PR URL + a "Run review" button + a "Cancel" button
- [ ] While `prReviewing`: show spinner + "Reviewing…" label (same spinner pattern as AI-suggest), disable input and button
- [ ] Show `prError` as a small error message below the input when non-empty; clear it on a new submission attempt
- [ ] Pass `refreshKey={commentsRefreshKey}` to `<CommentsSection />` (this is the wire-up that makes the comment list refresh after review)

**Files:** `frontend/components/issues/IssueDetail.tsx`
