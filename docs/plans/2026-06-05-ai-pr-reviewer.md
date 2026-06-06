# Implementation Plan: AI PR Reviewer

Design: `docs/designs/2026-06-05-ai-pr-reviewer.md`

---

## Phase 1 — Foundations

*No dependencies. All tasks work on different files and can be executed in parallel.*

### Task 1.1: AI System User Seed

- [x] Create `Common/Identity/SystemUsers.cs` with `public static class SystemUsers { public const long AiUserId = 1L; }`
- [x] In `AppDbContext.OnModelCreating`, inside the `User` entity configuration block, call `b.HasData(new User { Id = SystemUsers.AiUserId, Email = "ai@system", Name = "AI", PasswordHash = "!", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) })`
- [x] Run `dotnet ef migrations add SeedAiSystemUser --project backend/src/AiIssueTracker.Api --startup-project backend/src/AiIssueTracker.Api` to generate the migration
- [x] Verify the generated `Up` inserts the AI user row and `Down` deletes it

**Files:** `backend/src/AiIssueTracker.Api/Common/Identity/SystemUsers.cs`, `backend/src/AiIssueTracker.Api/Data/AppDbContext.cs`, generated migration file

---

### Task 1.2: GitHub Client

- [x] Create `Integrations/GitHub/GitHubOptions.cs` with `SectionName = "GitHub"` and optional `Token` string property
- [x] Create `Integrations/GitHub/GitHubClient.cs` as a typed `HttpClient` wrapper with:
  - A static `ParsePrUrl(string url)` method returning `(string owner, string repo, int number)` — throws `ValidationException` with error code `ai:pr_review:pull_request_url:invalid` if URL does not match `https://github.com/{owner}/{repo}/pull/{number}`
  - `FetchMetadataAsync(url)` — `GET /repos/{owner}/{repo}/pulls/{number}`, returns formatted string of title, description, author, base/head refs
  - `FetchDiffAsync(url)` — same endpoint with `Accept: application/vnd.github.v3.diff`, caps at 50 KB and appends `\n[diff truncated]` if over limit
  - `FetchChangedFilesAsync(url)` — `GET /repos/{owner}/{repo}/pulls/{number}/files`, returns formatted list of paths + statuses
  - `FetchFileContentAsync(url, path)` — `GET /repos/{owner}/{repo}/contents/{path}?ref={headSha}`, decodes base64 content, caps at 20 KB and appends `\n[content truncated]` if over limit
  - All GitHub HTTP errors (non-2xx) throw `BadGatewayException("GitHub API error.", "ai:pr_review:github:fetch_failed")`
  - Network/timeout errors also throw `BadGatewayException("GitHub is unreachable.", "ai:pr_review:github:fetch_failed")`
- [x] In `Program.cs`, register `GitHubOptions`: `builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName))`
- [x] In `Program.cs`, register the typed HttpClient: `builder.Services.AddHttpClient<GitHubClient>((sp, client) => { client.BaseAddress = new Uri("https://api.github.com"); client.DefaultRequestHeaders.Add("User-Agent", "ai-issue-tracker"); var token = sp.GetRequiredService<IOptions<GitHubOptions>>().Value.Token; if (!string.IsNullOrEmpty(token)) client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}"); })`

**Files:** `backend/src/AiIssueTracker.Api/Integrations/GitHub/GitHubOptions.cs`, `backend/src/AiIssueTracker.Api/Integrations/GitHub/GitHubClient.cs`, `backend/src/AiIssueTracker.Api/Program.cs`

---

### Task 1.3: BFF Passthrough Route

- [x] Create `frontend/app/api/projects/[slug]/issues/[number]/ai/review-pr/route.ts`
- [x] Implement a `POST` handler that checks for an active session (return 401 `MISSING_SESSION_RESPONSE` if absent), reads the raw request body, calls `serverFetch` at `/api/projects/${slug}/issues/${number}/ai/review-pr`, and returns `passthrough(upstream)` — mirror the triage route exactly

