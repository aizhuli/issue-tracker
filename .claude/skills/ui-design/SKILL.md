---
name: ui-design
description: Use before writing ANY frontend component or page for Aigo - Issue Board — defines the full design system, CSS class library, component primitives, layout patterns, motion, and do/don't rules (project)
---

# Aigo — UI Design System

**Announce at the start:** "I'm using the ui-design skill to implement this component."

Single source of truth for all visual decisions. Read before writing any React component, CSS, or Tailwind.

---

## 1. Brand

Product name: **Aigo - Issue Board** (exact string everywhere). Logo SVG is in `design_files/chrome.jsx` (`.sb-logo`). Reproduce exactly — no emoji or icon-library substitutes.

---

## 2. Design Tokens

All tokens are CSS custom properties on `:root` in `frontend/styles.css`. Never hard-code colours or pixel values that duplicate a token.

| Group | Token | Value | Use |
|---|---|---|---|
| Surface | `--paper` | `#F2F5EE` | Page / app background |
| | `--surface` | `#FFFFFF` | Cards, inputs, modals |
| | `--surface-2` | `#ECF0E6` | Column bodies, compose footers |
| | `--surface-3` | `#E4EADB` | Hover fills |
| Border | `--border` | `#DCE2D2` | Cards, inputs |
| | `--border-2` | `#E7ECDD` | Section dividers |
| | `--border-3` | `#C9D3BB` | Focused/active |
| Ink | `--ink-0` | `#18342A` | Primary text |
| | `--ink-1` | `#2D483D` | Headings, strong secondary |
| | `--ink-2` | `#607265` | Labels, meta |
| | `--ink-3` | `#8A9489` | Placeholders, timestamps |
| Accent | `--accent-1` | `#B6DF7B` | CTA backgrounds |
| | `--accent-1-strong` | `#9BCB57` | CTA hover |
| | `--accent-1-ink` | `#1B3A1B` | Text on accent-1 |
| | `--accent-2` | `#DCE9CC` | Pill backgrounds, active sidebar |
| | `--accent-2-ink` | `#3D5424` | Text on accent-2 |
| Status | `--st-backlog` | `#A8B0A2` | |
| | `--st-todo` | `#7B95B8` | |
| | `--st-progress` | `#D4A24C` | |
| | `--st-review` | `#9E7BC1` | |
| | `--st-done` | `#6FAE5A` | |
| Shape | `--radius` | `12px` | Cards, panels |
| | `--radius-lg` | `16px` | Modals |
| | `--radius-sm` | `8px` | Chips |
| | `--shadow-sm/md/lg` | — | Rest / hover / overlays |
| Layout | `--sb-w` | `240px` | Sidebar |
| | `--topbar-h` | `56px` (48px compact) | |
| | `--viewbar-h` | `44px` (38px compact) | |

Four accent presets are user-switchable via JS on `document.documentElement`. Always use the CSS variable.

### Semantic colour exceptions (raw hex required)

| Situation | Value |
|---|---|
| Error border | `#B94D2F` |
| Error pill bg / text | `#F0DDDD` / `#7A3535` |
| AI feature accent | `var(--ink-0)` bg + `#E7F4D6` text |
| AI summary card | `linear-gradient(180deg,#E8F0DA 0%,#DCE9CC 100%)`, border `#C5D9A8` |

---

## 3. Typography

| Family | Use |
|---|---|
| `Manrope` 400–800 | All UI text |
| `JetBrains Mono` 500–600 | Issue IDs, kbd keys, WIP limits |

**Scale** (set inline — no utility classes):

| Context | Size | Weight |
|---|---|---|
| Drawer title | 22px | 700 |
| Card title | 13px | 600 |
| Body | 13.5px | 400 |
| Labels / nav | 12–12.5px | 500–600 |
| Meta / timestamps | 11–11.5px | 400–500 |
| Badges | 10–10.5px | 700 |
| Mono IDs | 10.5–12px | 500–600 |

---

## 4. Icons & Primitives

### Icon

`<Icon name="…" size={16} color="currentColor" stroke={1.6} />` — shared SVG component in `components/`. Never import from Lucide/Heroicons. Add new icons to the `paths` map in `Icon`.

