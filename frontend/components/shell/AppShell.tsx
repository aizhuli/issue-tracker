"use client";

import { ReactNode, useState } from "react";
import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false);

  return (
    <div
      style={{
        display: "flex",
        height: "100vh",
        overflow: "hidden",
        background: "var(--paper)",
      }}
    >
      {/* Mobile scrim — only visible when sidebar is open on small screens */}
      {sidebarOpen && (
        <div
          className="sidebar-scrim"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      {/* Main column */}
      <div
        style={{
          flex: 1,
          display: "flex",
          flexDirection: "column",
          minWidth: 0,
          overflow: "hidden",
        }}
      >
        <Topbar onMenuClick={() => setSidebarOpen((v) => !v)} />

        {/* Scrollable content area */}
        <main
          style={{
            flex: 1,
            overflow: "auto",
            minHeight: 0,
          }}
        >
          {children}
        </main>
      </div>
    </div>
  );
}
