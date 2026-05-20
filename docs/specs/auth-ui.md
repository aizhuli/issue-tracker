# Auth UI

Login and registration pages for the Next.js frontend. A full-viewport animated Kanban board renders behind a centered frosted-glass panel containing a tabbed auth form. Users see the product before they log in.

## Pages

| Route | Component | Description |
|-------|-----------|-------------|
| `/login` | `AuthShell` (activeTab="login") | Renders the shell with the Log in tab active |
| `/register` | `AuthShell` (activeTab="register") | Renders the shell with the Sign up tab active |

Both routes share `app/(auth)/layout.tsx` — no sidebar, topbar, or viewbar.

## Component Tree

| Component | File | Purpose |
|-----------|------|---------|
| `AuthShell` | `components/auth/AuthShell.tsx` | Full-viewport shell: DemoBoard + overlay + centered panel + brand pill |
| `DemoBoard` | `components/auth/DemoBoard.tsx` | KanbanBoard fed `MOCK_DATA`, runs `useDemoAnimation`, non-interactive |
| `useDemoAnimation` | `components/auth/useDemoAnimation.ts` | Two `setInterval` timers driving card movement and card insertion |
| `AuthPanel` | `components/auth/AuthPanel.tsx` | Logo header, tab strip, form area, footer |
| `LoginForm` | `components/auth/LoginForm.tsx` | Email + password fields, submit, error display |
| `RegisterForm` | `components/auth/RegisterForm.tsx` | Name + email + password fields, submit, error display |
| `FormField` | `components/ui/FormField.tsx` | Label + input + inline error message |
| `PasswordInput` | `components/ui/PasswordInput.tsx` | Labeled input with show/hide toggle |
| `FormError` | `components/ui/FormError.tsx` | Form-level error pill |

## Validation Schemas (`lib/schemas/auth.ts`)

| Schema | Fields | Rules |
|--------|--------|-------|
| `loginSchema` | `email`, `password` | email valid + ≤254; password ≥1 char |
| `registerSchema` | `name`, `email`, `password` | name ≥1 + ≤100; email valid + ≤254; password 8–128 chars |

## Key Behaviors

### Layout

- `AuthShell` renders `DemoBoard` full-screen, then a `rgba(18,34,26,0.18)` overlay, then a centered frosted-glass panel (`backdrop-filter: blur(12px) saturate(1.1)`, `max-width: 520px`, `min-width: 340px`).
- A brand pill (logo SVG + "Aigo - Issue Board") is pinned at top-left over the board; it is `pointer-events: none`.
- `DemoBoard` is non-interactive (`pointer-events: none`, `user-select: none`).

### Tab navigation

- `AuthPanel` renders a `.dr-tabs` / `.dr-tab` strip with "Log in" and "Sign up".
- Clicking a tab calls `router.push('/login')` or `router.push('/register')` — no full reload.
- The form area is fixed at `minHeight: 300` to prevent panel resize when switching tabs.

### Board animation

- **Timer 1 (3.5 s):** picks a random non-`done` card, advances it one status step (`backlog → todo → in-progress → in-review → done`). Cards that reach `done` reset to `backlog` after a 2 s pause.
- **Timer 2 (6 s):** inserts a new mock card into a random non-`done` column with a 250 ms CSS enter animation (`opacity: 0→1`, `translateY(-6px)→0`). After 4 insertions the timer pauses for 12 s.
- Both timers use functional `setIssues` updates to avoid stale closures; interval IDs are held in `useRef`.

### Submission flow

1. Zod validation runs client-side; field errors appear immediately without a network call.
2. On Zod success the form `POST`s to `/api/auth/login` or `/api/auth/register` (BFF route handler).
3. The BFF forwards to the ASP.NET API and passes ProblemDetails through unchanged.
4. The frontend reads `errorCode` from the response and maps it to field-level or form-level messages.
5. On success the BFF writes the session cookie; the frontend reads `returnTo` from the query string (must start with `/`) and calls `router.push` — no full page reload.

### Loading state

The CTA button disables and shows a CSS `border`-based spinner while the fetch is in flight. No external spinner library is used.

### Route protection

Next.js middleware redirects authenticated users away from `/login` and `/register`. Every other path redirects unauthenticated users to `/login?returnTo=<original-path>`.

## Error Mapping

| `errorCode` | Display | Message |
|---|---|---|
| `auth:user:email:required` / `invalid_format` / `too_long` | Below email field | "Enter a valid email address" |
| `auth:user:password:required` / `too_short` / `too_long` | Below password field | "Password must be 8–128 characters" |
| `auth:user:name:required` / `too_long` | Below name field | "Name is required (max 100 chars)" |
| `auth:user:email:already_exists` | Below email field | "An account with this email already exists" |
| `auth:credentials:invalid` | Form-level pill | "Incorrect email or password" |
| Network / 5xx | Form-level pill | "Something went wrong — please try again" |

Form-level pill style: `background: #F0DDDD`, `color: #7A3535`, `border-radius: 8px`, `padding: 8px 12px`, `font-size: 12.5px`.

## Out of Scope

- Password reset, email verification, OAuth.
- Remember me / persistent sessions.
- CAPTCHA or rate-limit UI.
- Mobile / responsive layout (demo targets desktop).
- Vitest / Playwright tests for auth UI.