Available names: `inbox board list backlog sprint folder me settings search plus chevron down filter sort close link paperclip comment sparkle bell help grid archive dots enter flag calendar branch play`

### Domain primitives — always use these, never roll your own

| Component | Usage |
|---|---|
| `<Avatar id="u3" size={28} />` | Circular avatar with initials (oklch hue) |
| `<AvatarStack ids={[…]} size={20} max={4} />` | Overlapping avatar row |
| `<StatusDot id="in_progress" size={9} />` | Pie-progress circle; ids: `backlog todo in_progress in_review done` |
| `<PriorityGlyph level="high" size={14} />` | Three-bar indicator; levels: `urgent high medium low` |
| `<Label text="bug" />` | Coloured pill; colour is deterministic from text |

---

## 5. CSS Class Library

Reuse classes from `frontend/app/globals.css` before writing new CSS.

**App shell:** `.app` (grid) · `.main` (flex column)

**Sidebar:** `.sidebar` · `.sb-workspace` · `.sb-quick-btn.primary/.ghost` · `.sb-item[.active]` · `.sb-section` · `.sb-section-hd` · `.sb-project[.active]` · `.sb-foot-btn`

**Top bar:** `.topbar` · `.tb-pill[.ai]` · `.tb-search` · `.tb-icon-btn` · `.tb-dot`

**View bar:** `.viewbar` · `.vb-tabs` · `.vb-tab[.active]` · `.vb-chip`

**Board:** `.board` · `.col[.drag-over]` · `.col-hd` · `.col-name` · `.col-count` · `.col-wip` · `.col-body` · `.col-add-card`

**Issue card:** `.card[.compact]` · `.card-top` · `.card-id` · `.card-title` · `.card-labels` · `.card-bottom` · `.card-meta` · `.card-points` · `.card-stat`

**List view:** `.list` · `.list-group[.list-group-hd]` · `.row` · `.row-id` · `.row-title` · `.row-labels` · `.row-points` · `.row-updated`

**Drawer:** `.drawer-scrim` · `.drawer` · `.dr-hd[.dr-hd-l/.dr-hd-r]` · `.dr-id` · `.dr-status-pill` · `.dr-icon-btn` · `.dr-body` · `.dr-main/.dr-side` · `.dr-title` · `.dr-ai-card/.dr-ai-hd/.dr-ai-body/.dr-ai-badge` · `.dr-desc` · `.dr-tabs` · `.dr-tab[.active][.dr-tab-count]` · `.dr-comments/.dr-comment/.dr-comment-hd` · `.dr-comment-compose/.dr-compose-box/.dr-compose-send` · `.dr-activity/.dr-act-row` · `.dr-links/.dr-link-row` · `.dr-field/.dr-field-lbl/.dr-field-val/.dr-field-btn` · `.dr-side-foot`

**AI command bar:** `.ai-scrim` · `.ai-modal` · `.ai-hd` · `.ai-preview/.ai-preview-lbl/.ai-card-preview` · `.ai-chips/.ai-chip` · `.ai-foot/.ai-cta`

**Utility:** `.kbd` (JetBrains Mono 10px, `--surface-3` bg) · `.kbd-dark`

---

## 6. Motion

| Keyframe | Effect | Use |
|---|---|---|
| `slide-in` | `translateX(20px)→0`, 220ms ease | Drawer entrance — **not auth panel** (page remount retriggers it) |
| `fade` | `opacity 0→1`, 180ms | Scrim |
| `ai-pop` | `translateY(-8px)→0`, 160ms | Modal |
| `card-enter/card-enter-active` | `opacity+translateY(-6px)→0`, 250ms | New card appearing in board |

Durations: hover/border 100–120ms · panel slide 220ms · modal 160ms · drag feedback 150ms.

---

## 7. Layout Patterns

### Authenticated shell
```
┌─────────────────────────────────────────┐
│  Sidebar (--sb-w) │ Main (flex-1)       │
│                   │  TopBar (--topbar-h) │
│                   │  ViewBar (--vh)      │
│                   │  Content (flex-1)    │
└─────────────────────────────────────────┘
```

