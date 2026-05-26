"use client";
import { useState, useRef, useEffect, useCallback } from "react";
import { Avatar } from "@/components/ui/Avatar";
import { Icon } from "@/components/ui/Icon";
import type { UserSummary } from "@/lib/types/issues";

interface AssigneePickerProps {
  value: UserSummary | null;
  onChange: (next: UserSummary | null) => void;
}

export function AssigneePicker({ value, onChange }: AssigneePickerProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UserSummary[]>([]);
  const [loading, setLoading] = useState(false);

  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    if (open && inputRef.current) {
      inputRef.current.focus();
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;

    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        setOpen(false);
      }
    }

    document.addEventListener("mousedown", handleClick);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClick);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;

    const timer = setTimeout(() => {
      if (abortRef.current) {
        abortRef.current.abort();
      }
      const controller = new AbortController();
      abortRef.current = controller;

      setLoading(true);
      fetch(`/api/users/search?q=${encodeURIComponent(query)}&maxPageSize=10`, {
        signal: controller.signal,
      })
        .then((res) => {
          if (!res.ok) throw new Error("search failed");
          return res.json();
        })
        .then((data) => {
          setResults(data.items ?? data ?? []);
          setLoading(false);
        })
        .catch((err) => {
          if (err.name !== "AbortError") {
            setResults([]);
            setLoading(false);
          }
        });
    }, 200);

    return () => {
      clearTimeout(timer);
    };
  }, [query, open]);

  function handleClose() {
    setOpen(false);
    setQuery("");
    setResults([]);
  }

  function handleSelect(user: UserSummary | null) {
    onChange(user);
    handleClose();
  }

  function handleOpen() {
    setOpen(true);
    setQuery("");
    setResults([]);
  }

  return (
    <div ref={containerRef} style={{ position: "relative", display: "inline-block" }}>
      <button
        type="button"
        onClick={open ? handleClose : handleOpen}
        style={{
          display: "flex",
          alignItems: "center",
          gap: 6,
          padding: "4px 8px 4px 6px",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-sm)",
          background: "var(--surface)",
          color: value ? "var(--ink-0)" : "var(--ink-3)",
          fontSize: 13,
          fontWeight: 500,
          cursor: "pointer",
          transition: "border-color 80ms ease, background 80ms ease",
        }}
        onMouseEnter={(e) => {
          (e.currentTarget as HTMLButtonElement).style.borderColor = "var(--border-3)";
          (e.currentTarget as HTMLButtonElement).style.background = "var(--surface-2)";
        }}
        onMouseLeave={(e) => {
          (e.currentTarget as HTMLButtonElement).style.borderColor = "var(--border)";
          (e.currentTarget as HTMLButtonElement).style.background = "var(--surface)";
        }}
      >
        {value ? (
          <Avatar id={value.id} name={value.name} size={20} />
        ) : (
          <span
            style={{
              width: 20,
              height: 20,
              borderRadius: "50%",
              background: "var(--surface-3)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              flexShrink: 0,
            }}
          >
            <Icon name="me" size={12} color="var(--ink-3)" />
          </span>
        )}
        <span style={{ maxWidth: 120, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
          {value ? value.name : "Unassigned"}
        </span>
        <Icon name="down" size={12} color="var(--ink-3)" />
      </button>

      {open && (
        <div
          style={{
            position: "absolute",
            top: "calc(100% + 4px)",
            left: 0,
            zIndex: 200,
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: "var(--radius)",
            boxShadow: "var(--shadow-md)",
            width: 260,
            display: "flex",
            flexDirection: "column",
            overflow: "hidden",
          }}
        >
          <div
            style={{
              padding: "8px 8px 6px",
              borderBottom: "1px solid var(--border-2)",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 6,
                background: "var(--surface-2)",
                border: "1px solid var(--border)",
                borderRadius: "var(--radius-sm)",
                padding: "5px 8px",
              }}
            >
              <Icon name="search" size={13} color="var(--ink-3)" />
              <input
                ref={inputRef}
                type="text"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search members…"
                style={{
                  flex: 1,
                  background: "none",
                  border: "none",
                  outline: "none",
                  fontSize: 12.5,
                  color: "var(--ink-0)",
                  lineHeight: 1.4,
                }}
              />
              {loading && (
                <span
                  style={{
                    width: 12,
                    height: 12,
                    border: "2px solid var(--border-3)",
                    borderTopColor: "var(--accent-1-strong)",
                    borderRadius: "50%",
                    animation: "spin 0.6s linear infinite",
                    flexShrink: 0,
                  }}
                />
              )}
            </div>
          </div>

          <div style={{ overflowY: "auto", maxHeight: 220 }}>
            <button
              type="button"
              onClick={() => handleSelect(null)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                width: "100%",
                padding: "7px 10px",
                background: value === null ? "var(--surface-2)" : "none",
                border: "none",
                borderBottom: "1px solid var(--border-2)",
                cursor: "pointer",
                textAlign: "left",
                color: "var(--ink-2)",
                fontSize: 12.5,
                fontWeight: 500,
              }}
              onMouseEnter={(e) => {
                if (value !== null) (e.currentTarget as HTMLButtonElement).style.background = "var(--surface-2)";
              }}
              onMouseLeave={(e) => {
                if (value !== null) (e.currentTarget as HTMLButtonElement).style.background = "none";
              }}
            >
              <span
                style={{
                  width: 24,
                  height: 24,
                  borderRadius: "50%",
                  background: "var(--surface-3)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  flexShrink: 0,
                }}
              >
                <Icon name="me" size={13} color="var(--ink-3)" />
              </span>
              <span>Unassigned</span>
              {value === null && (
                <span style={{ marginLeft: "auto", color: "var(--accent-1-strong)" }}>
                  <Icon name="enter" size={12} color="var(--accent-1-strong)" />
                </span>
              )}
            </button>

            {results.length === 0 && !loading && query.length > 0 && (
              <div
                style={{
                  padding: "10px 12px",
                  fontSize: 12,
                  color: "var(--ink-3)",
                  textAlign: "center",
                }}
              >
                No members found
              </div>
            )}

            {results.length === 0 && !loading && query.length === 0 && (
              <div
                style={{
                  padding: "10px 12px",
                  fontSize: 12,
                  color: "var(--ink-3)",
                  textAlign: "center",
                }}
              >
                Type to search members
              </div>
            )}

            {results.map((user) => (
              <button
                key={user.id}
                type="button"
                onClick={() => handleSelect(user)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  width: "100%",
                  padding: "7px 10px",
                  background: value?.id === user.id ? "var(--surface-2)" : "none",
                  border: "none",
                  borderBottom: "1px solid var(--border-2)",
                  cursor: "pointer",
                  textAlign: "left",
                }}
                onMouseEnter={(e) => {
                  if (value?.id !== user.id) (e.currentTarget as HTMLButtonElement).style.background = "var(--surface-2)";
                }}
                onMouseLeave={(e) => {
                  if (value?.id !== user.id) (e.currentTarget as HTMLButtonElement).style.background = "none";
                }}
              >
                <Avatar id={user.id} name={user.name} size={24} />
                <div style={{ flex: 1, minWidth: 0, display: "flex", flexDirection: "column", gap: 1 }}>
                  <span
                    style={{
                      fontSize: 12.5,
                      fontWeight: 600,
                      color: "var(--ink-0)",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {user.name}
                  </span>
                  <span
                    style={{
                      fontSize: 11,
                      color: "var(--ink-3)",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {user.email}
                  </span>
                </div>
                {value?.id === user.id && (
                  <span style={{ marginLeft: "auto", flexShrink: 0 }}>
                    <Icon name="enter" size={12} color="var(--accent-1-strong)" />
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
