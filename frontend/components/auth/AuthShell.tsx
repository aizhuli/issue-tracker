"use client";

import { DemoBoard } from "./DemoBoard";
import { AuthPanel } from "./AuthPanel";

const LogoSvg = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
    <path d="M4 14L9 4l5 10-5 10z" fill="var(--accent-1)" />
    <path
      d="M11 4L16 14l-5 10"
      stroke="var(--ink-0)"
      strokeWidth="2.2"
      strokeLinejoin="round"
      fill="none"
    />
    <circle cx="18" cy="6" r="2.2" fill="var(--ink-0)" />
  </svg>
);

interface AuthShellProps {
  activeTab: "login" | "register";
}

export function AuthShell({ activeTab }: AuthShellProps) {
  return (
    <div
      style={{
        width: "100vw",
        height: "100vh",
        position: "relative",
        overflow: "hidden",
        background: "var(--paper)",
      }}
    >
      {/* Full-screen blurred demo board */}
      <DemoBoard />

      {/* Tinted overlay so the frosted panel pops */}
      <div
        aria-hidden
        style={{
          position: "absolute",
          inset: 0,
          background: "rgba(18, 34, 26, 0.18)",
          zIndex: 0,
        }}
      />

      {/* Centered auth panel */}
      <div
        style={{
          position: "absolute",
          top: "50%",
          left: "50%",
          transform: "translate(-50%, -50%)",
          width: "min(520px, 50vw)",
          minWidth: 340,
          maxHeight: "90vh",
          overflowY: "auto",
          background: "rgba(242, 245, 238, 0.88)",
          backdropFilter: "blur(12px) saturate(1.1)",
          WebkitBackdropFilter: "blur(12px) saturate(1.1)",
          border: "1px solid rgba(220, 226, 210, 0.7)",
          borderRadius: "var(--radius-lg)",
          boxShadow: "var(--shadow-lg)",
          zIndex: 1,
        }}
      >
        <AuthPanel activeTab={activeTab} />
      </div>

      {/* Brand pill — top-left */}
      <div
        style={{
          position: "absolute",
          top: 16,
          left: 18,
          display: "flex",
          alignItems: "center",
          gap: 7,
          padding: "5px 10px 5px 7px",
          background: "rgba(255,255,255,0.85)",
          backdropFilter: "blur(8px)",
          WebkitBackdropFilter: "blur(8px)",
          border: "1px solid var(--border)",
          borderRadius: 999,
          boxShadow: "var(--shadow-sm)",
          pointerEvents: "none",
          zIndex: 2,
        }}
      >
        <div
          style={{
            padding: 2,
            background: "var(--ink-0)",
            borderRadius: 6,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <LogoSvg />
        </div>
        <span className="sb-ws-name" style={{ fontSize: 12.5 }}>
          Aigo - Issue Board
        </span>
      </div>
    </div>
  );
}
