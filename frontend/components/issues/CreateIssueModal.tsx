"use client";

import { useState, useEffect, useRef, KeyboardEvent } from "react";
import { Modal } from "@/components/ui/Modal";
import { mapProblemDetailsToFields } from "@/lib/errors";
import type { IssueFull } from "@/lib/types/issues";

interface CreateIssueModalProps {
  open: boolean;
  projectSlug: string;
  onClose: () => void;
  onCreated: (issue: IssueFull) => void;
}

export function CreateIssueModal({
  open,
  projectSlug,
  onClose,
  onCreated,
}: CreateIssueModalProps) {
  const titleId = "create-issue-modal-title";
  const inputRef = useRef<HTMLInputElement>(null);

  const [title, setTitle] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setTitle("");
      setError(null);
      setSubmitting(false);
    }
  }, [open]);

  async function handleSubmit() {
    if (submitting || title.trim().length === 0) return;

    setError(null);
    setSubmitting(true);

    try {
      const res = await fetch(`/api/projects/${projectSlug}/issues`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ title: title.trim() }),
      });

      if (res.ok) {
        const issue: IssueFull = await res.json();
        onCreated(issue);
        onClose();
        return;
      }

      const body = await res.json().catch(() => ({}));
      const fieldErrors = mapProblemDetailsToFields(body);
      setError(fieldErrors.title ?? fieldErrors._form ?? "Something went wrong");
    } catch {
      setError("Something went wrong — please try again");
    } finally {
      setSubmitting(false);
    }
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter") {
      e.preventDefault();
      handleSubmit();
    }
  }

  const isDisabled = submitting || title.trim().length === 0;

  return (
    <Modal open={open} onClose={onClose} labelledBy={titleId}>
      <div
        style={{
          padding: "24px 24px 20px",
          display: "flex",
          flexDirection: "column",
          gap: 16,
          boxSizing: "border-box",
        }}
      >
        <h2
          id={titleId}
          style={{
            margin: 0,
            fontSize: 17,
            fontWeight: 700,
            fontFamily: "'Manrope', sans-serif",
            color: "var(--ink-0)",
            lineHeight: 1.2,
          }}
        >
          New issue
        </h2>

        <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
          <input
            ref={inputRef}
            id="issue-title"
            type="text"
            value={title}
            onChange={(e) => {
              setTitle(e.target.value);
              if (error) setError(null);
            }}
            onKeyDown={handleKeyDown}
            autoComplete="off"
            placeholder="Issue title"
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
              fontFamily: "inherit",
            }}
          />
          {error && (
            <span
              style={{
                fontSize: 12,
                color: "#B94D2F",
                lineHeight: 1.3,
              }}
            >
              {error}
            </span>
          )}
        </div>

        <button
          type="button"
          aria-label="Create issue"
          onClick={handleSubmit}
          disabled={isDisabled}
          style={{
            width: "100%",
            height: 40,
            background: isDisabled ? "var(--surface-3)" : "var(--accent-1)",
            color: isDisabled ? "var(--ink-3)" : "var(--accent-1-ink)",
            border: "none",
            borderRadius: 9,
            fontSize: 13.5,
            fontWeight: 600,
            cursor: isDisabled ? "not-allowed" : "pointer",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            transition: "background 0.12s",
          }}
        >
          {submitting ? (
            <span
              aria-hidden="true"
              style={{
                display: "inline-block",
                width: 16,
                height: 16,
                border: "2px solid var(--border-3)",
                borderTopColor: "var(--ink-2)",
                borderRadius: "50%",
                animation: "spin 0.7s linear infinite",
                flexShrink: 0,
              }}
            />
          ) : (
            "Create"
          )}
        </button>
      </div>
    </Modal>
  );
}
