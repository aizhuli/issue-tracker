---
name: ui-design
description: Use before writing ANY frontend component or page for Aigo - Issue Board — defines the full design system, CSS class library, component primitives, layout patterns, motion, and do/don't rules (project)
---

# Aigo — UI Design System

**Announce at the start:** "I'm using the ui-design skill to implement this component."

Use this skill before writing any React component, CSS, or Tailwind for this project. It is the single source of truth for visual decisions.

---

## 1. Brand & App Name

The product is called **Aigo - Issue Board**. Use this exact string in all UI. The logo SVG lives in `design_files/chrome.jsx` (`<svg>` inside `.sb-logo`). Reproduce it exactly — do not substitute an emoji or icon library glyph.

---

## 2. Design Tokens

All tokens are CSS custom properties on `:root` in `frontend/styles.css`. Use them everywhere — never hard-code colours or raw pixel values that duplicate a token.

### Surfaces

| Token | Value | Use |
|---|---|---|
| `--paper` | `#F2F5EE` | App background, page background |
| `--surface` | `#FFFFFF` | Cards, inputs, modals |
| `--surface-2` | `#ECF0E6` | Subtle fills, column bodies, compose footers |
| `--surface-3` | `#E4EADB` | Hover states, deeper fills |
| `--border` | `#DCE2D2` | Card and input borders |
| `--border-2` | `#E7ECDD` | Subtle dividers between sections |
| `--border-3` | `#C9D3BB` | Focused/active borders |

### Ink (text)

| Token | Value | Use |
|---|---|---|
| `--ink-0` | `#18342A` | Primary text — deep forest |
| `--ink-1` | `#2D483D` | Strong secondary, headings |
| `--ink-2` | `#607265` | Muted labels, meta text |
| `--ink-3` | `#8A9489` | Placeholders, timestamps, monochrome icons |

### Accent — Lime (default)

| Token | Value | Use |
|---|---|---|
| `--accent-1` | `#B6DF7B` | Primary accent, CTA backgrounds |
| `--accent-1-strong` | `#9BCB57` | Hover on accent CTAs |
| `--accent-1-ink` | `#1B3A1B` | Text on `--accent-1` backgrounds |
| `--accent-2` | `#DCE9CC` | Sage pill backgrounds, active sidebar items |
| `--accent-2-ink` | `#3D5424` | Text on `--accent-2` backgrounds |

The app supports four accent presets (user-switchable). All are set via JS on `document.documentElement`. Always use the CSS variables — never reference the raw hex.

### Status colours

| Status | Token | Value |
|---|---|---|
| Backlog | `--st-backlog` | `#A8B0A2` |
| Todo | `--st-todo` | `#7B95B8` |
| In Progress | `--st-progress` | `#D4A24C` |
| In Review | `--st-review` | `#9E7BC1` |
| Done | `--st-done` | `#6FAE5A` |

### Shape & depth

| Token | Value |
|---|---|
| `--radius` | `12px` — standard card/panel |
| `--radius-lg` | `16px` — modals |
| `--radius-sm` | `8px` — chips, small elements |
| `--shadow-sm` | Cards at rest |
| `--shadow-md` | Cards on hover |
| `--shadow-lg` | Drawers, modals, overlays |

### Layout dimensions

| Token | Value |
|---|---|
| `--sb-w` | `240px` — sidebar width |
| `--topbar-h` | `56px` (48px compact) |
| `--viewbar-h` | `44px` (38px compact) |

---

## 3. Typography

| Family | Variable | Use |
|---|---|---|
| `Manrope` | — | All UI text (weights: 400, 500, 600, 700, 800) |
| `JetBrains Mono` | — | Issue IDs, kbd keys, WIP limits, numeric badges |

### Scale reference (no utility classes — set `font-size` inline or via class)

| Use | Size | Weight |
|---|---|---|
| Drawer title | 22px | 700 |
| Card title | 13px | 600 |
| Body / description | 13.5px | 400 |
| Labels, nav items | 12–12.5px | 500–600 |
| Timestamps, meta | 11–11.5px | 400–500 |
| Badge / section header | 10–10.5px | 700 |
| Monospace IDs | 10.5–12px | 500–600 |

Always set `-webkit-font-smoothing: antialiased` at the body level (already done in the base stylesheet).

---

## 4. Icon Component

`Icon` is a shared SVG component in `components/`. Accepts `name`, `size` (default 16), `color` (default `currentColor`), and `stroke` (default 1.6).