### Auth pages (`/login`, `/register`) — no sidebar/topbar/viewbar
```
┌─────────────────────────────────────────────┐
│  DemoBoard  position:absolute; inset:0       │
│  filter:blur(5px) saturate(0.85)            │
│  transform:scale(1.06)  ← hides blur edges  │
│  pointer-events:none                        │
│                                             │
│  [dark tint  rgba(18,34,26,0.18)]           │
│                                             │
│        ┌──────────────────────┐             │
│        │  Auth Panel          │             │
│        │  width:min(520px,50vw)│            │
│        │  minWidth:340px      │             │
│        │  frosted glass       │             │
│        └──────────────────────┘             │
│  [Logo pill — top: 16px; left: 18px]        │
└─────────────────────────────────────────────┘
```

**Panel:** centered via `top:50%;left:50%;transform:translate(-50%,-50%)` · `maxHeight:90vh;overflow-y:auto` · `background:rgba(242,245,238,0.88)` · `backdrop-filter:blur(12px) saturate(1.1)` · `border:1px solid rgba(220,226,210,0.7)` · `border-radius:var(--radius-lg)` · `box-shadow:var(--shadow-lg)` · no `slide-in` class.

**Form area inside panel:** `minHeight:300px` — prevents resize when switching Log in (2 fields) ↔ Sign up (3 fields).

**Logo pill:** `background:rgba(255,255,255,0.85)` · `backdrop-filter:blur(8px)` · `border-radius:999px`.

**Demo board animation** (`useDemoAnimation`): Timer 1 (3.5s) advances a random card one step; done cards reset to backlog after 2s. Timer 2 (6s) inserts a new mock card; pauses 12s after 4 inserts. Timers held in `useRef`. Card entry uses `requestAnimationFrame` to toggle `.card-enter` → `.card-enter-active`.

### Drawer: `min(880px, 92vw)` · `.drawer-scrim` · `slide-in` · Esc closes.
### Modal: `min(640px, 92vw)` · `top:18vh` · `ai-pop` entrance.

---

## 8. Form Patterns

**Field:** label 11px/600/`--ink-2` → input `background:var(--surface); border:1px solid var(--border)` → error 12px `#B94D2F` (border also shifts to `#B94D2F`).

**Password:** standard input + absolute eye-icon toggle (`type="password"`↔`type="text"`).

**CTA button:** `height:40px; background:var(--accent-1); color:var(--accent-1-ink); border-radius:9px; font-size:13.5px; font-weight:700; width:100%`. Hover: `--accent-1-strong`. Disabled/loading: `--surface-3` bg, `--ink-3` text.

**Error pill:** `background:#F0DDDD; color:#7A3535; border-radius:8px; padding:8px 12px; font-size:12.5px` — above the CTA.

**Spinner:** CSS-only border circle, `animation:spin 0.7s linear infinite`.

---

## 9. Density & Accessibility

**Compact mode** (`body.density-compact`): `--topbar-h:48px`, `--viewbar-h:38px`; cards tighter, labels hidden. Test both densities for any board/card component.

**Accessibility:** `<button>` for all interactive elements (never `<div onClick>`). `aria-label` on icon-only buttons. Modals/drawers trap focus and close on Escape. Never use colour as the only state indicator.

---

## 10. Mock Data

`MOCK_DATA` in `design_files/data.js` / `components/demo/mockData.ts`.

Members: `u1` Maya Chen · `u2` Jordan Reyes · `u3` Priya Shah · `u4` Theo Nilsson · `u5` Imani Brooks · `u6` Sam Okafor.
Projects: `p1` Web Platform · `p2` Mobile App · `p3` Public API · `p4` Design System.

---

## 11. Checklist

Before submitting any frontend component:

- [ ] CSS tokens only — no raw hex (except the documented semantic exceptions in §2)
- [ ] `Manrope` / `JetBrains Mono` — no other fonts
- [ ] `<Icon name="…">` for all icons; `<Avatar>` / `<Label>` / `<StatusDot>` / `<PriorityGlyph>` for domain primitives
- [ ] Existing CSS classes reused before adding new ones
- [ ] `<button>` for interactive elements; `aria-label` on icon-only buttons
- [ ] Escape closes modals and drawers
- [ ] Tested in regular and compact density
- [ ] No external UI libraries (Radix, shadcn, MUI, Chakra, Lucide, etc.) unless explicitly approved