**Files:** `frontend/app/api/projects/[slug]/issues/[number]/ai/review-pr/route.ts`

---

### Task 1.4: CommentsSection `refreshKey` Prop

- [x] Add optional `refreshKey?: number` to `CommentsSectionProps`
- [x] Add `refreshKey` to the dependency array of the initial fetch `useEffect` (the one that calls `fetchComments()` with no page token) — this causes the component to re-fetch from page 1 whenever the key increments
- [x] Update the call site in `IssueDetail.tsx` where `<CommentsSection ... />` is rendered to accept the new optional prop (no functional change yet — that comes in Phase 2)

**Files:** `frontend/components/issues/CommentsSection.tsx`, `frontend/components/issues/IssueDetail.tsx`

---

## Phase 2 — Feature Slice + Review UI

*Depends on Phase 1. Tasks 2.1 and 2.2 touch different files and can run in parallel.*

### Task 2.1: ReviewPullRequest Vertical Slice

Follow the `vertical-slice-architecture` skill.

- [x] Create `Features/Ai/ReviewPullRequest.cs` with the standard slice layout (`Endpoint`, `Request`, `ReviewDto`, `RequestValidator`, `RequestHandler`)
- [x] `Endpoint`: `MapPost("/api/projects/{slug}/issues/{number}/ai/review-pr")`, body param `Body(string PullRequestUrl)`, `.RequireAuthorization(AuthPolicies.RequireUser)`, `.WithTags("Ai")`, `.WithName("ReviewPullRequest")`
- [x] `Request(string Slug, int Number, string PullRequestUrl)` implements `IRequest<ReviewDto>`; `ReviewDto(string CommentId, string Body)`
- [x] `RequestValidator`: `PullRequestUrl` not empty (error code `ai:pr_review:pull_request_url:required`), matches regex `^https://github\.com/[^/]+/[^/]+/pull/\d+$` (error code `ai:pr_review:pull_request_url:invalid`)
- [x] `RequestHandler` constructor: `(AppDbContext db, IChatClient chatClient, GitHubClient gitHub, IdFactory idFactory)`
- [x] In `Handle`: load project by slug → 404 `projects:project:not_found`; load issue by `ProjectId + Number` → 404 `issues:issue:not_found`
- [x] Build 4 `AIFunction` tools via `AIFunctionFactory.Create`, each wrapping the corresponding `GitHubClient` method and forwarding `request.PullRequestUrl`; give each tool a clear name and description matching the design
- [x] Wrap `chatClient` locally: `var agent = chatClient.AsBuilder().UseFunctionInvocation(o => o.MaximumIterations = 10).Build()`
- [x] Build `ChatOptions` with `Tools` set to the 4 functions
- [x] Write system prompt: if `issue.AcceptanceCriteria` is not null, anchor the review to each criterion explicitly; otherwise instruct a general code quality review; ask for markdown output
- [x] Call `agent.GetResponseAsync(messages, options, ct)` wrapped in a try/catch that maps `OperationCanceledException` (timeout) and other exceptions to `BadGatewayException("LLM service unavailable.", "ai:pr_review:llm:unavailable")`
- [x] Persist the result: create `Comment { Id = idFactory.Create(), IssueId = issue.Id, AuthorId = SystemUsers.AiUserId, Body = reviewText.Trim()[..Math.Min(10_000, reviewText.Trim().Length)], CreatedAt = now, UpdatedAt = now }`, add to db, save
- [x] Return `new ReviewDto(IdEncoding.Encode(comment.Id), comment.Body)`

**Files:** `backend/src/AiIssueTracker.Api/Features/Ai/ReviewPullRequest.cs`

---

### Task 2.2: IssueDetail Review PR UI