```tsx
<Icon name="sparkle" size={14} />
<Icon name="close" size={14} color="var(--ink-2)" />
```

### Available icon names

`inbox` `board` `list` `backlog` `sprint` `folder` `me` `settings` `search` `plus` `chevron` `down` `filter` `sort` `close` `link` `paperclip` `comment` `sparkle` `bell` `help` `grid` `archive` `dots` `enter` `flag` `calendar` `branch` `play`

Do not import from Lucide, Heroicons, or any icon library. If a new icon is needed, add its SVG path to the `paths` map in `Icon`.

---

## 5. Primitive Components

### Avatar

Renders a circular avatar with initials, coloured by the member's `hue` (oklch).

```tsx
<Avatar id="u3" size={28} />       // member id + optional size
<AvatarStack ids={['u1','u2']} size={20} max={4} />
```

Never render a raw `<div>` with initials — always use `Avatar`.

### StatusDot

Renders a pie-progress circle for workflow status.

```tsx
<StatusDot id="in_progress" size={9} />
// ids: backlog | todo | in_progress | in_review | done
```

### PriorityGlyph

Renders a three-bar priority indicator.

```tsx
<PriorityGlyph level="high" size={14} />
// levels: urgent | high | medium | low
```

### Label (pill)

Renders a coloured pill with a dot. Colour is deterministic from the label text — no prop needed.

```tsx
<Label text="bug" />
<Label text="frontend" />
```

---

## 6. Semantic Colour Usage

| Situation | Colour |
|---|---|
| Destructive / error border | `#B94D2F` |
| Error pill background | `#F0DDDD` |
| Error pill text | `#7A3535` |
| AI feature accent | `var(--ink-0)` bg + `#E7F4D6` text |
| AI summary card | `linear-gradient(180deg, #E8F0DA 0%, #DCE9CC 100%)`, border `#C5D9A8` |
| WIP over-limit | `#B94D2F` (`.col-wip.over`) |

---

## 7. CSS Class Library

Reuse existing classes from `styles.css` before writing new CSS. Key classes by zone:

### App shell
- `.app` — grid shell (`sidebar + main`)
- `.main` — flex column content area

### Sidebar
- `.sidebar` — aside panel
- `.sb-workspace` — logo + workspace row
- `.sb-quick-btn.primary` — accent CTA button (e.g. "New issue")
- `.sb-quick-btn.ghost` — icon-only bordered button
- `.sb-item` / `.sb-item.active` — nav item
- `.sb-section` / `.sb-section-hd` — nav section with label
- `.sb-project` / `.sb-project.active` — project row
- `.sb-foot-btn` — bottom settings/help button

### Top bar
- `.topbar` — fixed-height header
- `.tb-pill` — label+icon pill button (default: bordered white)
- `.tb-pill.ai` — dark AI button with sparkle
- `.tb-search` — search input wrapper
- `.tb-icon-btn` — circular icon button
- `.tb-dot` — notification dot on icon button

### View bar
- `.viewbar` — secondary toolbar below topbar
- `.vb-tabs` / `.vb-tab` / `.vb-tab.active` — board/list/backlog tabs
- `.vb-chip` — filter/sort chip

### Kanban board
- `.board` — 5-column grid
- `.col` / `.col.drag-over` — column card
- `.col-hd` / `.col-name` / `.col-count` / `.col-wip` — column header elements
- `.col-body` — scrollable card list
- `.col-add-card` — dashed add button at bottom of column

### Issue card
- `.card` / `.card.compact` — card container
- `.card-top` / `.card-id` / `.card-title` — card header
- `.card-labels` — label row
- `.card-bottom` / `.card-meta` / `.card-points` / `.card-stat` — card footer

### List view
- `.list` / `.list-group` / `.list-group-hd` — list container and group
- `.row` — 8-column grid row
- `.row-id` / `.row-title` / `.row-labels` / `.row-points` / `.row-updated` — row cells

