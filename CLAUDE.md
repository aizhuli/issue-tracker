# ai-issue-tracker

An MVP issue tracker with two AI features:

1. **AI Triage** — single-shot LLM call that proposes `priority`, `labels`, and `acceptance_criteria` for a fresh issue.
2. **AI PR Reviewer** — multi-step agent with tools (`fetch_pr_metadata`, `fetch_pr_diff`, `fetch_changed_files`, `fetch_file_content`) that reviews a GitHub PR against the issue's acceptance criteria and posts the review as a comment.

The product is meant for demo/teaching. Optimize for clarity and a clean vertical-slice implementation, not for production hardening.

---

## Tech Stack

| Layer            | Choice                                                        |
|------------------|---------------------------------------------------------------|
| Backend API      | ASP.NET Core (.NET 10) — Minimal APIs                         |
| Orchestration    | .NET Aspire (AppHost + ServiceDefaults)                       |
| Persistence      | EF Core 9 + Npgsql (PostgreSQL)                               |
| Mediator         | MediatR (request/response + validation pipeline)              |
| Validation       | FluentValidation (backend) + Zod (frontend)                   |
| IDs              | IdGen → `long` PK → Crockford Base32 for public surfaces      |
| Pagination       | AIP-158 token pagination (`PageResponse<T>`)                  |
| Error format     | RFC 7807 ProblemDetails with `errorCode` + `traceId`          |
| Frontend         | Next.js 16 (App Router, TypeScript) acting as a BFF           |
| BFF auth         | HTTP-only encrypted session cookie (iron-session / next-auth) |
| API auth         | JWT bearer token, issued by ASP.NET, attached by the BFF      |
| Testing (BE)     | xUnit + Testcontainers (PostgreSQL) via harness pattern       |
| Testing (FE)     | Vitest + React Testing Library; Playwright for E2E (optional) |
| LLM SDK          | `Microsoft.Extensions.AI` over the `OpenAI` provider          |
| LLM host         | OpenAI-compatible endpoint (provider / model TBD)             |

### Open decisions

- **LLM hosting**: local Ollama vs cloud (OpenRouter / Together / Groq) vs self-hosted vLLM. Anything below ~30B parameters tends to fail multi-tool agentic loops — the PR-reviewer feature constrains the choice. Decide before implementing the PR reviewer slice.

---

## Domain

### Entities

- **User** — `email` (unique, login), `password_hash`, `name`, `avatar?`. Anyone can create projects, issues, take work, and comment. No "project membership" concept.
- **Project** — `slug`, `name`, `description?`, `owner: User`. Owns its own set of labels.
- **Issue** — `title` (≤200), `description?` (markdown), `status` (enum), `priority` (enum), `assignee?: User`, `reporter: User` (auto), `labels: Label[]`, `acceptance_criteria?` (markdown, may be AI-suggested), `created_at`, `updated_at`, `closed_at?`.
- **Label** — `name` (unique within project, case-insensitive), `color`. M:N to Issue.
- **Comment** — `author: User`, `body` (markdown), `created_at`, `updated_at`. Ordered ASC by `created_at`.

### Enums

```text
IssueStatus   = backlog | todo | in-progress | in-review | done
IssuePriority = low | medium | high | urgent
```

Status transitions: linear `backlog → todo → in-progress → in-review → done`, with backward steps and skips allowed (process-with-people, not a strict FSM).

### Invariants

- New issue defaults: `status = backlog`, `priority = medium`, `assignee = null`.
- `reporter` is set on create from the current user and never changes.
- `closed_at` is set when transitioning to `done`; cleared on any transition out of `done`.
- `Label.name` is unique within a project (case-insensitive).
- A comment can only be edited or deleted by its author.
- AI-suggested `acceptance_criteria` is marked with `ai_suggested = true` until the user accepts or edits it.

### Use cases (the seven slices that have to work end-to-end)

