"use client";

import { useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { Avatar } from "@/components/ui/Avatar";
import { AssigneePicker } from "@/components/issues/AssigneePicker";
import { LabelPicker } from "@/components/issues/LabelPicker";
import { CommentsSection } from "@/components/issues/CommentsSection";
import { DeleteIssueDialog } from "@/components/issues/DeleteIssueDialog";
import { mapProblemDetailsToFields } from "@/lib/errors";
import type { IssueFull, IssueStatus, IssuePriority, UserSummary, LabelDto, TriageSuggestion } from "@/lib/types/issues";

interface IssueDetailProps {
  issue: IssueFull;
  me: { id: string; name: string; email: string };
  projectOwnerId: string;
  projectSlug: string;
  onChange: (next: IssueFull) => void;
  onDeleted: () => void;
  onClose?: () => void;
  openInPageUrl?: string;
}

const STATUS_LABELS: Record<IssueStatus, string> = {
  backlog: "Backlog",
  todo: "Todo",
  "in-progress": "In Progress",
  "in-review": "In Review",
  done: "Done",
};

const STATUS_OPTIONS: IssueStatus[] = ["backlog", "todo", "in-progress", "in-review", "done"];

const PRIORITY_LABELS: Record<IssuePriority, string> = {
  low: "Low",
  medium: "Medium",
  high: "High",
  urgent: "Urgent",
};

const PRIORITY_OPTIONS: IssuePriority[] = ["low", "medium", "high", "urgent"];

const PRIORITY_GLYPHS: Record<IssuePriority, string> = {
  low: "▽",
  medium: "◈",
  high: "△",
  urgent: "⬆",
};

function hexLuminance(hex: string): number {
  const clean = hex.replace("#", "");
  const r = parseInt(clean.substring(0, 2), 16) / 255;
  const g = parseInt(clean.substring(2, 4), 16) / 255;
  const b = parseInt(clean.substring(4, 6), 16) / 255;
  const toLinear = (c: number) => (c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
  return 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b);
}

function labelTextColor(hex: string): string {
  return hexLuminance(hex) > 0.179 ? "#000000" : "#ffffff";
}

export function IssueDetail({
  issue,
  me,
  projectOwnerId,
  projectSlug,
  onChange,
  onDeleted,
  onClose,
  openInPageUrl,
}: IssueDetailProps) {
  const [mode, setMode] = useState<"read" | "edit">("read");
  const [deleteOpen, setDeleteOpen] = useState(false);

  const [editTitle, setEditTitle] = useState(issue.title);
  const [editDescription, setEditDescription] = useState(issue.description ?? "");
  const [editAcceptanceCriteria, setEditAcceptanceCriteria] = useState(issue.acceptanceCriteria ?? "");
  const [editStatus, setEditStatus] = useState<IssueStatus>(issue.status);
  const [editPriority, setEditPriority] = useState<IssuePriority>(issue.priority);
  const [editAssignee, setEditAssignee] = useState<UserSummary | null>(issue.assignee);
  const [editLabels, setEditLabels] = useState<LabelDto[]>(issue.labels);

  const [saving, setSaving] = useState(false);
  const [aiSuggesting, setAiSuggesting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const canDelete = me.id === issue.reporter.id || me.id === projectOwnerId;

  function enterEdit() {
    setEditTitle(issue.title);
    setEditDescription(issue.description ?? "");
    setEditAcceptanceCriteria(issue.acceptanceCriteria ?? "");
    setEditStatus(issue.status);
    setEditPriority(issue.priority);
    setEditAssignee(issue.assignee);
    setEditLabels(issue.labels);
    setFieldErrors({});
    setMode("edit");
  }

  function cancelEdit() {
    setFieldErrors({});
    setMode("read");
  }

  async function handleSave() {
    if (saving) return;
    if (!editTitle.trim()) {
      setFieldErrors({ title: "Title is required" });
      return;
    }
    setSaving(true);
    setFieldErrors({});
    try {
      const res = await fetch(`/api/projects/${projectSlug}/issues/${issue.number}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          title: editTitle.trim(),
          description: editDescription || null,
          status: editStatus,
          priority: editPriority,
          assigneeId: editAssignee?.id ?? null,
          labelIds: editLabels.map((l) => l.id),
          acceptanceCriteria: editAcceptanceCriteria || null,
        }),
      });

      if (res.ok) {
        const updated: IssueFull = await res.json();
        onChange(updated);
        setMode("read");
        return;
      }

      const body = await res.json().catch(() => ({}));
      setFieldErrors(mapProblemDetailsToFields(body));
    } catch {
      setFieldErrors({ _form: "Failed to connect to the server." });
    } finally {
      setSaving(false);
    }
  }

  async function handleAiSuggest() {
    if (aiSuggesting || !editTitle.trim()) return;
    setAiSuggesting(true);
    setFieldErrors({});
    try {
      const res = await fetch(`/api/projects/${projectSlug}/issues/${issue.number}/ai/triage`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          title: editTitle.trim(),
          description: editDescription || null,
        }),
      });

      if (res.ok) {
        const s: TriageSuggestion = await res.json();
        setEditPriority(s.priority as IssuePriority);
        setEditLabels((prev) => {
          const existingIds = new Set(prev.map((l) => l.id));
          const merged = [...prev];
          for (const l of s.labels) {
            if (!existingIds.has(l.id)) merged.push(l);
          }
          return merged;
        });
        setEditAcceptanceCriteria(s.acceptanceCriteria);
        return;
      }

      const body = await res.json().catch(() => ({}));
      setFieldErrors(mapProblemDetailsToFields(body));
    } catch {
      setFieldErrors({ _form: "Failed to connect to the server." });
    } finally {
      setAiSuggesting(false);
    }
  }

  if (mode === "edit") {
    return (
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          gap: 0,
          height: "100%",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "16px 20px 14px",
            borderBottom: "1px solid var(--border-2)",
            flexShrink: 0,
          }}
        >
          <span
            style={{
              fontFamily: "var(--font-jetbrains-mono), monospace",
              fontSize: 11,
              fontWeight: 500,
              color: "var(--ink-3)",
              letterSpacing: "0.04em",
            }}
          >
            {issue.displayKey}
          </span>
          <span style={{ fontSize: 13, fontWeight: 600, color: "var(--ink-1)" }}>Edit issue</span>
        </div>

        <div
          style={{
            flex: 1,
            overflowY: "auto",
            padding: "20px 20px 0",
            display: "flex",
            flexDirection: "column",
            gap: 16,
          }}
        >
          {fieldErrors._form && (
            <div
              role="alert"
              style={{
                padding: "10px 12px",
                background: "color-mix(in srgb, #B94D2F 12%, transparent)",
                border: "1px solid color-mix(in srgb, #B94D2F 30%, transparent)",
                borderRadius: "var(--radius-sm)",
                fontSize: 13,
                color: "#B94D2F",
              }}
            >
              {fieldErrors._form}
            </div>
          )}

          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <label
              htmlFor="edit-title"
              style={{ fontSize: 12, fontWeight: 600, color: "var(--ink-2)" }}
            >
              Title <span style={{ color: "#B94D2F" }}>*</span>
            </label>
            <input
              id="edit-title"
              type="text"
              value={editTitle}
              onChange={(e) => {
                setEditTitle(e.target.value);
                if (fieldErrors.title) setFieldErrors((prev) => ({ ...prev, title: "" }));
              }}
              style={{
                width: "100%",
                padding: "8px 10px",
                border: `1px solid ${fieldErrors.title ? "#B94D2F" : "var(--border-3)"}`,
                borderRadius: "var(--radius-sm)",
                background: "var(--surface)",
                fontSize: 13.5,
                color: "var(--ink-0)",
                outline: "none",
              }}
            />
            {fieldErrors.title && (
              <span style={{ fontSize: 12, color: "#B94D2F" }}>{fieldErrors.title}</span>
            )}
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <label
              htmlFor="edit-description"
              style={{ fontSize: 12, fontWeight: 600, color: "var(--ink-2)" }}
            >
              Description
            </label>
            <textarea
              id="edit-description"
              value={editDescription}
              onChange={(e) => setEditDescription(e.target.value)}
              rows={5}
              style={{
                width: "100%",
                padding: "8px 10px",
                border: `1px solid ${fieldErrors.description ? "#B94D2F" : "var(--border-3)"}`,
                borderRadius: "var(--radius-sm)",
                background: "var(--surface)",
                fontSize: 13.5,
                color: "var(--ink-0)",
                outline: "none",
                resize: "vertical",
                fontFamily: "inherit",
              }}
              placeholder="Describe the issue… (Markdown supported)"
            />
            {fieldErrors.description && (
              <span style={{ fontSize: 12, color: "#B94D2F" }}>{fieldErrors.description}</span>
            )}
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <label
              htmlFor="edit-acceptance-criteria"
              style={{ fontSize: 12, fontWeight: 600, color: "var(--ink-2)" }}
            >
              Acceptance criteria
            </label>
            <textarea
              id="edit-acceptance-criteria"
              value={editAcceptanceCriteria}
              onChange={(e) => setEditAcceptanceCriteria(e.target.value)}
              rows={4}
              style={{
                width: "100%",
                padding: "8px 10px",
                border: `1px solid ${fieldErrors.acceptanceCriteria ? "#B94D2F" : "var(--border-3)"}`,
                borderRadius: "var(--radius-sm)",
                background: "var(--surface)",
                fontSize: 13.5,
                color: "var(--ink-0)",
                outline: "none",
                resize: "vertical",
                fontFamily: "inherit",
              }}
              placeholder="Define done… (Markdown supported)"
            />
            {fieldErrors.acceptanceCriteria && (
              <span style={{ fontSize: 12, color: "#B94D2F" }}>{fieldErrors.acceptanceCriteria}</span>
            )}
            <button
              type="button"
              onClick={handleAiSuggest}
              disabled={aiSuggesting || !editTitle.trim()}
              style={{
                alignSelf: "flex-start",
                padding: "6px 12px",
                fontSize: 12.5,
                fontWeight: 600,
                background: aiSuggesting || !editTitle.trim() ? "var(--surface-3)" : "var(--surface-2)",
                border: "1px solid var(--border)",
                borderRadius: "var(--radius-sm)",
                color: aiSuggesting || !editTitle.trim() ? "var(--ink-3)" : "var(--ink-1)",
                cursor: aiSuggesting || !editTitle.trim() ? "not-allowed" : "pointer",
                display: "inline-flex",
                alignItems: "center",
                gap: 5,
                transition: "background 80ms ease",
              }}
            >
              {aiSuggesting ? (
                <>
                  <span
                    aria-hidden="true"
                    style={{
                      display: "inline-block",
                      width: 11,
                      height: 11,
                      border: "2px solid var(--border-3)",
                      borderTopColor: "var(--ink-2)",
                      borderRadius: "50%",
                      animation: "spin 0.7s linear infinite",
                    }}
                  />
                  Suggesting…
                </>
              ) : (
                "✨ AI-suggest"
              )}
            </button>
          </div>

          <div style={{ display: "flex", gap: 16, flexWrap: "wrap" }}>
            <div style={{ display: "flex", flexDirection: "column", gap: 5, flex: "1 1 140px" }}>
              <label
                htmlFor="edit-status"
                style={{ fontSize: 12, fontWeight: 600, color: "var(--ink-2)" }}
              >
                Status
              </label>
              <select
                id="edit-status"
                value={editStatus}
                onChange={(e) => setEditStatus(e.target.value as IssueStatus)}
                style={{
                  padding: "7px 10px",
                  border: "1px solid var(--border-3)",
                  borderRadius: "var(--radius-sm)",
                  background: "var(--surface)",
                  fontSize: 13,
                  color: "var(--ink-0)",
                  outline: "none",
                  cursor: "pointer",
                }}
              >
                {STATUS_OPTIONS.map((s) => (
                  <option key={s} value={s}>
                    {STATUS_LABELS[s]}
                  </option>
                ))}
              </select>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 5, flex: "1 1 140px" }}>
              <label
                htmlFor="edit-priority"
                style={{ fontSize: 12, fontWeight: 600, color: "var(--ink-2)" }}
              >
                Priority
              </label>
              <select
                id="edit-priority"
                value={editPriority}
                onChange={(e) => setEditPriority(e.target.value as IssuePriority)}
                style={{
                  padding: "7px 10px",
                  border: "1px solid var(--border-3)",
                  borderRadius: "var(--radius-sm)",
                  background: "var(--surface)",
                  fontSize: 13,
                  color: "var(--ink-0)",
                  outline: "none",
                  cursor: "pointer",
                }}
              >
                {PRIORITY_OPTIONS.map((p) => (
                  <option key={p} value={p}>
                    {PRIORITY_LABELS[p]}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <span style={{ fontSize: 12, fontWeight: 600, color: "var(--ink-2)" }}>Assignee</span>
            <AssigneePicker value={editAssignee} onChange={setEditAssignee} />
            {fieldErrors.assigneeId && (
              <span style={{ fontSize: 12, color: "#B94D2F" }}>{fieldErrors.assigneeId}</span>
            )}
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <span style={{ fontSize: 12, fontWeight: 600, color: "var(--ink-2)" }}>Labels</span>
            <LabelPicker
              projectSlug={projectSlug}
              projectOwnerId={projectOwnerId}
              meId={me.id}
              value={editLabels}
              onChange={setEditLabels}
            />
            {fieldErrors.labelIds && (
              <span style={{ fontSize: 12, color: "#B94D2F" }}>{fieldErrors.labelIds}</span>
            )}
          </div>
        </div>

        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            gap: 8,
            padding: "14px 20px 16px",
            borderTop: "1px solid var(--border-2)",
            flexShrink: 0,
          }}
        >
          <button
            type="button"
            onClick={cancelEdit}
            disabled={saving}
            style={{
              padding: "8px 16px",
              fontSize: 13.5,
              fontWeight: 500,
              background: "transparent",
              border: "1px solid var(--border-3)",
              borderRadius: "var(--radius-sm)",
              color: "var(--ink-1)",
              cursor: saving ? "not-allowed" : "pointer",
              opacity: saving ? 0.6 : 1,
            }}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={saving || !editTitle.trim()}
            style={{
              padding: "8px 18px",
              fontSize: 13.5,
              fontWeight: 600,
              background:
                saving || !editTitle.trim() ? "var(--surface-3)" : "var(--accent-1)",
              color:
                saving || !editTitle.trim() ? "var(--ink-3)" : "var(--accent-1-ink)",
              border: "none",
              borderRadius: "var(--radius-sm)",
              cursor: saving || !editTitle.trim() ? "not-allowed" : "pointer",
              transition: "background 120ms ease",
              display: "flex",
              alignItems: "center",
              gap: 6,
            }}
          >
            {saving && (
              <span
                aria-hidden="true"
                style={{
                  display: "inline-block",
                  width: 13,
                  height: 13,
                  border: "2px solid var(--border-3)",
                  borderTopColor: "var(--ink-2)",
                  borderRadius: "50%",
                  animation: "spin 0.7s linear infinite",
                }}
              />
            )}
            {saving ? "Saving…" : "Save"}
          </button>
        </div>
      </div>
    );
  }

  return (
    <>
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          height: "100%",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "14px 20px 12px",
            borderBottom: "1px solid var(--border-2)",
            flexShrink: 0,
            gap: 10,
          }}
        >
          <span
            style={{
              fontFamily: "var(--font-jetbrains-mono), monospace",
              fontSize: 11,
              fontWeight: 500,
              color: "var(--ink-3)",
              letterSpacing: "0.04em",
            }}
          >
            {issue.displayKey}
          </span>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginLeft: "auto" }}>
            {openInPageUrl && (
              <a
                href={openInPageUrl}
                style={{
                  fontSize: 12.5,
                  fontWeight: 500,
                  color: "var(--ink-2)",
                  textDecoration: "none",
                  padding: "4px 8px",
                  border: "1px solid var(--border)",
                  borderRadius: "var(--radius-sm)",
                  background: "var(--surface)",
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 4,
                  transition: "border-color 80ms ease",
                }}
              >
                Open ↗
              </a>
            )}
            {onClose && (
              <button
                type="button"
                onClick={onClose}
                aria-label="Close"
                style={{
                  width: 28,
                  height: 28,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  border: "1px solid var(--border)",
                  borderRadius: "var(--radius-sm)",
                  background: "var(--surface)",
                  color: "var(--ink-2)",
                  fontSize: 16,
                  cursor: "pointer",
                }}
              >
                ×
              </button>
            )}
          </div>
        </div>

        <div
          style={{
            flex: 1,
            overflowY: "auto",
            display: "flex",
            flexDirection: "row",
            gap: 0,
          }}
        >
          <div
            style={{
              flex: 1,
              minWidth: 0,
              padding: "20px 20px 24px",
              display: "flex",
              flexDirection: "column",
              gap: 20,
              overflowY: "auto",
            }}
          >
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              <h1
                style={{
                  margin: 0,
                  fontSize: 18,
                  fontWeight: 700,
                  color: "var(--ink-0)",
                  lineHeight: 1.25,
                  letterSpacing: "-0.01em",
                }}
              >
                {issue.title}
              </h1>

              <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                <span className={`status-pill--${issue.status}`}>{STATUS_LABELS[issue.status]}</span>

                <span
                  className={`priority-icon priority-icon--${issue.priority}`}
                  style={{ display: "inline-flex", alignItems: "center", gap: 4, fontSize: 12 }}
                >
                  <span aria-hidden="true">{PRIORITY_GLYPHS[issue.priority]}</span>
                  <span style={{ fontSize: 11, fontWeight: 600 }}>{PRIORITY_LABELS[issue.priority]}</span>
                </span>

                {issue.assignee ? (
                  <span style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 12, color: "var(--ink-2)" }}>
                    <Avatar id={issue.assignee.id} name={issue.assignee.name} size={18} />
                    <span style={{ fontWeight: 500 }}>{issue.assignee.name}</span>
                  </span>
                ) : (
                  <span style={{ fontSize: 12, color: "var(--ink-3)", fontWeight: 500 }}>Unassigned</span>
                )}
              </div>
            </div>

            {issue.description ? (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                <span
                  style={{
                    fontSize: 11,
                    fontWeight: 700,
                    color: "var(--ink-3)",
                    textTransform: "uppercase",
                    letterSpacing: "0.06em",
                  }}
                >
                  Description
                </span>
                <div className="markdown-body">
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>
                    {issue.description}
                  </ReactMarkdown>
                </div>
              </div>
            ) : (
              <p style={{ margin: 0, fontSize: 13, color: "var(--ink-3)", fontStyle: "italic" }}>
                No description provided.
              </p>
            )}

            {issue.acceptanceCriteria && (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                <span
                  style={{
                    fontSize: 11,
                    fontWeight: 700,
                    color: "var(--ink-3)",
                    textTransform: "uppercase",
                    letterSpacing: "0.06em",
                  }}
                >
                  Acceptance criteria
                </span>
                <div className="markdown-body">
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>
                    {issue.acceptanceCriteria}
                  </ReactMarkdown>
                </div>
              </div>
            )}

            <div style={{ borderTop: "1px solid var(--border-2)", paddingTop: 20 }}>
              <CommentsSection
                projectSlug={projectSlug}
                issueNumber={issue.number}
                me={me}
              />
            </div>
          </div>

          <div
            style={{
              width: 220,
              flexShrink: 0,
              borderLeft: "1px solid var(--border-2)",
              padding: "16px 14px 20px",
              display: "flex",
              flexDirection: "column",
              gap: 20,
              overflowY: "auto",
            }}
          >
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              <button
                type="button"
                onClick={enterEdit}
                style={{
                  width: "100%",
                  padding: "7px 12px",
                  fontSize: 13,
                  fontWeight: 600,
                  background: "var(--surface-2)",
                  border: "1px solid var(--border)",
                  borderRadius: "var(--radius-sm)",
                  color: "var(--ink-1)",
                  cursor: "pointer",
                  textAlign: "center",
                  transition: "background 80ms ease, border-color 80ms ease",
                }}
                onMouseEnter={(e) => {
                  (e.currentTarget as HTMLButtonElement).style.background = "var(--surface-3)";
                  (e.currentTarget as HTMLButtonElement).style.borderColor = "var(--border-3)";
                }}
                onMouseLeave={(e) => {
                  (e.currentTarget as HTMLButtonElement).style.background = "var(--surface-2)";
                  (e.currentTarget as HTMLButtonElement).style.borderColor = "var(--border)";
                }}
              >
                Edit
              </button>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
              <span
                style={{
                  fontSize: 10.5,
                  fontWeight: 700,
                  color: "var(--ink-3)",
                  textTransform: "uppercase",
                  letterSpacing: "0.06em",
                }}
              >
                Reporter
              </span>
              <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 2 }}>
                <Avatar id={issue.reporter.id} name={issue.reporter.name} size={22} />
                <div style={{ minWidth: 0 }}>
                  <div
                    style={{
                      fontSize: 12.5,
                      fontWeight: 600,
                      color: "var(--ink-0)",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {issue.reporter.name}
                  </div>
                  <div
                    style={{
                      fontSize: 11,
                      color: "var(--ink-3)",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {issue.reporter.email}
                  </div>
                </div>
              </div>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                <span
                  style={{
                    fontSize: 10.5,
                    fontWeight: 700,
                    color: "var(--ink-3)",
                    textTransform: "uppercase",
                    letterSpacing: "0.06em",
                  }}
                >
                  Created
                </span>
                <span style={{ fontSize: 12, color: "var(--ink-1)" }}>
                  {new Date(issue.createdAt).toLocaleString()}
                </span>
              </div>

              <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                <span
                  style={{
                    fontSize: 10.5,
                    fontWeight: 700,
                    color: "var(--ink-3)",
                    textTransform: "uppercase",
                    letterSpacing: "0.06em",
                  }}
                >
                  Updated
                </span>
                <span style={{ fontSize: 12, color: "var(--ink-1)" }}>
                  {new Date(issue.updatedAt).toLocaleString()}
                </span>
              </div>

              {issue.closedAt && (
                <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                  <span
                    style={{
                      fontSize: 10.5,
                      fontWeight: 700,
                      color: "var(--ink-3)",
                      textTransform: "uppercase",
                      letterSpacing: "0.06em",
                    }}
                  >
                    Closed
                  </span>
                  <span style={{ fontSize: 12, color: "var(--ink-1)" }}>
                    {new Date(issue.closedAt).toLocaleString()}
                  </span>
                </div>
              )}
            </div>

            {issue.labels.length > 0 && (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                <span
                  style={{
                    fontSize: 10.5,
                    fontWeight: 700,
                    color: "var(--ink-3)",
                    textTransform: "uppercase",
                    letterSpacing: "0.06em",
                  }}
                >
                  Labels
                </span>
                <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
                  {issue.labels.map((label) => (
                    <span
                      key={label.id}
                      className="label-chip"
                      style={{
                        background: label.color,
                        color: labelTextColor(label.color),
                      }}
                    >
                      {label.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {canDelete && (
              <div style={{ marginTop: "auto", paddingTop: 12 }}>
                <button
                  type="button"
                  onClick={() => setDeleteOpen(true)}
                  style={{
                    width: "100%",
                    padding: "7px 12px",
                    fontSize: 13,
                    fontWeight: 600,
                    background: "transparent",
                    border: "1px solid color-mix(in srgb, #dc2626 40%, transparent)",
                    borderRadius: "var(--radius-sm)",
                    color: "#dc2626",
                    cursor: "pointer",
                    textAlign: "center",
                    transition: "background 80ms ease",
                  }}
                  onMouseEnter={(e) => {
                    (e.currentTarget as HTMLButtonElement).style.background =
                      "color-mix(in srgb, #dc2626 8%, transparent)";
                  }}
                  onMouseLeave={(e) => {
                    (e.currentTarget as HTMLButtonElement).style.background = "transparent";
                  }}
                >
                  Delete
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      <DeleteIssueDialog
        open={deleteOpen}
        issue={issue}
        projectSlug={projectSlug}
        onClose={() => setDeleteOpen(false)}
        onDeleted={onDeleted}
      />
    </>
  );
}