- [x] Add state to `IssueDetail`: `prFormOpen` (bool), `prUrl` (string), `prReviewing` (bool), `prError` (string), `commentsRefreshKey` (number, starts at 0)
- [x] Add `handleReviewPr` async function: POST `{ pullRequestUrl: prUrl }` to `/api/projects/${projectSlug}/issues/${issue.number}/ai/review-pr`; on success set `prFormOpen = false`, `prUrl = ""`, `prError = ""`, increment `commentsRefreshKey`; on failure extract error message from ProblemDetails and set `prError`
- [x] In the right sidebar, add a "Review PR" button directly below the "Edit" button — same styling as the Edit button
- [x] Clicking "Review PR" sets `prFormOpen = true`; when `prFormOpen`, render the inline form below the button: a text input for the GitHub PR URL + a "Run review" button + a "Cancel" button
- [x] While `prReviewing`: show spinner + "Reviewing…" label (same spinner pattern as AI-suggest), disable input and button
- [x] Show `prError` as a small error message below the input when non-empty; clear it on a new submission attempt
- [x] Pass `refreshKey={commentsRefreshKey}` to `<CommentsSection />` (this is the wire-up that makes the comment list refresh after review)

**Files:** `frontend/components/issues/IssueDetail.tsx`

---

## Phase 3 — Tests

### Task 3.1: ReviewPullRequest Tests

- [x] `ReviewPr_HappyPath_Returns200AndPersistsComment` — fake LLM returns review text; verify 200, `ReviewDto` body matches, comment persisted with `AuthorId = SystemUsers.AiUserId`
- [x] `ReviewPr_WithAcceptanceCriteria_IncludesInSystemPrompt` — issue has AC; verify system prompt contains the criteria text
- [x] `ReviewPr_NoAcceptanceCriteria_UsesGeneralPrompt` — no AC; verify prompt contains "code quality" and not "acceptance criteria"
- [x] `ReviewPr_LlmThrows_Returns502Unavailable` — fake throws; verify 502 with `ai:pr_review:llm:unavailable`
- [x] `ReviewPr_UnknownProject_Returns404` — unknown slug; verify 404 with `projects:project:not_found`
- [x] `ReviewPr_UnknownIssue_Returns404` — unknown number; verify 404 with `issues:issue:not_found`
- [x] `ReviewPr_EmptyUrl_Returns400WithRequiredError` — empty URL; verify 400 with `ai:pr_review:pull_request_url:required`
- [x] `ReviewPr_InvalidUrl_Returns400WithInvalidError` — non-GitHub URL; verify 400 with `ai:pr_review:pull_request_url:invalid`

**Files:** `backend/tests/AiIssueTracker.Api.Tests/Features/Ai/ReviewPullRequestTests.cs`

---

## Code Review Findings

Findings from post-implementation review of the feature branch. Ranked most-severe first.

### High

- [x] **`response.Text` null → NullReferenceException, no comment saved** (`ReviewPullRequest.cs:122`)
  `response.Text` is nullable in MEAI — null when the LLM ends its turn with only tool-call messages (e.g. MaximumIterationsPerRequest reached). Line 134 calls `reviewText.Trim()` without a null check; the resulting `NullReferenceException` is caught by the broad `catch (Exception)` and surfaces as 502 `ai:pr_review:llm:unavailable` with no comment persisted. Fix: null-check `response.Text` and throw a descriptive `BadGatewayException("LLM returned no review text.", ...)` before the Trim call.

- [x] **`GetJsonDocumentAsync` catch-all swallows `EnsureSuccessAsync` exception** (`GitHubClient.cs:113`)
  `EnsureSuccessAsync` throws `BadGatewayException("GitHub API error.")` for non-2xx responses. The enclosing `catch (Exception ex) when (ex is not OperationCanceledException)` catches it and re-throws `BadGatewayException("GitHub is unreachable.")`. Every 403 (rate limit), 404 (private repo), or 422 shows the wrong detail message. Fix: move `EnsureSuccessAsync` outside the try block (after `http.GetAsync` returns successfully), or catch only `HttpRequestException`.

