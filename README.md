# Aigo — AI Issue Tracker

A demo/teaching project: a lightweight issue tracker with two AI-powered features built on .NET 10 + Next.js 16.

## What it does

**Core tracker** — projects, issues, labels, comments, status board, assignees. Everything you'd expect from a minimal issue tracker.

**AI Triage** — one click on a fresh issue runs a single-shot LLM call that suggests priority, labels, and acceptance criteria drawn from the project's real label set. You review and apply (or ignore) the suggestions.

**AI PR Reviewer** *(in progress)* — paste a GitHub PR URL on an issue and a tool-using agent fetches the PR metadata, diff, and changed files, then writes a review comment mapped to the issue's acceptance criteria.

## Tech stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core (.NET 10) — Minimal APIs, Vertical Slice Architecture |
| Orchestration | .NET Aspire |
| Database | PostgreSQL via EF Core 10 + Npgsql |
| Auth | HTTP-only iron-session cookie (BFF) + shared-secret header to the API |
| Frontend | Next.js 16 (App Router, TypeScript) acting as a BFF |
| LLM | `Microsoft.Extensions.AI` over OpenAI-compatible endpoint (Ollama locally) |
| IDs | IdGen → Crockford Base32 on public surfaces |
| Testing | xUnit + Testcontainers (BE), Vitest + RTL (FE) |

## Architecture in one paragraph

The browser only talks to the Next.js BFF. The BFF forwards requests to the ASP.NET API, adding a shared-secret header and the session user-id. The API never faces the browser directly. PostgreSQL is the only persistence layer. LLM calls go through `IChatClient` (configurable endpoint — Ollama in local dev, any OpenAI-compatible API in prod).

## Getting started

**Prerequisites:** .NET 10 SDK, Node.js 20+, Docker (for PostgreSQL via Aspire), Ollama (for local AI features).

```powershell
# Clone and run the full stack (API + Postgres + Next.js) via Aspire
dotnet run --project backend/src/AiIssueTracker.AppHost
```

Aspire starts everything: PostgreSQL container, the ASP.NET API, and the Next.js dev server. Open the Aspire dashboard URL printed in the console, then navigate to the Next.js app URL.

**Standalone frontend** (if you have the API running separately):

```bash
cd frontend
npm install
npm run dev
```

**Backend tests:**

```bash
dotnet test backend/tests/AiIssueTracker.Api.Tests
# PostgreSQL Testcontainer spins up automatically
```

## Configuration

LLM endpoint is configured via `LlmOptions`. For local dev with Ollama no config is needed. For a hosted endpoint, set user secrets on `AiIssueTracker.Api`:

```bash
dotnet user-secrets set "Llm:Endpoint" "https://..."
dotnet user-secrets set "Llm:ApiKey" "..."
dotnet user-secrets set "Llm:ModelId" "..."
```

GitHub token for the PR Reviewer (optional — public repos work without one):

```bash
dotnet user-secrets set "GitHub:Token" "ghp_..."
```

## Project structure

```
backend/src/
├── AiIssueTracker.AppHost/        # Aspire composition root
├── AiIssueTracker.ServiceDefaults/
└── AiIssueTracker.Api/
    └── Features/                  # One file per vertical slice
        ├── Auth/
        ├── Projects/Labels/
        ├── Issues/
        ├── Comments/
        └── Ai/                    # Triage + PR Reviewer

frontend/
├── app/(auth)/                    # Login / register
├── app/(app)/projects/[slug]/     # Project + issue UI
└── app/api/                       # BFF route handlers
```

## Current status

- [x] Auth (register, login, session)
- [x] Projects + labels
- [x] Issues — create, list, filter, update, delete
- [x] Status transitions, assignment, comments
- [x] AI Triage (single-shot)
- [x] Responsive UI (mobile + tablet)
- [ ] AI PR Reviewer (tool-using agent) — in progress
- [ ] Full-text search, closed-issue filter

## What's out of scope (intentionally)

Real-time updates, notifications, OAuth, file attachments, audit logs, multi-tenant permissions, caching, background workers.
