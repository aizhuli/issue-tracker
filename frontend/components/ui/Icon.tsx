"use client";

const PATHS: Record<string, React.ReactNode> = {
  inbox:    <><path d="M3 13l3-7h12l3 7"/><path d="M3 13v6h18v-6"/><path d="M8 13a4 4 0 008 0"/></>,
  board:    <><rect x="3" y="4" width="6" height="16" rx="1.5"/><rect x="10" y="4" width="6" height="10" rx="1.5"/><rect x="17" y="4" width="4" height="7" rx="1.5"/></>,
  list:     <><path d="M4 6h16M4 12h16M4 18h12"/></>,
  backlog:  <><circle cx="12" cy="12" r="8" strokeDasharray="2 2.5"/></>,
  sprint:   <><circle cx="12" cy="12" r="8"/><path d="M12 8v4l3 2"/></>,
  folder:   <><path d="M3 6a1 1 0 011-1h5l2 2h9a1 1 0 011 1v10a1 1 0 01-1 1H4a1 1 0 01-1-1V6z"/></>,
  me:       <><circle cx="12" cy="8" r="3.2"/><path d="M5 19a7 7 0 0114 0"/></>,
  settings: <><circle cx="12" cy="12" r="2.8"/><path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8"/></>,
  search:   <><circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/></>,
  plus:     <><path d="M12 5v14M5 12h14"/></>,
  chevron:  <><path d="M9 6l6 6-6 6"/></>,
  down:     <><path d="M6 9l6 6 6-6"/></>,
  filter:   <><path d="M4 6h16M7 12h10M10 18h4"/></>,
  sort:     <><path d="M7 4v16M3 8l4-4 4 4M17 4v16M13 16l4 4 4-4"/></>,
  close:    <><path d="M6 6l12 12M18 6L6 18"/></>,
  link:     <><path d="M10 14a4 4 0 005.66 0l3-3a4 4 0 10-5.66-5.66l-1.5 1.5"/><path d="M14 10a4 4 0 00-5.66 0l-3 3a4 4 0 105.66 5.66l1.5-1.5"/></>,
  paperclip:<><path d="M21 11.5l-9 9a5 5 0 01-7-7l9-9a3.5 3.5 0 015 5l-9 9a2 2 0 01-3-3l8-8"/></>,
  comment:  <><path d="M21 12a8 8 0 01-11.6 7.1L4 21l1.9-5.4A8 8 0 1121 12z"/></>,
  sparkle:  <><path d="M12 3l1.6 4.6L18 9l-4.4 1.4L12 15l-1.6-4.6L6 9l4.4-1.4L12 3z"/><path d="M19 15l.7 2 2 .7-2 .7-.7 2-.7-2-2-.7 2-.7.7-2z"/></>,
  bell:     <><path d="M6 16V11a6 6 0 1112 0v5l1.5 2H4.5L6 16z"/><path d="M10 20a2 2 0 004 0"/></>,
  help:     <><circle cx="12" cy="12" r="9"/><path d="M9.5 9.5a2.5 2.5 0 015 .2c0 1.6-2.5 2-2.5 3.8M12 17v.5"/></>,
  grid:     <><rect x="4" y="4" width="6" height="6" rx="1"/><rect x="14" y="4" width="6" height="6" rx="1"/><rect x="4" y="14" width="6" height="6" rx="1"/><rect x="14" y="14" width="6" height="6" rx="1"/></>,
  archive:  <><rect x="3" y="4" width="18" height="4" rx="1"/><path d="M5 8v11a1 1 0 001 1h12a1 1 0 001-1V8M10 12h4"/></>,
  dots:     <><circle cx="6" cy="12" r="1.4"/><circle cx="12" cy="12" r="1.4"/><circle cx="18" cy="12" r="1.4"/></>,
  enter:    <><path d="M9 10h8a2 2 0 012 2v4M9 10l3-3M9 10l3 3"/></>,
  flag:     <><path d="M5 21V4M5 4h11l-2 4 2 4H5"/></>,
  calendar: <><rect x="4" y="6" width="16" height="14" rx="1.5"/><path d="M4 10h16M9 4v4M15 4v4"/></>,
  branch:   <><circle cx="6" cy="6" r="2"/><circle cx="6" cy="18" r="2"/><circle cx="18" cy="8" r="2"/><path d="M6 8v8M6 14c0-4 12-2 12-6"/></>,
  play:     <><path d="M7 5v14l12-7z"/></>,
  // Eye icons for password toggle
  eye:      <><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></>,
  "eye-off":<><path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94"/><path d="M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19"/><path d="M1 1l22 22"/></>,
};

interface IconProps {
  name: string;
  size?: number;
  color?: string;
  stroke?: number;
}

export function Icon({ name, size = 16, color = "currentColor", stroke = 1.6 }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth={stroke}
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      {PATHS[name]}
    </svg>
  );
}