1. **Quick create** — one-field form, `POST /api/projects/{slug}/issues` with title only.
2. **AI triage** — `POST /api/issues/{id}/ai/triage` returns suggested priority/labels/acceptance criteria; the user applies or edits.
3. **Manual triage + assign** — set priority/labels/criteria/assignee, move to `todo`.
4. **Work the issue** — status transitions + comments + PR URL attachment.
5. **AI PR review** — `POST /api/issues/{id}/ai/review-pr` runs a tool-using agent that writes a review comment tied to the criteria.
6. **Search & filter** — list with `status`, `assignee`, `labels`, text search on `title`/`description`, token pagination.
7. **Close** — transition to `done` (or out of it), close timestamp managed automatically.

---

## Architecture

### Vertical Slice Architecture (backend)

One feature = one file. **Always** use the local skill `vertical-slice-architecture` when implementing any backend feature — it defines the exact layout (Endpoint / Request / Response / RequestValidator / RequestHandler) and the anti-patterns to avoid. The skill at `.claude/skills/vertical-slice-architecture/` is the source of truth.

Namespace root: `AiIssueTracker.Api`.

```text
backend/src/AiIssueTracker.Api/
├── Features/
│   ├── Auth/                  # Register, Login, RefreshToken, Me
│   ├── Projects/              # Create, Get, List, UpdateLabels
│   ├── Issues/                # Create, Get, List, Update, ChangeStatus, Assign
│   ├── Comments/              # CreateComment, UpdateComment, DeleteComment
│   └── Ai/                    # TriageIssue, ReviewPullRequest
├── Common/
│   ├── Auth/                  # JwtIssuer, password hashing, claims extractors
│   ├── Exceptions/            # DomainException + GlobalExceptionHandler
│   ├── Http/                  # IEndpoint contract + extension methods
│   ├── Identity/              # IdFactory (IdGen wrapper) + Base32Encoder
│   ├── Pagination/            # LimitOffsetPaging (AIP-158)
│   └── Validation/            # ValidationBehavior (MediatR pipeline)
├── Data/
│   ├── AppDbContext.cs
│   ├── Entities/              # POCOs — User, Project, Issue, Label, Comment
│   └── Migrations/
├── Integrations/
│   ├── GitHub/                # GitHubClient — diff/files/metadata fetchers
│   └── Llm/                   # IChatClient registration + prompt templates
└── Program.cs
```

### Aspire orchestration

```text
backend/src/
├── AiIssueTracker.AppHost/            # composition root for local dev
└── AiIssueTracker.ServiceDefaults/    # OTel, health checks, resilience defaults
```

Resources wired in AppHost:

- **PostgreSQL** container (with pgAdmin in dev).
- **AiIssueTracker.Api** project, depends on Postgres.
- **frontend** (Next.js) as an `AddNpmApp(...)` resource pointing at `frontend/`, depends on the API.
- **LLM endpoint** as a connection string parameter — local Ollama container in dev, or external URL via configuration.

### Next.js BFF

```text
frontend/
├── app/
│   ├── (auth)/                 # login / register pages
│   ├── projects/[slug]/...     # project + issue UI
│   └── api/                    # BFF route handlers — proxy to ASP.NET
├── components/
├── lib/
│   ├── api-client.ts           # typed fetch helpers
│   ├── session.ts              # iron-session config
│   └── schemas/                # Zod schemas mirroring backend validators
├── AGENTS.md                   # Next.js 16 agent guidance — read before any FE work
├── CLAUDE.md                   # mirrors AGENTS.md (`@AGENTS.md`)
└── middleware.ts               # session enforcement
```

**Next.js 16 caveat (important):** `create-next-app` ships `frontend/AGENTS.md` warning that Next.js 16 has API/convention/file-structure breaking changes vs. older training data. Before writing any frontend code, consult `frontend/node_modules/next/dist/docs/` for the version-correct patterns (router, caching directives, server actions, etc.). Don't trust pre-Next-16 muscle memory.

**BFF passthrough rule:** route handlers under `app/api/*` forward the request to ASP.NET, attaching the JWT from the session. They never validate, never reshape payloads, and pass ProblemDetails responses through unchanged. The only logic in the BFF is session management.

---

## Authentication

Login/password only — no OAuth.

```text
Browser ──cookie (iron-session, HTTP-only, encrypted)──→ Next.js BFF
                                                            │
                                                            │ Authorization: Bearer <jwt>
                                                            ▼
                                                        ASP.NET API
```

