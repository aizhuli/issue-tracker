"use client";

import { Suspense } from "react";
import { useRouter } from "next/navigation";
import { LoginForm } from "./LoginForm";
import { RegisterForm } from "./RegisterForm";

const LogoSvg = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
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

interface AuthPanelProps {
  activeTab: "login" | "register";
}

export function AuthPanel({ activeTab }: AuthPanelProps) {
  const router = useRouter();

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        padding: "28px 32px 24px",
      }}
    >
      {/* Header */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 9,
          marginBottom: 28,
          flexShrink: 0,
        }}
      >
        <div
          style={{
            padding: 3,
            background: "var(--ink-0)",
            borderRadius: 8,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <LogoSvg />
        </div>
        <span className="sb-ws-name">Aigo - Issue Board</span>
      </div>

      {/* Tab strip */}
      <div className="dr-tabs" style={{ flexShrink: 0 }}>
        <button
          type="button"
          className={`dr-tab${activeTab === "login" ? " active" : ""}`}
          onClick={() => router.push("/login")}
        >
          Log in
        </button>
        <button
          type="button"
          className={`dr-tab${activeTab === "register" ? " active" : ""}`}
          onClick={() => router.push("/register")}
        >
          Sign up
        </button>
      </div>

      {/* Form area — fixed height prevents panel resize when switching tabs */}
      <div style={{ minHeight: 300 }}>
        <Suspense fallback={null}>
          {activeTab === "login" ? <LoginForm /> : <RegisterForm />}
        </Suspense>
      </div>

      {/* Footer */}
      <div
        style={{
          flexShrink: 0,
          paddingTop: 16,
          color: "var(--ink-3)",
          fontSize: 11,
          textAlign: "center",
        }}
      >
        © 2025 Aigo
      </div>
    </div>
  );
}
