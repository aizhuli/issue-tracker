"use client";
import { useState, useRef, useEffect } from "react";
import { Icon } from "@/components/ui/Icon";
import type { LabelDto, LabelFullDto } from "@/lib/types/issues";

const DEFAULT_COLORS = ["#7B95B8", "#9E7BC1", "#D4A24C", "#6FAE5A", "#B6DF7B", "#A8B0A2"];

const chip = (color: string): React.CSSProperties => ({
  width: 10,
  height: 10,
  borderRadius: "50%",
  background: color,
  display: "inline-block",
  flexShrink: 0,
});

interface LabelPickerProps {
  projectSlug: string;
  projectOwnerId: string;
  meId: string;
  value: LabelDto[];
  onChange: (next: LabelDto[]) => void;
}

type EditKind = "rename" | "delete" | null;

export function LabelPicker({ projectSlug, projectOwnerId, meId, value, onChange }: LabelPickerProps) {
  const [open, setOpen] = useState(false);
  const [labels, setLabels] = useState<LabelFullDto[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);
  const [editState, setEditState] = useState<Map<string, EditKind>>(new Map());
  const [renameValues, setRenameValues] = useState<Map<string, string>>(new Map());
  const [renameErrors, setRenameErrors] = useState<Map<string, string>>(new Map());
  const [creating, setCreating] = useState(false);

  const containerRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const isOwner = meId === projectOwnerId;

  useEffect(() => {
    if (!open) return;
    if (searchRef.current) searchRef.current.focus();
    setLoading(true);
    fetch(`/api/projects/${projectSlug}/labels`)
      .then((res) => (res.ok ? res.json() : Promise.reject()))
      .then((data: LabelFullDto[]) => {
        setLabels(data);
        setLoading(false);
      })
      .catch(() => setLoading(false));
  }, [open, projectSlug]);

  useEffect(() => {
    if (!open) return;
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", handleClick);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClick);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  function handleOpen() {
    setSearch("");
    setEditState(new Map());
    setRenameValues(new Map());
    setRenameErrors(new Map());
    setOpen(true);
  }

  function handleClose() {
    setOpen(false);
    setSearch("");
    setEditState(new Map());
    setRenameValues(new Map());
    setRenameErrors(new Map());
  }

  function toggleLabel(label: LabelDto) {
    const already = value.some((v) => v.id === label.id);
    if (already) {
      onChange(value.filter((v) => v.id !== label.id));
    } else {
      onChange([...value, { id: label.id, name: label.name, color: label.color }]);
    }
  }

  const filtered = labels.filter((l) =>
    l.name.toLowerCase().includes(search.toLowerCase())
  );

  const hasExactMatch = labels.some(
    (l) => l.name.toLowerCase() === search.toLowerCase()
  );

  const showCreate = search.trim().length > 0 && !hasExactMatch;

  async function handleCreate() {
    if (creating) return;
    const name = search.trim();
    const color = DEFAULT_COLORS[labels.length % DEFAULT_COLORS.length];
    setCreating(true);
    try {
      const res = await fetch(`/api/projects/${projectSlug}/labels`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, color }),
      });
      if (!res.ok) throw new Error();
      const created: LabelFullDto = await res.json();
      setLabels((prev) => [...prev, created]);
      onChange([...value, { id: created.id, name: created.name, color: created.color }]);
      setSearch("");
    } catch {
    } finally {
      setCreating(false);
    }
  }

  function setEdit(id: string, kind: EditKind) {
    setEditState((prev) => {
      const next = new Map(prev);
      next.set(id, kind);
      return next;
    });
    if (kind === "rename") {
      const label = labels.find((l) => l.id === id);
      if (label) {
        setRenameValues((prev) => {
          const next = new Map(prev);
          next.set(id, label.name);
          return next;
        });
        setRenameErrors((prev) => {
          const next = new Map(prev);
          next.delete(id);
          return next;
        });
      }
    }
  }

  async function handleRenameConfirm(label: LabelFullDto) {
    const newName = (renameValues.get(label.id) ?? "").trim();
    if (!newName || newName === label.name) {
      setEdit(label.id, null);
      return;
    }
    try {
      const res = await fetch(`/api/projects/${projectSlug}/labels/${label.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: newName, color: label.color }),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        if (body?.errorCode === "projects:label:name:already_exists") {
          setRenameErrors((prev) => {
            const next = new Map(prev);
            next.set(label.id, "Name already exists");
            return next;
          });
          return;
        }
        throw new Error();
      }
      setLabels((prev) =>
        prev.map((l) => (l.id === label.id ? { ...l, name: newName } : l))
      );
      onChange(
        value.map((v) => (v.id === label.id ? { ...v, name: newName } : v))
      );
      setEdit(label.id, null);
    } catch {
      setEdit(label.id, null);
    }
  }

  async function handleDeleteConfirm(label: LabelFullDto) {
    try {
      const res = await fetch(`/api/projects/${projectSlug}/labels/${label.id}`, {
        method: "DELETE",
      });
      if (!res.ok) throw new Error();
      setLabels((prev) => prev.filter((l) => l.id !== label.id));
      onChange(value.filter((v) => v.id !== label.id));
      setEdit(label.id, null);
    } catch {
      setEdit(label.id, null);
    }
  }

  return (
    <div ref={containerRef} style={{ position: "relative", display: "inline-block" }}>
      <button
        type="button"
        onClick={open ? handleClose : handleOpen}
        style={{
          display: "flex",
          alignItems: "center",
          gap: 5,
          flexWrap: "wrap",
          padding: "4px 8px 4px 6px",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-sm)",
          background: "var(--surface)",
          color: value.length > 0 ? "var(--ink-0)" : "var(--ink-3)",
          fontSize: 13,
          fontWeight: 500,
          cursor: "pointer",
          transition: "border-color 80ms ease, background 80ms ease",
          minWidth: 80,
          textAlign: "left",
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
        {value.length === 0 ? (
          <>
            <Icon name="flag" size={13} color="var(--ink-3)" />
            <span>Labels</span>
          </>
        ) : (
          value.map((l) => (
            <span
              key={l.id}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 4,
                padding: "1px 6px",
                borderRadius: 99,
                background: l.color + "30",
                border: `1px solid ${l.color}60`,
                fontSize: 11.5,
                fontWeight: 600,
                color: "var(--ink-0)",
                whiteSpace: "nowrap",
              }}
            >
              <span style={chip(l.color)} />
              {l.name}
            </span>
          ))
        )}
        <Icon name="down" size={12} color="var(--ink-3)" style={{ marginLeft: "auto" }} />
      </button>

      {open && (
        <div
          style={{
            position: "absolute",
            top: "calc(100% + 4px)",
            left: 0,
            zIndex: 50,
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: "var(--radius)",
            boxShadow: "var(--shadow-md)",
            minWidth: 240,
            maxHeight: 300,
            display: "flex",
            flexDirection: "column",
            overflow: "hidden",
          }}
        >
          <div
            style={{
              padding: "8px 8px 6px",
              borderBottom: "1px solid var(--border-2)",
              flexShrink: 0,
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
                ref={searchRef}
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search or create label…"
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

          <div style={{ overflowY: "auto", flex: 1 }}>
            {showCreate && (
              <button
                type="button"
                onClick={handleCreate}
                disabled={creating}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 7,
                  width: "100%",
                  padding: "7px 10px",
                  background: "none",
                  border: "none",
                  borderBottom: "1px solid var(--border-2)",
                  cursor: creating ? "default" : "pointer",
                  textAlign: "left",
                  color: "var(--accent-1-strong)",
                  fontSize: 12.5,
                  fontWeight: 500,
                  opacity: creating ? 0.6 : 1,
                }}
                onMouseEnter={(e) => {
                  if (!creating) (e.currentTarget as HTMLButtonElement).style.background = "var(--surface-2)";
                }}
                onMouseLeave={(e) => {
                  (e.currentTarget as HTMLButtonElement).style.background = "none";
                }}
              >
                <Icon name="plus" size={13} color="var(--accent-1-strong)" />
                <span>
                  Create label{" "}
                  <strong style={{ fontWeight: 700 }}>&ldquo;{search.trim()}&rdquo;</strong>
                </span>
              </button>
            )}

            {!loading && filtered.length === 0 && !showCreate && (
              <div
                style={{
                  padding: "12px",
                  fontSize: 12,
                  color: "var(--ink-3)",
                  textAlign: "center",
                }}
              >
                No labels
              </div>
            )}

            {filtered.map((label) => {
              const kind = editState.get(label.id) ?? null;
              const isSelected = value.some((v) => v.id === label.id);

              if (kind === "rename") {
                const renameVal = renameValues.get(label.id) ?? label.name;
                const renameErr = renameErrors.get(label.id);
                return (
                  <div
                    key={label.id}
                    style={{
                      padding: "6px 10px",
                      borderBottom: "1px solid var(--border-2)",
                    }}
                  >
                    <input
                      autoFocus
                      type="text"
                      value={renameVal}
                      onChange={(e) =>
                        setRenameValues((prev) => {
                          const next = new Map(prev);
                          next.set(label.id, e.target.value);
                          return next;
                        })
                      }
                      onKeyDown={(e) => {
                        if (e.key === "Enter") handleRenameConfirm(label);
                        if (e.key === "Escape") setEdit(label.id, null);
                      }}
                      style={{
                        width: "100%",
                        fontSize: 12.5,
                        color: "var(--ink-0)",
                        background: "var(--surface-2)",
                        border: `1px solid ${renameErr ? "var(--red)" : "var(--border-3)"}`,
                        borderRadius: "var(--radius-sm)",
                        padding: "4px 7px",
                        outline: "none",
                      }}
                    />
                    {renameErr && (
                      <div
                        style={{
                          fontSize: 11,
                          color: "var(--red, #e53e3e)",
                          marginTop: 3,
                          paddingLeft: 2,
                        }}
                      >
                        {renameErr}
                      </div>
                    )}
                    <div
                      style={{
                        display: "flex",
                        gap: 6,
                        marginTop: 5,
                        justifyContent: "flex-end",
                      }}
                    >
                      <button
                        type="button"
                        onClick={() => setEdit(label.id, null)}
                        style={{
                          fontSize: 11.5,
                          padding: "2px 8px",
                          border: "1px solid var(--border)",
                          borderRadius: "var(--radius-sm)",
                          background: "var(--surface)",
                          color: "var(--ink-2)",
                          cursor: "pointer",
                        }}
                      >
                        Cancel
                      </button>
                      <button
                        type="button"
                        onClick={() => handleRenameConfirm(label)}
                        style={{
                          fontSize: 11.5,
                          padding: "2px 8px",
                          border: "1px solid var(--accent-1-strong)",
                          borderRadius: "var(--radius-sm)",
                          background: "var(--accent-1-strong)",
                          color: "#fff",
                          cursor: "pointer",
                        }}
                      >
                        Save
                      </button>
                    </div>
                  </div>
                );
              }

              if (kind === "delete") {
                return (
                  <div
                    key={label.id}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                      padding: "6px 10px",
                      borderBottom: "1px solid var(--border-2)",
                    }}
                  >
                    <span style={{ fontSize: 12, color: "var(--ink-2)", flex: 1 }}>
                      Delete <strong style={{ color: "var(--ink-0)" }}>{label.name}</strong>?
                    </span>
                    <button
                      type="button"
                      onClick={() => setEdit(label.id, null)}
                      style={{
                        fontSize: 11.5,
                        padding: "2px 7px",
                        border: "1px solid var(--border)",
                        borderRadius: "var(--radius-sm)",
                        background: "var(--surface)",
                        color: "var(--ink-2)",
                        cursor: "pointer",
                      }}
                    >
                      No
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDeleteConfirm(label)}
                      style={{
                        fontSize: 11.5,
                        padding: "2px 7px",
                        border: "1px solid var(--red, #e53e3e)",
                        borderRadius: "var(--radius-sm)",
                        background: "var(--red, #e53e3e)",
                        color: "#fff",
                        cursor: "pointer",
                      }}
                    >
                      Yes
                    </button>
                  </div>
                );
              }

              return (
                <LabelRow
                  key={label.id}
                  label={label}
                  isSelected={isSelected}
                  isOwner={isOwner}
                  onToggle={() => toggleLabel(label)}
                  onRename={() => setEdit(label.id, "rename")}
                  onDelete={() => setEdit(label.id, "delete")}
                />
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

interface LabelRowProps {
  label: LabelFullDto;
  isSelected: boolean;
  isOwner: boolean;
  onToggle: () => void;
  onRename: () => void;
  onDelete: () => void;
}

function LabelRow({ label, isSelected, isOwner, onToggle, onRename, onDelete }: LabelRowProps) {
  const [hovered, setHovered] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        borderBottom: "1px solid var(--border-2)",
        background: hovered ? "var(--surface-2)" : "none",
        transition: "background 80ms ease",
      }}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => {
        setHovered(false);
        setMenuOpen(false);
      }}
    >
      <button
        type="button"
        onClick={onToggle}
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          flex: 1,
          padding: "7px 10px",
          background: "none",
          border: "none",
          cursor: "pointer",
          textAlign: "left",
          minWidth: 0,
        }}
      >
        <span style={chip(label.color)} />
        <span
          style={{
            fontSize: 12.5,
            color: "var(--ink-0)",
            fontWeight: isSelected ? 600 : 400,
            flex: 1,
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {label.name}
        </span>
        {isSelected && (
          <span style={{ flexShrink: 0, color: "var(--accent-1-strong)" }}>
            <Icon name="enter" size={12} color="var(--accent-1-strong)" />
          </span>
        )}
      </button>

      {isOwner && hovered && (
        <div style={{ position: "relative", flexShrink: 0, paddingRight: 6 }}>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              setMenuOpen((v) => !v);
            }}
            style={{
              padding: "3px 5px",
              background: "none",
              border: "1px solid var(--border)",
              borderRadius: "var(--radius-sm)",
              cursor: "pointer",
              color: "var(--ink-2)",
              display: "flex",
              alignItems: "center",
            }}
          >
            <Icon name="dots" size={13} color="var(--ink-2)" />
          </button>

          {menuOpen && (
            <div
              style={{
                position: "absolute",
                top: "calc(100% + 2px)",
                right: 0,
                zIndex: 60,
                background: "var(--surface)",
                border: "1px solid var(--border)",
                borderRadius: "var(--radius-sm)",
                boxShadow: "var(--shadow-md)",
                minWidth: 110,
                overflow: "hidden",
              }}
            >
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setMenuOpen(false);
                  onRename();
                }}
                style={{
                  display: "block",
                  width: "100%",
                  padding: "7px 12px",
                  background: "none",
                  border: "none",
                  borderBottom: "1px solid var(--border-2)",
                  textAlign: "left",
                  fontSize: 12.5,
                  color: "var(--ink-0)",
                  cursor: "pointer",
                }}
                onMouseEnter={(e) =>
                  ((e.currentTarget as HTMLButtonElement).style.background = "var(--surface-2)")
                }
                onMouseLeave={(e) =>
                  ((e.currentTarget as HTMLButtonElement).style.background = "none")
                }
              >
                Rename
              </button>
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setMenuOpen(false);
                  onDelete();
                }}
                style={{
                  display: "block",
                  width: "100%",
                  padding: "7px 12px",
                  background: "none",
                  border: "none",
                  textAlign: "left",
                  fontSize: 12.5,
                  color: "var(--red, #e53e3e)",
                  cursor: "pointer",
                }}
                onMouseEnter={(e) =>
                  ((e.currentTarget as HTMLButtonElement).style.background = "var(--surface-2)")
                }
                onMouseLeave={(e) =>
                  ((e.currentTarget as HTMLButtonElement).style.background = "none")
                }
              >
                Delete
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
