"use client";

import { InputHTMLAttributes, useState } from "react";
import { Icon } from "./Icon";

interface PasswordInputProps {
  label: string;
  error?: string;
  inputProps: Omit<InputHTMLAttributes<HTMLInputElement>, "type">;
}

export function PasswordInput({ label, error, inputProps }: PasswordInputProps) {
  const [show, setShow] = useState(false);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
      <label
        style={{
          fontSize: 12,
          fontWeight: 600,
          color: "var(--ink-1)",
          letterSpacing: "0.005em",
        }}
      >
        {label}
      </label>
      <div style={{ position: "relative" }}>
        <input
          {...inputProps}
          type={show ? "text" : "password"}
          style={{
            width: "100%",
            height: 36,
            padding: "0 38px 0 10px",
            borderRadius: 8,
            border: `1px solid ${error ? "#B94D2F" : "var(--border-3)"}`,
            background: "var(--surface)",
            fontSize: 13.5,
            color: "var(--ink-0)",
            outline: "none",
            transition: "border-color 0.12s",
            boxSizing: "border-box",
          }}
        />
        <button
          type="button"
          onClick={() => setShow((s) => !s)}
          aria-label={show ? "Hide password" : "Show password"}
          style={{
            position: "absolute",
            right: 8,
            top: "50%",
            transform: "translateY(-50%)",
            color: "var(--ink-3)",
            display: "flex",
            alignItems: "center",
            padding: 2,
          }}
        >
          <Icon name={show ? "eye-off" : "eye"} size={16} stroke={1.8} />
        </button>
      </div>
      {error && (
        <span style={{ fontSize: 12, color: "#B94D2F", lineHeight: 1.3 }}>
          {error}
        </span>
      )}
    </div>
  );
}
