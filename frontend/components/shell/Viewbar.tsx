"use client";

import { ReactNode } from "react";
import { Icon } from "@/components/ui/Icon";

interface ViewbarProps {
  title: string;
  search?: {
    value: string;
    onChange: (s: string) => void;
  };
  actions?: ReactNode;
}

export function Viewbar({ title, search, actions }: ViewbarProps) {
  return (
    <div
      style={{
        height: "var(--viewbar-h)",
        borderBottom: "1px solid var(--border)",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "0 12px",
        background: "var(--surface)",
        flexShrink: 0,
        gap: 8,
      }}
    >
      {/* Left: title */}
      <span
        style={{
          fontWeight: 700,
          fontSize: 13.5,
          color: "var(--ink-0)",
          letterSpacing: "-0.01em",
          whiteSpace: "nowrap",
        }}
      >
        {title}
      </span>

      {/* Right: search + actions */}
      <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
        {search !== undefined && (
          <label
            style={{
              display: "flex",
              alignItems: "center",
              gap: 5,
              background: "var(--surface-2)",
              border: "1px solid var(--border)",
              borderRadius: "var(--radius-sm)",
              padding: "3px 8px",
              cursor: "text",
            }}
          >
            <Icon name="search" size={13} color="var(--ink-3)" />
            <input
              type="search"
              value={search.value}
              onChange={(e) => search.onChange(e.target.value)}
              placeholder="Search…"
              style={{
                border: "none",
                outline: "none",
                background: "transparent",
                fontSize: 12.5,
                color: "var(--ink-0)",
                width: 140,
              }}
            />
          </label>
        )}
        {actions}
      </div>
    </div>
  );
}
