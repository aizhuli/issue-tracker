"use client";

import { useRouter } from "next/navigation";

interface TopbarProps {
  onMenuClick?: () => void;
}

export function Topbar({ onMenuClick }: TopbarProps) {
  const router = useRouter();

  async function handleLogout() {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push("/login");
  }

  return (
    <header
      style={{
        height: "var(--topbar-h)",
        borderBottom: "1px solid var(--border-2)",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "0 16px",
        background: "var(--surface)",
        flexShrink: 0,
        gap: 8,
      }}
    >
      {/* Hamburger — visible only on tablet/mobile via CSS */}
      <button
        onClick={onMenuClick}
        aria-label="Open menu"
        className="topbar-menu-btn"
      >
        <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
          <path d="M2 4.5h14M2 9h14M2 13.5h14" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"/>
        </svg>
      </button>

      <button
        onClick={handleLogout}
        style={{
          fontSize: 12.5,
          fontWeight: 600,
          color: "var(--ink-2)",
          padding: "5px 10px",
          borderRadius: "var(--radius-sm)",
          border: "1px solid var(--border)",
          background: "transparent",
          cursor: "pointer",
          transition: "background 0.1s, color 0.1s",
        }}
        onMouseEnter={(e) => {
          e.currentTarget.style.background = "var(--surface-2)";
          e.currentTarget.style.color = "var(--ink-0)";
        }}
        onMouseLeave={(e) => {
          e.currentTarget.style.background = "transparent";
          e.currentTarget.style.color = "var(--ink-2)";
        }}
      >
        Log out
      </button>
    </header>
  );
}
