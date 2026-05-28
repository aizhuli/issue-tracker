# AI Triage

Single-shot LLM feature that suggests a priority, a set of labels, and acceptance criteria for an existing issue. The suggestion is returned to the caller for review — no database writes occur.

## API Endpoints

| Method | Path | Authorization | Description |
|--------|------|---------------|-------------|
| `POST` | `/api/projects/{slug}/issues/{number}/ai/triage` | RequireUser | Request a triage suggestion for the issue |

### Request body

```json
{ "title": "string", "description": "string?" }
```

### Response body (`TriageSuggestion`)

```json
{
  "priority": "low | medium | high | urgent",
  "labels": [{ "id": "Base32", "name": "string", "color": "string" }],
  "acceptanceCriteria": "markdown string"
}
```

## Key Behaviors

- The endpoint requires both the project (`slug`) and the issue (`number`) to exist; returns `projects:project:not_found` or `issues:issue:not_found` otherwise.
- The prompt sent to the LLM includes: project name, project description (when present), the project's full label set, and the issue title and description.
- The LLM is instructed to choose only from the project's existing labels. Labels in the response are matched case-insensitively against the project's label set; unrecognized label names are silently dropped.
- If the returned priority is not a valid value, the issue's current priority is used as the fallback.
- Acceptance criteria are trimmed and capped at 10 000 characters.
- The LLM call uses JSON response mode at temperature 0.2. On a JSON parse failure the call is retried once; a second failure returns `ai:triage:llm:invalid_response` (502).
- LLM calls are bounded by a configurable timeout (`Llm:TimeoutSeconds`, default 30 s). Timeout or network failure returns `ai:triage:llm:unavailable` (502).

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `Llm:BaseUrl` | — | OpenAI-compatible endpoint base URL |
| `Llm:Model` | — | Model identifier |
| `Llm:ApiKey` | — | API key (omit for unauthenticated local endpoints) |
| `Llm:TimeoutSeconds` | `30` | Per-request timeout in seconds |

## Validation

| Field | Rule | Error code |
|-------|------|------------|
| `title` | Required, max 200 chars | `ai:triage:title:required_or_too_long` |
| `description` | Max 10 000 chars | `ai:triage:description:too_long` |

## BFF Route

The BFF exposes `POST /api/projects/[slug]/issues/[number]/ai/triage` as a passthrough. It requires an active session and attaches `X-Bff-Secret` + `X-User-Id` before forwarding to the backend.
