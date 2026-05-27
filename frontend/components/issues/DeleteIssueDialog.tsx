"use client";

import { useState, useEffect } from "react";
import { Modal } from "@/components/ui/Modal";
import type { IssueFull } from "@/lib/types/issues";

interface DeleteIssueDialogProps {
  open: boolean;
  issue: IssueFull;
  projectSlug: string;
  onClose: () => void;
  onDeleted: () => void;
}

export function DeleteIssueDialog({ open, issue, projectSlug, onClose, onDeleted }: DeleteIssueDialogProps) {
  const [confirmText, setConfirmText] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setConfirmText("");
      setError(null);
    }
  }, [open]);

  async function handleDelete() {
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/api/projects/${projectSlug}/issues/${issue.number}`, {
        method: "DELETE",
      });
      if (res.ok) {
        onDeleted();
        onClose();
      } else {
        let detail = "An unexpected error occurred.";
        try {
          const body = await res.json();
          if (body?.detail) detail = body.detail;
        } catch {
          // ignore parse failures
        }
        setError(detail);
      }
    } catch {
      setError("Failed to connect to the server.");
    } finally {
      setSubmitting(false);
    }
  }

  const canDelete = confirmText === issue.displayKey && !submitting;

  return (
    <Modal open={open} onClose={onClose} labelledBy="delete-issue-title">
      <div
        style={{
          padding: "24px",
          display: "flex",
          flexDirection: "column",
          gap: "16px",
        }}
      >
        <h2
          id="delete-issue-title"
          style={{
            margin: 0,
            fontSize: "16px",
            fontWeight: 600,
            color: "var(--text-primary, #e0e0ef)",
          }}
        >
          Delete{" "}
          <em style={{ fontStyle: "normal", fontWeight: 700 }}>{issue.displayKey}</em>?
        </h2>

        <p
          style={{
            margin: 0,
            fontSize: "14px",
            color: "var(--text-secondary, #a0a0b8)",
            lineHeight: "1.5",
          }}
        >
          This permanently removes its comments and label links.
        </p>

        <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
          <label
            htmlFor="delete-confirm-input"
            style={{
              fontSize: "13px",
              color: "var(--text-secondary, #a0a0b8)",
            }}
          >
            Type{" "}
            <strong style={{ color: "var(--text-primary, #e0e0ef)", fontFamily: "monospace" }}>
              {issue.displayKey}
            </strong>{" "}
            to confirm
          </label>
          <input
            id="delete-confirm-input"
            type="text"
            value={confirmText}
            onChange={(e) => setConfirmText(e.target.value)}
            autoComplete="off"
            spellCheck={false}
            style={{
              padding: "8px 10px",
              fontSize: "14px",
              background: "var(--surface-2, #1e1e2e)",
              border: "1px solid var(--border, #2e2e3e)",
              borderRadius: "6px",
              color: "var(--text-primary, #e0e0ef)",
              outline: "none",
              fontFamily: "monospace",
            }}
          />
        </div>

        {error && (
          <p
            role="alert"
            style={{
              margin: 0,
              fontSize: "13px",
              color: "#f87171",
            }}
          >
            {error}
          </p>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "4px" }}>
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            style={{
              padding: "8px 16px",
              fontSize: "14px",
              fontWeight: 500,
              background: "transparent",
              border: "1px solid var(--border, #2e2e3e)",
              borderRadius: "6px",
              color: "var(--text-primary, #e0e0ef)",
              cursor: submitting ? "not-allowed" : "pointer",
              opacity: submitting ? 0.6 : 1,
            }}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleDelete}
            disabled={!canDelete}
            aria-disabled={!canDelete}
            style={{
              padding: "8px 16px",
              fontSize: "14px",
              fontWeight: 600,
              background: canDelete ? "#dc2626" : "#7f1d1d",
              border: "none",
              borderRadius: "6px",
              color: "#fff",
              cursor: canDelete ? "pointer" : "not-allowed",
              opacity: canDelete ? 1 : 0.5,
              transition: "background 120ms ease",
            }}
          >
            {submitting ? "Deleting…" : "Delete"}
          </button>
        </div>
      </div>
    </Modal>
  );
}