### Issue drawer
- `.drawer-scrim` — semi-transparent overlay behind drawer
- `.drawer` — right-side panel (380–880px wide)
- `.dr-hd` / `.dr-hd-l` / `.dr-hd-r` — drawer header
- `.dr-id` — monospace issue ID badge
- `.dr-status-pill` — status selector pill
- `.dr-icon-btn` — small icon button (28×28)
- `.dr-body` — `1fr 260px` grid (main + sidebar)
- `.dr-main` / `.dr-side` — drawer content areas
- `.dr-title` — large editable title
- `.dr-ai-card` / `.dr-ai-hd` / `.dr-ai-body` / `.dr-ai-badge` — AI summary card
- `.dr-desc` — markdown description area
- `.dr-tabs` / `.dr-tab` / `.dr-tab.active` / `.dr-tab-count` — tab strip
- `.dr-comments` / `.dr-comment` / `.dr-comment-hd` — comment list
- `.dr-comment-compose` / `.dr-compose-box` / `.dr-compose-send` — compose area
- `.dr-activity` / `.dr-act-row` — activity feed
- `.dr-links` / `.dr-link-row` — linked items
- `.dr-field` / `.dr-field-lbl` / `.dr-field-val` / `.dr-field-btn` — property row in sidebar
- `.dr-side-foot` — created/updated timestamps

### AI command bar
- `.ai-scrim` / `.ai-modal` — full-screen overlay + command modal
- `.ai-hd` — input row
- `.ai-preview` / `.ai-preview-lbl` / `.ai-card-preview` — preview area
- `.ai-chips` / `.ai-chip` — parsed field chips
- `.ai-foot` / `.ai-cta` — footer with keyboard hints + create button

### Utility
- `.kbd` — keyboard key badge (JetBrains Mono, 10px, `--surface-3` bg)
- `.kbd-dark` — dark variant for use on coloured buttons

---

## 8. Motion & Animation

### Existing keyframes (reuse from `styles.css`)

| Name | Effect | Use |
|---|---|---|
| `slide-in` | `translateX(20px) → 0`, 220ms `cubic-bezier(0.2,0.7,0.2,1)` | Drawer, auth panel entrance |
| `fade` | `opacity 0 → 1`, 180ms | Scrim entrances |
| `ai-pop` | `translateX(-50%) translateY(-8px) → 0`, 160ms | Modal pop-in |

### Durations

- Micro-interactions (hover backgrounds, border colour): 100–120ms
- Panel slides: 220ms
- Modal pops: 160ms
- Card drag feedback: 150ms

### Card entry (new items appearing)

Apply a CSS class one frame after mount (via `requestAnimationFrame`):
```css
.card-enter { opacity: 0; transform: translateY(-6px); }
.card-enter-active { opacity: 1; transform: translateY(0); transition: opacity 250ms, transform 250ms; }
```

---

## 9. Layout Patterns

### Authenticated app shell

```
┌──────────────────────────────────────────┐
│  Sidebar (240px) │ Main (flex-1)         │
│                  │ ┌──────────────────┐  │
│                  │ │ TopBar (56px)    │  │
│                  │ ├──────────────────┤  │
│                  │ │ ViewBar (44px)   │  │
│                  │ ├──────────────────┤  │
│                  │ │ Content (flex-1) │  │
│                  │ └──────────────────┘  │
└──────────────────────────────────────────┘
```

### Auth pages (`/login`, `/register`)

No sidebar, topbar, or viewbar.

```
┌──────────────────────────────┬──────────┐
│  DemoBoard (flex-1)          │  Panel   │
│  pointer-events: none        │  380px   │
│  filter: saturate(0.85)      │  fixed   │
│  [gradient fade on right]    │  right   │
│                              │          │
│  [Logo pill top-left]        │          │
└──────────────────────────────┴──────────┘
```

- Board behind: `KanbanBoard` + `MOCK_DATA`, wrapped `pointer-events: none`.
- Right-edge fade: `linear-gradient(to right, transparent 0%, var(--paper) 100%)` over last 60px.
- Panel: same as `.drawer` — `border-left`, `box-shadow: var(--shadow-lg)`, `slide-in` on mount.
- Logo pill: `position: absolute; top: 16px; left: 18px` — logo SVG + "Aigo - Issue Board".

### Drawer / side panel

`min(880px, 92vw)` wide. Backed by `.drawer-scrim`. Slide-in from right. Esc closes.

### Modal / command bar

`min(640px, 92vw)` wide, `top: 18vh`. Backed by scrim. `ai-pop` entrance.

---

## 10. Form Patterns (Auth & General)

### Field anatomy

```
[Label — 11px, 600, --ink-2]
[Input — full width, background: var(--surface), border: 1px solid var(--border)]
[Error — 12px, color: #B94D2F]  ← only visible on error
```