- [x] **`FetchChangedFilesAsync` silently truncates to 30 files** (`GitHubClient.cs:70`)
  The GitHub `/pulls/{number}/files` endpoint defaults to 30 items per page. PRs with >30 changed files return an incomplete list with no truncation marker. Fix: add `?per_page=100` (GitHub's max for this endpoint) and append `\n[file list truncated — {n}/total shown]` when the response hits the limit, consistent with how `FetchDiffAsync` handles its cap.

### Medium

- [x] **GitHub `HttpClient` timeout mislabeled as `ai:pr_review:llm:unavailable`** (`GitHubClient.cs:104`)
  `GetJsonDocumentAsync` does not catch `OperationCanceledException`, so a GitHub `HttpClient` timeout (`TaskCanceledException`) propagates to the handler where line 124 catches it as `when (!ct.IsCancellationRequested)` and emits `ai:pr_review:llm:unavailable`. Fix: catch `OperationCanceledException` in `GetJsonDocumentAsync` / `SendAsync` and rethrow as `BadGatewayException("GitHub timed out.", "ai:pr_review:github:fetch_failed")`.

- [x] **No server-side LLM timeout; stalled agentic loop holds thread indefinitely** (`ReviewPullRequest.cs:121`)
  The handler passes `ct` (the ASP.NET request token) directly to `GetResponseAsync`. If Ollama/OpenAI stalls mid-loop, the request blocks until the browser or BFF proxy times out. Fix: create a linked `CancellationTokenSource` with `CancelAfter(timeoutSeconds)` (mirroring `TriageIssue`), use it for the `GetResponseAsync` call, and distinguish timeout from user-cancel in the catch.

- [x] **`FetchFileContentAsync` makes 2 GitHub API calls per file** (`GitHubClient.cs:85`)
  Every `fetch_file_content` tool call re-fetches `/repos/{owner}/{repo}/pulls/{number}` solely to extract `head.sha`, doubling rate-limit consumption. Fix: expose `headSha` from `FetchMetadataAsync` (or cache it as a private field on the first metadata call) and pass it directly into `FetchFileContentAsync`.

- [x] **No `AbortController` in `handleReviewPr`** (`IssueDetail.tsx:341`)
  `handleReviewPr` uses a bare `fetch(...)` with no abort signal, unlike `handleSuggest` which uses `abortRef`. If the user navigates away during a 30+ second review, state setters fire on the unmounted component. Fix: create an `AbortController` (or reuse `abortRef`), pass its signal to `fetch`, and clean up in the `finally` block.

### Low

- [x] **Test 4 (LlmThrows) does not seed the AI system user — fragile FK invariant** (`ReviewPullRequestTests.cs:1003`)
  `ResetAsync` truncates the `users` table, wiping the migration-seeded AI user. Test 4 skips `MakeAiUser()` and works today only because the LLM throws before `SaveChangesAsync`. Any future refactor that reaches `SaveChangesAsync` without the AI user present will produce an FK violation instead of the expected 502. Fix: either seed the AI user in all tests unconditionally, or add the AI user to a Respawn `TablesToIgnore` / re-seed it in `ResetAsync`.

- [x] **`CommentsSection` shows stale comments during `refreshKey` refresh** (`CommentsSection.tsx:258`)
  When `refreshKey` increments, the effect calls `setLoading(true)` then fetches page 1, but does not call `setComments([])` first. The old comment list (without the new AI review) is visible during the async gap. On a busy issue (>20 comments) the new AI review comment lands off-screen behind "Load more" after the reset to page 1. Fix: call `setComments([])` and `setNextPageToken(null)` before fetching when the trigger is a `refreshKey` change.

- [x] **`MISSING_SESSION_RESPONSE` copy-pasted across all BFF route files** (`review-pr/route.ts:5`)
  The object literal is duplicated in every route instead of being imported from a shared module. Fix: export it from `frontend/lib/bff-responses.ts` and import in each route.
