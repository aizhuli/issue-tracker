"use client";

import { InputHTMLAttributes } from "react";

interface FormFieldProps {
  label: string;
  error?: string;
  inputProps: InputHTMLAttributes<HTMLInputElement>;
}

export function FormField({ label, error, inputProps }: FormFieldProps) {
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
      <input
        {...inputProps}
        style={{
          width: "100%",
          height: 36,
          padding: "0 10px",
          borderRadius: 8,
          border: `1px solid ${error ? "#B94D2F" : "var(--border-3)"}`,
          background: "var(--surface)",
          fontSize: 13.5,
          color: "var(--ink-0)",
          outline: "none",
          transition: "border-color 0.12s",
          boxSizing: "border-box",
          ...inputProps.style,
        }}
      />
      {error && (
        <span style={{ fontSize: 12, color: "#B94D2F", lineHeight: 1.3 }}>
          {error}
        </span>
      )}
    </div>
  );
}
