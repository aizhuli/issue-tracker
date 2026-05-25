import { ReactNode } from "react";
import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  return (
    <div
      style={{
        display: "flex",
        height: "100vh",
        overflow: "hidden",
        background: "var(--paper)",
      }}
    >
      <Sidebar />

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
        <Topbar />

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