On error: border shifts to `#B94D2F`.

### Password input

Standard input + an absolute-positioned eye icon button on the right. Use `Icon name="help"` or a bespoke eye path. Toggle `type="password"` / `type="text"`.

### CTA button (primary)

```css
height: 40px;
background: var(--accent-1);
color: var(--accent-1-ink);
border-radius: 9px;
font-size: 13px;
font-weight: 700;
width: 100%;
```

Hover: `background: var(--accent-1-strong)`.
Disabled / loading: `background: var(--surface-3); color: var(--ink-3); cursor: not-allowed`.

### Form-level error pill

```css
background: #F0DDDD;
color: #7A3535;
border-radius: 8px;
padding: 8px 12px;
font-size: 12.5px;
```

Place above the CTA button.

### Loading spinner

CSS-only. A `border`-based circle with one transparent side + `animation: spin 0.7s linear infinite`. No external library.

---

## 11. Demo Board Animation

Used on auth pages. Implemented as `useDemoAnimation(issues, setIssues)`.

- **Timer 1 (3.5s):** Advance a random non-done card one status step. Done cards reset to backlog after 2s.
- **Timer 2 (6s):** Insert a new mock card. After 4 new cards, pause 12s.
- Both timers use `useRef` to hold the interval ID (no stale closure).
- Card entry: CSS class toggled via `requestAnimationFrame` for opacity + translateY transition.
- Board visual: `filter: saturate(0.85)` on the wrapper.

---

## 12. Density Mode

`body.density-compact` is toggled by user preference. When present:
- `--topbar-h: 48px`, `--viewbar-h: 38px`
- Cards: tighter padding, smaller title font, labels hidden
- Board: tighter gap and padding

Always test both densities when building board or card components.

---

## 13. Keyboard & Accessibility

- All interactive elements are `<button>` (not `<div onClick>`).
- Modals and drawers trap focus and close on `Escape`.
- `kbd.kbd` renders keyboard shortcut hints in JetBrains Mono 10px.
- `aria-label` on icon-only buttons.
- Colour is never the only indicator of state — always pair with shape, text, or icon.

---

## 14. Mock Data Reference

`MOCK_DATA` in `design_files/data.js` / `components/demo/mockData.ts`:

**Members:** `u1` Maya Chen (Design), `u2` Jordan Reyes (Eng), `u3` Priya Shah (PM), `u4` Theo Nilsson (Eng), `u5` Imani Brooks (Eng), `u6` Sam Okafor (Design).

**Projects:** `p1` Web Platform `#7AB85E`, `p2` Mobile App `#D4A24C`, `p3` Public API `#7B95B8`, `p4` Design System `#9E7BC1`.

**Status IDs:** `backlog` `todo` `in_progress` `in_review` `done`.

**Priority levels:** `urgent` `high` `medium` `low`.

---

## 15. Do / Don't

| Do | Don't |
|---|---|
| Use CSS custom properties for all colours | Hard-code hex values |
| Reuse existing CSS classes | Duplicate existing styles with new class names |
| Use `<Icon name="…">` for all icons | Import from Lucide / Heroicons / Feather |
| Use `<Avatar>` for user avatars | Render raw initials divs |
| Use `<Label text="…">` for tags | Roll your own tag with a different colour scheme |
| Animate with existing keyframes | Add new animation libraries |
| Use `Manrope` for all UI text | Use system fonts or other Google Fonts |
| Use `JetBrains Mono` for IDs and kbd | Use `monospace` generic family |
| Close modals / drawers on `Escape` | Ignore keyboard dismissal |
| Test compact and regular density | Build for only one density |
| Write `<button>` for clickable elements | Use `<div onClick>` |

---

## 16. Implementation Checklist

Before submitting any frontend component:

- [ ] Only CSS tokens used — no raw colour hex values
- [ ] `Manrope` / `JetBrains Mono` only — no other fonts
- [ ] `<Icon>` used for all icons
- [ ] `<Avatar>` / `<Label>` / `<StatusDot>` / `<PriorityGlyph>` used for domain primitives
- [ ] Existing CSS classes reused where applicable
- [ ] `<button>` for all interactive elements
- [ ] `aria-label` on icon-only buttons
- [ ] `Escape` closes modals / drawers
- [ ] Tested in regular and compact density
- [ ] No external UI libraries (no Radix, no shadcn, no MUI, no Chakra) unless explicitly approved
