"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { Avatar } from "@/components/ui/Avatar";

type MeResponse = {
  name: string;
  email: string;
  userId?: string;
};

interface SidebarProps {
  isOpen?: boolean;
  onClose?: () => void;
}

export function Sidebar({ isOpen, onClose }: SidebarProps) {
  const pathname = usePathname();
  const isProjectsActive = pathname.startsWith("/projects");

  const [user, setUser] = useState<MeResponse | null>(null);

  useEffect(() => {
    fetch("/api/auth/me")
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data) setUser(data);
      })
      .catch(() => {/* ignore */});
  }, []);

  return (
    <aside
      className={`sidebar${isOpen ? " sidebar--open" : ""}`}
      style={{
        width: "var(--sb-w)",
        height: "100vh",
        background: "var(--surface)",
        borderRight: "1px solid var(--border-2)",
        display: "flex",
        flexDirection: "column",
        flexShrink: 0,
        position: "sticky",
        top: 0,
      }}
    >
      {/* Logo / brand */}
      <div
        style={{
          padding: "16px 14px 12px",
          borderBottom: "1px solid var(--border-2)",
          display: "flex",
          alignItems: "center",
          gap: 8,
        }}
      >
        <Link
          href="/projects"
          onClick={onClose}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 8,
            textDecoration: "none",
            transition: "opacity 120ms",
            flex: 1,
          }}
          onMouseEnter={(e) => { e.currentTarget.style.opacity = "0.75"; }}
          onMouseLeave={(e) => { e.currentTarget.style.opacity = "1"; }}
        >
          <div
            style={{
              width: 26,
              height: 26,
              background: "var(--accent-1)",
              borderRadius: 7,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              flexShrink: 0,
            }}
          >
            <span
              style={{
                fontWeight: 800,
                fontSize: 13,
                color: "var(--accent-1-ink)",
                lineHeight: 1,
              }}
            >
              A
            </span>
          </div>
          <span className="sb-ws-name" style={{ color: "var(--ink-0)" }}>
            Aigo
          </span>
        </Link>
        {onClose && (
          <button
            onClick={onClose}
            aria-label="Close menu"
            className="topbar-menu-btn"
            style={{ width: 28, height: 28 }}
          >
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
              <path d="M12 4L4 12M4 4l8 8" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
            </svg>
          </button>
        )}
      </div>

      {/* Nav */}
      <nav style={{ flex: 1, padding: "8px 6px" }}>
        <Link
          href="/projects"
          style={{
            display: "flex",
            alignItems: "center",
            gap: 8,
            padding: "7px 10px",
            borderRadius: "var(--radius-sm)",
            background: isProjectsActive ? "var(--surface-2)" : "transparent",
            color: isProjectsActive ? "var(--ink-0)" : "var(--ink-2)",
            textDecoration: "none",
            fontSize: 13.5,
            fontWeight: isProjectsActive ? 600 : 500,
            transition: "background 0.1s, color 0.1s",
          }}
        >
          <Icon name="folder" size={15} color="currentColor" />
          Projects
        </Link>
      </nav>

      {/* User avatar + name */}
      <div
        style={{
          padding: "10px 10px 12px",
          borderTop: "1px solid var(--border-2)",
          display: "flex",
          alignItems: "center",
          gap: 8,
        }}
      >
        <Avatar id={user?.userId ?? user?.email ?? "unknown"} name={user?.name ?? ""} size={28} />
        <div style={{ overflow: "hidden" }}>
          <div
            style={{
              fontSize: 12.5,
              fontWeight: 600,
              color: "var(--ink-0)",
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
            }}
          >
            {user?.name ?? "—"}
          </div>
          <div
            style={{
              fontSize: 11,
              color: "var(--ink-3)",
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
            }}
          >
            {user?.email ?? ""}
          </div>
        </div>
      </div>
    </aside>
  );
}
