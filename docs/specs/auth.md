# Auth

Authenticates users with email and password. The ASP.NET API is only reachable through the Next.js BFF: the browser holds an encrypted `iron-session` cookie, and the BFF forwards each request with a shared secret header plus the user's id. There is no JWT, no refresh token, and no server-side session store.

## Data Model

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| User | `Id: long`, `Email`, `PasswordHash`, `Name`, `Avatar?`, `CreatedAt` | Unique index on `Email` (stored lower-cased). `Email` max 254, `Name` max 100. Password hashed with PBKDF2 via `PasswordHasher<User>`. Public `Id` is Crockford Base32. |

## API Endpoints

All API endpoints require the `X-Bff-Secret` header. `RequireUser` additionally requires `X-User-Id` (Base32-encoded user id).

| Method | Path | Authorization | Description |
|--------|------|---------------|-------------|
| POST | `/api/auth/register` | BffOnly | Create a new user. Returns `{ id, email, name }`. |
| POST | `/api/auth/login` | BffOnly | Verify credentials. Returns `{ id, email, name }`. |
| GET | `/api/auth/me` | RequireUser | Return the current user's `{ id, email, name, avatar }`. |

### BFF Route Handlers

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/auth/register` | Forwards to the API. On success, writes `{ userId, email, name }` to the session cookie. |
| POST | `/api/auth/login` | Forwards to the API. On success, writes `{ userId, email, name }` to the session cookie. |
| POST | `/api/auth/logout` | Destroys the session cookie. Returns 204. No upstream call. |
| GET | `/api/auth/me` | Returns 401 with `errorCode: auth:session:missing` when no session is present; otherwise forwards to the API. |

### Browser Route Protection

A Next.js middleware (`frontend/proxy.ts`) gates every browser-facing path. Public paths — `/login`, `/register`, `/api/auth/*`, `/_next/*`, and static assets — pass through. Every other path requires `session.user`; if absent, the request is redirected to `/login?returnTo=<original-path>`.

## Key Behaviors

- The BFF cookie (`ait_session`, HTTP-only, SameSite=lax, encrypted) is the only browser-facing credential. Its payload is `{ user: { userId, email, name } }`.
- The BFF attaches `X-Bff-Secret` on every API call. When a session exists, it also attaches `X-User-Id` set to the Base32-encoded user id.
- The API runs a single authentication scheme (`BffAuth`). The handler validates the shared secret in constant time, decodes `X-User-Id` into a `ClaimTypes.NameIdentifier` claim (raw `long`), and also exposes the encoded id as the `encoded_id` claim.
- `BffOnly` policy: valid `X-Bff-Secret` is required. `RequireUser` policy: valid `X-Bff-Secret` plus a `NameIdentifier` claim is required.
- `ICurrentUser.UserId` reads the `NameIdentifier` claim and is the only way handlers access the calling user.
- Register normalizes the email (trim + lower-case) and rejects duplicates with `auth:user:email:already_exists`.
- Login returns the same `auth:credentials:invalid` error for unknown email and wrong password — no user enumeration.
- `Me` returns `auth:user:not_found` (404) when the `X-User-Id` claim points at a user that no longer exists (stale session). The BFF treats this as a signal to destroy the cookie on the next interaction.
- Logout is a BFF-only operation; there is no server state to revoke.
- All errors are returned as RFC 7807 ProblemDetails with `errorCode` and `traceId`. The BFF passes them through unchanged.

### Validation Rules

| Field | Register | Login |
|-------|----------|-------|
| `email` | required, valid format, ≤254 chars | required, valid format |
| `password` | required, 8–128 chars | required |
| `name` | required, ≤100 chars | — |

### Error Codes

| Code | Status | Where |
|------|--------|-------|
| `auth:user:email:required` / `invalid_format` / `too_long` | 400 | Register, Login |
| `auth:user:password:required` / `too_short` / `too_long` | 400 | Register, Login |
| `auth:user:name:required` / `too_long` | 400 | Register |
| `auth:user:email:already_exists` | 409 | Register |
| `auth:credentials:invalid` | 401 | Login |
| `auth:user:not_found` | 404 | Me |
| `auth:current_user:unauthenticated` | 401 | Any handler reading `ICurrentUser.UserId` without a `NameIdentifier` claim |
| `auth:session:missing` | 401 | BFF `/api/auth/me` when no cookie |

## Configuration

| Setting | Consumer | Source |
|---------|----------|--------|
| `BffAuth:SharedSecret` | API | Aspire parameter, injected as `BffAuth__SharedSecret`. |
| `BFF_SHARED_SECRET` | BFF | Aspire parameter; sent as `X-Bff-Secret`. |
| `SESSION_COOKIE_PASSWORD` | BFF | Aspire parameter; encrypts the `iron-session` cookie. |
| `API_URL` | BFF | Aspire service discovery; base URL for `serverFetch`. |