- Backend issues a short-lived access token (e.g. 15 min) + refresh token on `POST /api/auth/login`.
- BFF stores both inside the encrypted session cookie.
- BFF refreshes silently when the access token is near expiry (server-side, never exposes tokens to the browser).
- `JwtBearer` is the only authentication scheme on the API.
- `HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)` carries the user `long` ID (Base32-decoded back into a long on read).

Passwords are hashed with ASP.NET's `PasswordHasher<User>` (PBKDF2). No `Microsoft.AspNetCore.Identity` machinery — the user model lives in `Data/Entities/User.cs`.

---

## AI Features

### 1. Triage (single-shot)

- Endpoint: `POST /api/issues/{id}/ai/triage` → returns `{ priority, labels: string[], acceptanceCriteria }`.
- Prompt context: project name + description + **the project's label set** (so the model can only choose from real labels) + the issue title + description.
- Output is parsed JSON. Validate against the project's labels before returning.
- No DB writes — the user reviews and applies via existing update endpoints.

### 2. PR Reviewer (tool-using agent)

- Endpoint: `POST /api/issues/{id}/ai/review-pr` with `{ pullRequestUrl }`.
- Implemented with `Microsoft.Extensions.AI`'s tool-calling loop. The agent has four custom functions:
  - `fetch_pr_metadata(url)` → title, description, author, base/head refs.
  - `fetch_pr_diff(url)` → unified diff.
  - `fetch_changed_files(url)` → list of files with paths and statuses.
  - `fetch_file_content(url, path)` → full file content from the PR's head ref.
- System prompt anchors the review to the issue's `acceptance_criteria`. Output is a review comment that explicitly maps findings to each criterion.
- The review is persisted as a `Comment` on the issue with `author = "AI"` (a reserved system user) and `body` containing the review.

GitHub access uses an unauthenticated client for public repos in MVP. Token-based auth (PAT in `appsettings.json`) is an easy upgrade — gated behind `GitHubOptions`.

---

## Conventions

### Code

- **One feature = one file** in `Features/{Domain}/{Feature}.cs`. Nested `Endpoint`, `Request`, `Response`, `RequestValidator`, `RequestHandler`.
- **Always go through MediatR** — never call a handler directly. The validation pipeline runs on `Send`.
- **Thin endpoints** — extract claims, build the `Request`, dispatch, map to `IResult`. No business logic.
- **DTOs only over the wire** — never serialize EF entities.
- **Options pattern** for configuration. Each domain has `Features/{Domain}/Options/{Domain}Options.cs`. Never inject `IConfiguration` into handlers.
- **Shared infra lives in `Common/{Concern}/`** with a specific concern name. No generic `Helpers/` or `Utilities/` folders.

### IDs

Always follow the local `id-generation` skill:

- DB primary and foreign keys: raw `long` from `IdFactory.Create()` (IdGen).
- Public API responses: Crockford-Base32 encoded via `IdEncoding.Encode(long)`.
- URLs: prefer slug for content entities (`Project.Slug`); encoded ID for everything else.

### Validation & errors

Always follow the local `validation` and `error-handling` skills.

- Error codes follow `domain:entity:field:error_type` (validation) and `domain:entity:operation:error_type` (errors). Examples for this project:
  - `issues:issue:title:required`
  - `issues:issue:status:invalid_transition` (if we ever decide to enforce a transition rule)
  - `projects:label:name:already_exists`
  - `comments:comment:update:forbidden`
  - `ai:pr_review:github:fetch_failed`
