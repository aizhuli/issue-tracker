// Shared UI components.
const { useState, useMemo, useEffect, useRef, useCallback } = React;

const MEMBERS_BY_ID = Object.fromEntries(window.MOCK_DATA.members.map(m => [m.id, m]));

function Avatar({ id, size = 22 }) {
  const m = MEMBERS_BY_ID[id];
  if (!m) return null;
  const bg = `oklch(0.78 0.07 ${m.hue})`;
  const fg = `oklch(0.28 0.08 ${m.hue})`;
  return (
    <div
      title={m.name}
      style={{
        width: size, height: size, borderRadius: '50%',
        background: bg, color: fg,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        fontSize: size * 0.42, fontWeight: 600, letterSpacing: '0.02em',
        flexShrink: 0, fontFamily: 'Manrope, sans-serif',
        boxShadow: 'inset 0 0 0 1.5px rgba(255,255,255,0.6)',
      }}
    >
      {m.initials}
    </div>
  );
}

function AvatarStack({ ids, size = 20, max = 4 }) {
  const shown = ids.slice(0, max);
  const extra = ids.length - shown.length;
  return (
    <div style={{ display: 'flex', alignItems: 'center' }}>
      {shown.map((id, i) => (
        <div key={id} style={{ marginLeft: i ? -size * 0.32 : 0, position: 'relative', zIndex: shown.length - i }}>
          <Avatar id={id} size={size} />
        </div>
      ))}
      {extra > 0 && (
        <div style={{
          marginLeft: -size * 0.32, width: size, height: size, borderRadius: '50%',
          background: 'var(--surface-2)', color: 'var(--ink-2)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          fontSize: size * 0.4, fontWeight: 600,
          boxShadow: 'inset 0 0 0 1.5px rgba(255,255,255,0.7)',
        }}>+{extra}</div>
      )}
    </div>
  );
}

function PriorityGlyph({ level, size = 14 }) {
  const p = window.PRIORITIES[level];
  if (!p) return null;
  // Bar-chart style glyph
  const heights = level === 'urgent' ? [1, 1, 1] : level === 'high' ? [0.5, 0.8, 1] : level === 'medium' ? [0.5, 0.8, 0.4] : [0.5, 0.3, 0.3];
  const active = level === 'urgent' ? [1,1,1] : level === 'high' ? [1,1,1] : level === 'medium' ? [1,1,0] : [1,0,0];
  return (
    <span title={p.label + ' priority'} style={{ display: 'inline-flex', alignItems: 'flex-end', gap: 1.5, height: size, width: size }}>
      {heights.map((h, i) => (
        <span key={i} style={{
          width: 3, height: h * size,
          borderRadius: 1,
          background: active[i] ? p.color : 'var(--border-2)',
        }} />
      ))}
    </span>
  );
}

function StatusDot({ id, size = 9 }) {
  const s = window.STATUSES.find(x => x.id === id);
  if (!s) return null;
  if (id === 'done') {
    return (
      <span style={{
        width: size, height: size, borderRadius: '50%',
        background: s.dot,
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        color: '#fff', fontSize: size * 0.7, fontWeight: 700,
        flexShrink: 0,
      }}>✓</span>
    );
  }
  // Pie-progress dots
  const pct = id === 'backlog' ? 0 : id === 'todo' ? 0 : id === 'in_progress' ? 0.4 : id === 'in_review' ? 0.75 : 1;
  return (
    <span style={{
      width: size, height: size, borderRadius: '50%',
      background: `conic-gradient(${s.dot} ${pct * 360}deg, transparent 0)`,
      boxShadow: `inset 0 0 0 1.4px ${s.dot}`,
      flexShrink: 0,
      display: 'inline-block',
    }} />
  );
}

function Label({ text }) {
  // Color hash
  const palette = [
    ['#E4EFD6', '#3D5424'], // sage
    ['#F2E4CB', '#7A571E'], // amber
    ['#DDE6F0', '#3A5573'], // blue
    ['#ECDDF0', '#5D3373'], // purple
    ['#F0DDDD', '#7A3535'], // red
    ['#DDF0EA', '#1F5C50'], // teal
  ];
  let h = 0;
  for (let i = 0; i < text.length; i++) h = (h * 31 + text.charCodeAt(i)) >>> 0;
  const [bg, fg] = palette[h % palette.length];
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: '2px 7px', borderRadius: 999,
      background: bg, color: fg,
      fontSize: 10.5, fontWeight: 600, letterSpacing: '0.01em',
      fontFamily: 'Manrope, sans-serif',
    }}>
      <span style={{ width: 5, height: 5, borderRadius: '50%', background: fg, opacity: 0.6 }} />
      {text}
    </span>
  );
}

