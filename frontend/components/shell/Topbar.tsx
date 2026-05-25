"use client";

import { useRouter } from "next/navigation";

export function Topbar() {
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
        justifyContent: "flex-end",
        padding: "0 16px",
        background: "var(--surface)",
        flexShrink: 0,
      }}
    >
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