- Backend → ProblemDetails with `errorCode` + `traceId`.
- BFF → passthrough (don't reshape).
- Frontend → typed `ApiError` mapped to toasts / form errors via Zod issue codes.

### Pagination

All list endpoints follow the local `token-pagination` skill — `pageToken`, `maxPageSize`, `PageResponse<T>`. Always pass every filter parameter into both `TryGetOffsetAndLimit` and `CreateNextPageToken`, in the same order. Always order by a stable, unique field (`OrderBy(x => x.Id)`).

### Testing

Always follow the local `component-testing` skill.

- Backend tests: component-level via real HTTP + a Postgres Testcontainer + harnesses. Domain-specific xUnit collections (`IssuesTestsCollection`, `ProjectsTestsCollection`, `AiTestsCollection`, …).
- Validator tests: nested `ValidatorTests` class inside the corresponding feature test file, using `FluentValidation.TestHelper`. Every feature with a `RequestValidator` requires these tests. Validation is **not** covered in component tests.
- AI features: stub `IChatClient` in tests with a fake that returns canned tool calls / responses. Don't burn real LLM tokens in CI.
- GitHub integration: stub the `GitHubClient` in tests; no live network calls.

---

## Local Skills

The `.claude/skills/` directory holds project-specific skills. **Always check whether a relevant skill applies before writing code** — they are the source of truth for the project's conventions.

| Skill                            | Use when                                                  |
|----------------------------------|-----------------------------------------------------------|
| `vertical-slice-architecture`    | Implementing any backend feature                          |
| `component-testing`              | Writing tests for a vertical slice                        |
| `validation`                     | Adding any form / input validation                        |
| `error-handling`                 | Surfacing failures (not-found, forbidden, conflict, …)    |
| `id-generation`                  | Creating an entity or designing a public ID               |
| `token-pagination`               | Building any list endpoint                                |
| `architecture`                   | Significant cross-slice design decisions                  |
| `requirements`, `mvp-scope`      | Negotiating scope with the user                           |
| `design-brainstorming`           | Anything creative — features, flows, behavior             |
| `implementation-planning`        | Turning a design into a checkbox plan for parallel agents |
| `spec-maintenance`               | Keeping `docs/designs/*.md` honest as the code drifts     |
| `ui-design`                      | Writing ANY frontend component or page                    |

The kit also ships skills under `dotnet-claude-kit:*` (build-fix, code-review, ef-core, opentelemetry, etc.). Use them when they fit; they don't replace the local skills above.

---

## Development Workflow

> Working directory is `E:\ai-issue-tracker`. Shell is PowerShell.

### One-time setup (not yet executed)

```powershell
dotnet new sln -n AiIssueTracker

dotnet new web      -n AiIssueTracker.Api             -o backend/src/AiIssueTracker.Api
dotnet new xunit    -n AiIssueTracker.Api.Tests       -o backend/tests/AiIssueTracker.Api.Tests
dotnet new aspire-apphost          -n AiIssueTracker.AppHost          -o backend/src/AiIssueTracker.AppHost
dotnet new aspire-servicedefaults  -n AiIssueTracker.ServiceDefaults  -o backend/src/AiIssueTracker.ServiceDefaults

dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj)

npx create-next-app@latest frontend --typescript --app --tailwind --eslint --no-src-dir
```

### Day-to-day

```powershell
# Run the full stack (API + Postgres + Next.js) via Aspire
dotnet run --project backend/src/AiIssueTracker.AppHost

# Backend tests (PostgreSQL Testcontainer starts automatically)
dotnet test backend/tests/AiIssueTracker.Api.Tests

# EF migrations
dotnet ef migrations add <Name> `
    --project backend/src/AiIssueTracker.Api `
    --startup-project backend/src/AiIssueTracker.Api

# Frontend
cd frontend; npm run dev      # standalone
cd frontend; npm test         # vitest
cd frontend; npm run lint
```

### Working order

The recommended slice-by-slice order, each shippable on its own:

1. Aspire + Postgres + API skeleton + `/health`.
2. `Users` (register, login, JWT issuance, `me`).
3. Next.js BFF auth wiring (iron-session cookie, login form).
4. `Projects` (create, get, list) and `Labels` (create, list, delete).
5. `Issues` quick-create + get + list (with token pagination + filters).
6. Status transitions + assignment + comments.
7. **AI Triage** (single-shot endpoint).
8. **AI PR Reviewer** (tool-using agent).
9. Polish: search, closed-issue filter, AI badge for `ai_suggested`.

Don't start a later step until the earlier ones have passing component tests.

---

## What's intentionally out of scope

- Real-time updates (SignalR / web sockets). Polling is fine for the demo.
- Notifications / email.
- Multi-tenant permissions or project membership.
- Audit log / activity feed beyond comments.
- File attachments on issues or comments.
- OAuth, SSO, password reset email flow.
- Caching (HybridCache / Redis), background workers, messaging.

These are easy to add later if any of them comes up — but they are **not** on the MVP path.