function Icon({ name, size = 16, color = 'currentColor', stroke = 1.6 }) {
  const paths = {
    inbox: <><path d="M3 13l3-7h12l3 7"/><path d="M3 13v6h18v-6"/><path d="M8 13a4 4 0 008 0"/></>,
    board: <><rect x="3" y="4" width="6" height="16" rx="1.5"/><rect x="10" y="4" width="6" height="10" rx="1.5"/><rect x="17" y="4" width="4" height="7" rx="1.5"/></>,
    list:  <><path d="M4 6h16M4 12h16M4 18h12"/></>,
    backlog: <><circle cx="12" cy="12" r="8" strokeDasharray="2 2.5"/></>,
    sprint: <><circle cx="12" cy="12" r="8"/><path d="M12 8v4l3 2"/></>,
    folder:<><path d="M3 6a1 1 0 011-1h5l2 2h9a1 1 0 011 1v10a1 1 0 01-1 1H4a1 1 0 01-1-1V6z"/></>,
    me:    <><circle cx="12" cy="8" r="3.2"/><path d="M5 19a7 7 0 0114 0"/></>,
    settings: <><circle cx="12" cy="12" r="2.8"/><path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8"/></>,
    search: <><circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/></>,
    plus:  <><path d="M12 5v14M5 12h14"/></>,
    chevron: <><path d="M9 6l6 6-6 6"/></>,
    down: <><path d="M6 9l6 6 6-6"/></>,
    filter:<><path d="M4 6h16M7 12h10M10 18h4"/></>,
    sort:  <><path d="M7 4v16M3 8l4-4 4 4M17 4v16M13 16l4 4 4-4"/></>,
    close: <><path d="M6 6l12 12M18 6L6 18"/></>,
    link:  <><path d="M10 14a4 4 0 005.66 0l3-3a4 4 0 10-5.66-5.66l-1.5 1.5"/><path d="M14 10a4 4 0 00-5.66 0l-3 3a4 4 0 105.66 5.66l1.5-1.5"/></>,
    paperclip: <><path d="M21 11.5l-9 9a5 5 0 01-7-7l9-9a3.5 3.5 0 015 5l-9 9a2 2 0 01-3-3l8-8"/></>,
    comment: <><path d="M21 12a8 8 0 01-11.6 7.1L4 21l1.9-5.4A8 8 0 1121 12z"/></>,
    sparkle: <><path d="M12 3l1.6 4.6L18 9l-4.4 1.4L12 15l-1.6-4.6L6 9l4.4-1.4L12 3z"/><path d="M19 15l.7 2 2 .7-2 .7-.7 2-.7-2-2-.7 2-.7.7-2z"/></>,
    bell:  <><path d="M6 16V11a6 6 0 1112 0v5l1.5 2H4.5L6 16z"/><path d="M10 20a2 2 0 004 0"/></>,
    help:  <><circle cx="12" cy="12" r="9"/><path d="M9.5 9.5a2.5 2.5 0 015 .2c0 1.6-2.5 2-2.5 3.8M12 17v.5"/></>,
    grid:  <><rect x="4" y="4" width="6" height="6" rx="1"/><rect x="14" y="4" width="6" height="6" rx="1"/><rect x="4" y="14" width="6" height="6" rx="1"/><rect x="14" y="14" width="6" height="6" rx="1"/></>,
    archive: <><rect x="3" y="4" width="18" height="4" rx="1"/><path d="M5 8v11a1 1 0 001 1h12a1 1 0 001-1V8M10 12h4"/></>,
    dots:  <><circle cx="6" cy="12" r="1.4"/><circle cx="12" cy="12" r="1.4"/><circle cx="18" cy="12" r="1.4"/></>,
    enter: <><path d="M9 10h8a2 2 0 012 2v4M9 10l3-3M9 10l3 3"/></>,
    flag:  <><path d="M5 21V4M5 4h11l-2 4 2 4H5"/></>,
    calendar: <><rect x="4" y="6" width="16" height="14" rx="1.5"/><path d="M4 10h16M9 4v4M15 4v4"/></>,
    branch: <><circle cx="6" cy="6" r="2"/><circle cx="6" cy="18" r="2"/><circle cx="18" cy="8" r="2"/><path d="M6 8v8M6 14c0-4 12-2 12-6"/></>,
    play:  <><path d="M7 5v14l12-7z"/></>,
  };
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={stroke} strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      {paths[name]}
    </svg>
  );
}

window.Avatar = Avatar;
window.AvatarStack = AvatarStack;
window.PriorityGlyph = PriorityGlyph;
window.StatusDot = StatusDot;
window.Label = Label;
window.Icon = Icon;
window.MEMBERS_BY_ID = MEMBERS_BY_ID;
