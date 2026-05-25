---
name: run-project
description: Use when starting, running, building, or testing the ai-issue-tracker project — covers full-stack Aspire orchestration, standalone backend/frontend, builds, tests, migrations, and OpenAPI/Scalar endpoints.
---

# Run Project

Reference for launching, building, and testing ai-issue-tracker.  
Working directory: `E:\ai-issue-tracker`. Shell: PowerShell.

---

## Full Stack (recommended) — Aspire

```powershell
dotnet run --project backend/src/AiIssueTracker.AppHost
```

Aspire starts and wires together:

| Resource | Detail |
|----------|--------|
| PostgreSQL container | with data volume |
| pgAdmin | UI for browsing the DB |
| `api` | ASP.NET Core, `http://localhost:5007` (HTTPS `https://localhost:7072`) |
| `frontend` | Next.js 16, dynamic port (check dashboard or netstat — typically ~56000s range) |
| Aspire dashboard | `http://localhost:15299` |

Secrets injected automatically by Aspire (no manual env vars needed):

| Env var | Dev default |
|---------|-------------|
| `BffAuth__SharedSecret` | `dev-bff-secret-change-me-please-32chars-min` |
| `BFF_SHARED_SECRET` | same |
| `SESSION_COOKIE_PASSWORD` | `dev-session-cookie-password-must-be-at-least-32-chars-long-for-iron-session` |
| `API_URL` | resolved from the `api` resource's http endpoint |

### Hard restart

If `dotnet run` fails with a file-lock error (`MSB3026`/`MSB3027`), a previous Aspire session is still running. Kill it, then relaunch:

```powershell
Stop-Process -Name AiIssueTracker.AppHost, dcp -Force -ErrorAction SilentlyContinue
dotnet run --project backend/src/AiIssueTracker.AppHost
```

To find the live frontend URL when the dashboard isn't handy:

```powershell
# Find node process with highest CPU (the Next.js server) and its listening port
netstat -ano | Select-String "LISTENING" | ForEach-Object {
    if ($_ -match ":(\d+)\s.*\s(\d+)$") { [pscustomobject]@{Port=$matches[1];PID=$matches[2]} }
} | Where-Object { (Get-Process -Id $_.PID -EA SilentlyContinue).ProcessName -eq "node" }
```

---

## Backend Only (no Docker / Aspire)

Requires an external PostgreSQL instance. Set the connection string before running:

```powershell
$env:ConnectionStrings__issuetracker = "Host=localhost;Database=issuetracker;Username=postgres;Password=postgres"
dotnet run --project backend/src/AiIssueTracker.Api
```

| Profile | URL |
|---------|-----|
| http | `http://localhost:5007` |
| https | `https://localhost:7072` and `http://localhost:5007` |

Also set `BffAuth__SharedSecret` if testing auth:
```powershell
$env:BffAuth__SharedSecret = "dev-bff-secret-change-me-please-32chars-min"
```

---

## Frontend Only (standalone)

The frontend calls the API through BFF route handlers. Set env vars so it can reach the API:

```powershell
cd frontend
$env:API_URL = "http://localhost:5007"
$env:BFF_SHARED_SECRET = "dev-bff-secret-change-me-please-32chars-min"
$env:SESSION_COOKIE_PASSWORD = "dev-session-cookie-password-must-be-at-least-32-chars-long-for-iron-session"
npm run dev
```

---

## Build

```powershell
# API only
dotnet build backend/src/AiIssueTracker.Api/AiIssueTracker.Api.csproj

# Entire solution
dotnet build
```

---

## Tests

```powershell
# Backend component tests (PostgreSQL Testcontainer starts automatically)
dotnet test backend/tests/AiIssueTracker.Api.Tests

# Frontend lint
cd frontend; npm run lint
```

---

## EF Core Migrations

```powershell
# Add a migration
dotnet ef migrations add <MigrationName> `
    --project backend/src/AiIssueTracker.Api `
    --startup-project backend/src/AiIssueTracker.Api

# Apply manually (Aspire auto-applies on startup via MigrateAsync)
dotnet ef database update `
    --project backend/src/AiIssueTracker.Api `
    --startup-project backend/src/AiIssueTracker.Api
```

---

## OpenAPI / Scalar (when API is running)

| Path | Purpose |
|------|---------|
| `/openapi/v1.json` | Raw OpenAPI spec |
| `/scalar/v1` | Interactive Scalar UI (pre-fills `X-Bff-Secret`) |
