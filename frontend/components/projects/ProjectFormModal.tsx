"use client";

import { useState, useRef, useEffect, FormEvent } from "react";
import { Modal } from "@/components/ui/Modal";
import { Icon } from "@/components/ui/Icon";
import { FormError } from "@/components/ui/FormError";
import { slugSchema } from "@/lib/schemas/projects";
import { mapProblemDetailsToFields } from "@/lib/errors";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type ProjectSummary = {
  id: string;
  slug: string;
  name: string;
  ownerId: string;
  ownerName: string;
  createdAt: string;
};

type ProjectFull = ProjectSummary & {
  description?: string;
  updatedAt: string;
};

interface ProjectFormModalProps {
  open: boolean;
  mode: "create" | "edit";
  /** Provided in edit mode */
  project?: ProjectFull;
  onClose: () => void;
  onSaved: (project: ProjectFull) => void;
}

// ---------------------------------------------------------------------------
// Slug availability status
// ---------------------------------------------------------------------------

type SlugStatus =
  | "idle"
  | "checking"
  | "available"
  | "taken"
  | "invalid_format";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function slugify(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .substring(0, 50);
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function Spinner({ size = 14 }: { size?: number }) {
  return (
    <span
      aria-hidden="true"
      style={{
        display: "inline-block",
        width: size,
        height: size,
        border: "2px solid var(--border-3)",
        borderTopColor: "var(--ink-2)",
        borderRadius: "50%",
        animation: "spin 0.7s linear infinite",
        flexShrink: 0,
      }}
    />
  );
}

function SlugStatusIndicator({ status }: { status: SlugStatus }) {
  if (status === "idle") return null;

  if (status === "checking") {
    return (
      <span
        style={{
          display: "flex",
          alignItems: "center",
          gap: 5,
          fontSize: 12,
          color: "var(--ink-3)",
        }}
      >
        <Spinner size={12} />
        Checking…
      </span>
    );
  }

  if (status === "available") {
    return (
      <span
        style={{
          display: "flex",
          alignItems: "center",
          gap: 4,
          fontSize: 12,
          color: "#2D7A2D",
          fontWeight: 500,
        }}
      >
        <Icon name="enter" size={13} color="#2D7A2D" />
        Available
      </span>
    );
  }

  if (status === "taken") {
    return (
      <span style={{ fontSize: 12, color: "#B94D2F", fontWeight: 500 }}>
        Already taken
      </span>
    );
  }

  // invalid_format
  return (
    <span style={{ fontSize: 12, color: "#B94D2F", lineHeight: 1.3 }}>
      Invalid format — use lowercase letters, digits, and hyphens (3–50 chars,
      no leading/trailing hyphen)
    </span>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function ProjectFormModal({
  open,
  mode,
  project,
  onClose,
  onSaved,
}: ProjectFormModalProps) {
  const titleId = "project-form-modal-title";
  const isCreate = mode === "create";

  // ------------------------------------------------------------------
  // Form state
  // ------------------------------------------------------------------

  const [name, setName] = useState(project?.name ?? "");
  const [slug, setSlug] = useState(project?.slug ?? "");
  const [description, setDescription] = useState(project?.description ?? "");

  const [errors, setErrors] = useState<Record<string, string>>({});
  const [slugStatus, setSlugStatus] = useState<SlugStatus>("idle");
  const [submitting, setSubmitting] = useState(false);

  // Track whether the user has manually edited the slug field
  const slugTouched = useRef(false);
  // AbortController for cancelling in-flight slug availability checks
  const abortRef = useRef<AbortController | null>(null);

  // ------------------------------------------------------------------
  // Reset form when modal opens / mode changes
  // ------------------------------------------------------------------

  useEffect(() => {
    if (open) {
      setName(project?.name ?? "");
      setSlug(project?.slug ?? "");
      setDescription(project?.description ?? "");
      setErrors({});
      setSlugStatus("idle");
      setSubmitting(false);
      slugTouched.current = false;
    }
  }, [open, project]);

  // ------------------------------------------------------------------
  // Slug availability check (create mode only)
  // ------------------------------------------------------------------

  useEffect(() => {
    if (!isCreate) return;

    // Abort any previous in-flight request immediately
    abortRef.current?.abort();

    // Local format check first
    const parseResult = slugSchema.safeParse(slug);
    if (!parseResult.success) {
      if (slug.length === 0) {
        setSlugStatus("idle");
      } else {
        setSlugStatus("invalid_format");
      }
      return;
    }

    // Valid format — debounce the backend call
    setSlugStatus("checking");

    const controller = new AbortController();
    abortRef.current = controller;

    const timerId = setTimeout(async () => {
      try {
        const res = await fetch(
          `/api/projects/slug-availability?slug=${encodeURIComponent(slug)}`,
          { signal: controller.signal }
        );
        const data = await res.json();
        if (data.available === true) {
          setSlugStatus("available");
        } else {
          setSlugStatus(
            data.reason === "taken" ? "taken" : "invalid_format"
          );
        }
      } catch (err) {
        // Ignore aborted requests
        if (err instanceof Error && err.name === "AbortError") return;
        // Network / parse failure — fall back to idle so the user can try submitting
        setSlugStatus("idle");
      }
    }, 400);

    return () => {
      clearTimeout(timerId);
      controller.abort();
    };
  }, [slug, isCreate]);

  // ------------------------------------------------------------------
  // Event handlers
  // ------------------------------------------------------------------

  function handleNameChange(value: string) {
    setName(value);
    if (isCreate && !slugTouched.current) {
      setSlug(slugify(value));
    }
    // Clear name error on change
    if (errors.name) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next.name;
        return next;
      });
    }
  }

  function handleSlugChange(value: string) {
    slugTouched.current = true;
    setSlug(value);
    // Clear slug field error on change
    if (errors.slug) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next.slug;
        return next;
      });
    }
  }

  function handleDescriptionChange(value: string) {
    setDescription(value);
    if (errors.description) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next.description;
        return next;
      });
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setErrors({});

    setSubmitting(true);
    try {
      let res: Response;

      if (isCreate) {
        res = await fetch("/api/projects", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            name,
            slug,
            ...(description ? { description } : {}),
          }),
        });
      } else {
        res = await fetch(`/api/projects/${project!.slug}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            name,
            ...(description ? { description } : {}),
          }),
        });
      }

      if (res.ok) {
        const saved: ProjectFull = await res.json();
        onSaved(saved);
        onClose();
        return;
      }

      // Handle 4xx errors
      const body = await res.json().catch(() => ({}));
      const errorCode: string = body?.errorCode ?? "";

      if (errorCode === "projects:project:slug:already_exists") {
        setSlugStatus("taken");
      } else {
        const fieldErrors = mapProblemDetailsToFields(body);
        setErrors(fieldErrors);
      }
    } catch {
      setErrors({ _form: "Something went wrong — please try again" });
    } finally {
      setSubmitting(false);
    }
  }

  // ------------------------------------------------------------------
  // Submit button disabled logic
  // ------------------------------------------------------------------

  const isSubmitDisabled = isCreate
    ? submitting || name.trim().length === 0 || slugStatus !== "available"
    : submitting || name.trim().length === 0;

  // ------------------------------------------------------------------
  // Render
  // ------------------------------------------------------------------

  return (
    <Modal open={open} onClose={onClose} labelledBy={titleId}>
      <div
        className="ai-modal-card"
        style={{
          background: "var(--surface)",
          borderRadius: "var(--radius)",
          boxShadow: "var(--shadow-lg, 0 8px 32px rgba(0,0,0,0.18))",
          padding: "28px 28px 24px",
          width: "100%",
          maxWidth: 480,
          boxSizing: "border-box",
        }}
      >
        {/* Title */}
        <h2
          id={titleId}
          style={{
            margin: "0 0 20px",
            fontSize: 17,
            fontWeight: 700,
            fontFamily: "'Manrope', sans-serif",
            color: "var(--ink-0)",
            lineHeight: 1.2,
          }}
        >
          {isCreate ? "New Project" : "Edit Project"}
        </h2>

        <form
          onSubmit={handleSubmit}
          style={{ display: "flex", flexDirection: "column", gap: 16 }}
        >
          {/* ---- Name field ---- */}
          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <label
              htmlFor="project-name"
              style={{
                fontSize: 12,
                fontWeight: 600,
                color: "var(--ink-1)",
                letterSpacing: "0.005em",
              }}
            >
              Name
            </label>
            <input
              id="project-name"
              type="text"
              value={name}
              onChange={(e) => handleNameChange(e.target.value)}
              autoComplete="off"
              placeholder="My Awesome Project"
              style={{
                width: "100%",
                height: 36,
                padding: "0 10px",
                borderRadius: 8,
                border: `1px solid ${errors.name ? "#B94D2F" : "var(--border-3)"}`,
                background: "var(--surface)",
                fontSize: 13.5,
                color: "var(--ink-0)",
                outline: "none",
                transition: "border-color 0.12s",
                boxSizing: "border-box",
              }}
            />
            {errors.name && (
              <span style={{ fontSize: 12, color: "#B94D2F", lineHeight: 1.3 }}>
                {errors.name}
              </span>
            )}
          </div>

          {/* ---- Slug field ---- */}
          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <label
              htmlFor="project-slug"
              style={{
                fontSize: 12,
                fontWeight: 600,
                color: "var(--ink-1)",
                letterSpacing: "0.005em",
                display: "flex",
                alignItems: "center",
                gap: 6,
              }}
            >
              Slug
              {!isCreate && (
                <span
                  style={{
                    fontSize: 11,
                    fontWeight: 400,
                    color: "var(--ink-3)",
                  }}
                >
                  (cannot be changed)
                </span>
              )}
            </label>

            {/* Input row + availability indicator */}
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
              }}
            >
              <input
                id="project-slug"
                type="text"
                value={slug}
                onChange={(e) => handleSlugChange(e.target.value)}
                readOnly={!isCreate}
                autoComplete="off"
                placeholder="my-awesome-project"
                style={{
                  flex: 1,
                  height: 36,
                  padding: "0 10px",
                  borderRadius: 8,
                  border: `1px solid ${
                    errors.slug || slugStatus === "taken" || slugStatus === "invalid_format"
                      ? "#B94D2F"
                      : slugStatus === "available"
                      ? "#2D7A2D"
                      : "var(--border-3)"
                  }`,
                  background: !isCreate ? "var(--surface-2)" : "var(--surface)",
                  fontSize: 13.5,
                  color: !isCreate ? "var(--ink-2)" : "var(--ink-0)",
                  outline: "none",
                  transition: "border-color 0.12s",
                  boxSizing: "border-box",
                  cursor: !isCreate ? "default" : "text",
                  fontFamily: "monospace",
                }}
              />

              {/* Show idle / checking / available / taken inline to the right of the input */}
              {isCreate && slugStatus !== "invalid_format" && (
                <SlugStatusIndicator status={slugStatus} />
              )}
            </div>

            {errors.slug && (
              <span style={{ fontSize: 12, color: "#B94D2F", lineHeight: 1.3 }}>
                {errors.slug}
              </span>
            )}

            {/* invalid_format hint is long — render it below the input row */}
            {isCreate && slugStatus === "invalid_format" && !errors.slug && (
              <SlugStatusIndicator status="invalid_format" />
            )}
          </div>

          {/* ---- Description field ---- */}
          <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
            <label
              htmlFor="project-description"
              style={{
                fontSize: 12,
                fontWeight: 600,
                color: "var(--ink-1)",
                letterSpacing: "0.005em",
              }}
            >
              Description
            </label>
            <textarea
              id="project-description"
              value={description}
              onChange={(e) => handleDescriptionChange(e.target.value)}
              placeholder="Optional — what is this project about?"
              rows={3}
              style={{
                width: "100%",
                minHeight: 80,
                padding: "8px 10px",
                borderRadius: 8,
                border: `1px solid ${errors.description ? "#B94D2F" : "var(--border-3)"}`,
                background: "var(--surface)",
                fontSize: 13.5,
                color: "var(--ink-0)",
                outline: "none",
                resize: "vertical",
                transition: "border-color 0.12s",
                boxSizing: "border-box",
                fontFamily: "inherit",
                lineHeight: 1.5,
              }}
            />
            {errors.description && (
              <span style={{ fontSize: 12, color: "#B94D2F", lineHeight: 1.3 }}>
                {errors.description}
              </span>
            )}
          </div>

          {/* ---- Form-level error ---- */}
          <FormError message={errors._form} />

          {/* ---- Submit button ---- */}
          <button
            type="submit"
            disabled={isSubmitDisabled}
            style={{
              width: "100%",
              height: 40,
              background: isSubmitDisabled
                ? "var(--surface-3)"
                : "var(--accent-1)",
              color: isSubmitDisabled ? "var(--ink-3)" : "var(--accent-1-ink)",
              border: "none",
              borderRadius: 9,
              fontSize: 13.5,
              fontWeight: 600,
              cursor: isSubmitDisabled ? "not-allowed" : "pointer",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              gap: 8,
              transition: "background 0.12s",
              marginTop: 4,
            }}
          >
            {submitting ? (
              <Spinner size={16} />
            ) : isCreate ? (
              "Create Project"
            ) : (
              "Save Changes"
            )}
          </button>
        </form>
      </div>
    </Modal>
  );
}
