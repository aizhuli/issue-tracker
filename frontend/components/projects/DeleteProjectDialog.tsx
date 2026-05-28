"use client";

import { useEffect, useState } from "react";
import { Modal } from "@/components/ui/Modal";

type ProjectSummary = {
  id: string;
  slug: string;
  name: string;
  ownerId: string;
  ownerName: string;
  createdAt: string;
};

interface DeleteProjectDialogProps {
  open: boolean;
  project: ProjectSummary;
  onClose: () => void;
  onDeleted: () => void;
}

export function DeleteProjectDialog({
  open,
  project,
  onClose,
  onDeleted,
}: DeleteProjectDialogProps) {
  const [confirmName, setConfirmName] = useState("");
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset state when dialog opens
  useEffect(() => {
    if (open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setConfirmName("");
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setError(null);
    }
  }, [open]);

  const canDelete = confirmName === project.name && !deleting;

  const handleDelete = async () => {
    setDeleting(true);
    setError(null);

    try {
      const response = await fetch(`/api/projects/${project.slug}`, {
        method: "DELETE",
      });

      if (response.ok) {
        onDeleted();
        onClose();
      } else {
        // Parse ProblemDetails error response
        try {
          const data = await response.json();
          setError(data.detail || "Failed to delete project");
        } catch {
          setError("Failed to delete project");
        }
      }
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to delete project"
      );
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} labelledBy="delete-project-title">
      <div className="ai-pop" style={{ padding: "24px" }}>
        {/* Title */}
        <h2
          id="delete-project-title"
          style={{
            fontSize: 18,
            fontWeight: 700,
            color: "var(--ink-0)",
            margin: "0 0 12px 0",
            letterSpacing: "-0.005em",
          }}
        >
          Delete project
        </h2>

        {/* Body text */}
        <p
          style={{
            fontSize: 13.5,
            color: "var(--ink-1)",
            lineHeight: 1.45,
            margin: "0 0 20px 0",
          }}
        >
          Delete <strong>{project.name}</strong>? This permanently removes all
          its issues, labels, and comments.
        </p>

        {/* Confirmation input label */}
        <label
          style={{
            display: "block",
            fontSize: 12,
            fontWeight: 600,
            color: "var(--ink-1)",
            letterSpacing: "0.005em",
            marginBottom: 5,
          }}
        >
          Type the project name to confirm
        </label>

        {/* Input field */}
        <input
          type="text"
          value={confirmName}
          onChange={(e) => setConfirmName(e.target.value)}
          placeholder={project.name}
          style={{
            width: "100%",
            height: 36,
            padding: "0 10px",
            borderRadius: 8,
            border: "1px solid var(--border-3)",
            background: "var(--surface)",
            fontSize: 13.5,
            color: "var(--ink-0)",
            outline: "none",
            transition: "border-color 0.12s",
            boxSizing: "border-box",
            marginBottom: error ? 12 : 20,
          }}
        />

        {/* Error message */}
        {error && (
          <div
            role="alert"
            style={{
              background: "#F0DDDD",
              color: "#7A3535",
              borderRadius: 8,
              padding: "8px 12px",
              fontSize: 12.5,
              lineHeight: 1.4,
              marginBottom: 20,
            }}
          >
            {error}
          </div>
        )}

        {/* Button group */}
        <div
          style={{
            display: "flex",
            gap: 8,
            justifyContent: "flex-end",
          }}
        >
          <button
            onClick={onClose}
            disabled={deleting}
            style={{
              background: "var(--surface-2)",
              color: "var(--ink-1)",
              border: "none",
              borderRadius: "var(--radius-sm)",
              padding: "8px 16px",
              fontSize: 13,
              fontWeight: 600,
              cursor: deleting ? "not-allowed" : "pointer",
              opacity: deleting ? 0.5 : 1,
              transition: "background 0.15s",
            }}
            onMouseEnter={(e) => {
              if (!deleting) {
                e.currentTarget.style.background = "var(--border-2)";
              }
            }}
            onMouseLeave={(e) => {
              if (!deleting) {
                e.currentTarget.style.background = "var(--surface-2)";
              }
            }}
          >
            Cancel
          </button>

          <button
            onClick={handleDelete}
            disabled={!canDelete}
            style={{
              background: canDelete ? "#E05252" : "#C0C0C0",
              color: "#fff",
              border: "none",
              borderRadius: "var(--radius-sm)",
              padding: "8px 16px",
              fontSize: 13,
              fontWeight: 600,
              cursor: canDelete ? "pointer" : "not-allowed",
              transition: "background 0.15s",
            }}
            onMouseEnter={(e) => {
              if (canDelete) {
                e.currentTarget.style.background = "#C9393C";
              }
            }}
            onMouseLeave={(e) => {
              if (canDelete) {
                e.currentTarget.style.background = "#E05252";
              }
            }}
          >
            {deleting ? "Deleting..." : "Delete"}
          </button>
        </div>
      </div>
    </Modal>
  );
}
