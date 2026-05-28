"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { Viewbar } from "@/components/shell/Viewbar";
import { ProjectCard } from "@/components/projects/ProjectCard";
import { ProjectFormModal } from "@/components/projects/ProjectFormModal";
import { DeleteProjectDialog } from "@/components/projects/DeleteProjectDialog";

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

type PageResponse = {
  items: ProjectSummary[];
  nextPageToken: string | null;
};

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default function ProjectsPage() {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [nextPageToken, setNextPageToken] = useState<string | null>(null);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);

  const [me, setMe] = useState<{ id: string } | null>(null);

  const [formModal, setFormModal] = useState<{
    open: boolean;
    mode: "create" | "edit";
    project?: ProjectFull;
  }>({ open: false, mode: "create" });

  const [deleteDialog, setDeleteDialog] = useState<{
    open: boolean;
    project?: ProjectSummary;
  }>({ open: false });

  // Debounce timer ref
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // ---------------------------------------------------------------------------
  // Fetch current user once on mount
  // ---------------------------------------------------------------------------

  useEffect(() => {
    fetch("/api/auth/me")
      .then((res) => (res.ok ? res.json() : null))
      .then((data: { id: string; email: string; name: string } | null) => {
        if (data) setMe({ id: data.id });
      })
      .catch(() => {
        // Session unavailable — me stays null; cards simply won't show owner actions
      });
  }, []);

  // ---------------------------------------------------------------------------
  // First-page fetch (replace list)
  // ---------------------------------------------------------------------------

  const fetchFirstPage = useCallback(async (search: string) => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ maxPageSize: "24" });
      if (search) params.set("q", search);
      const res = await fetch(`/api/projects?${params.toString()}`);
      if (!res.ok) return;
      const data: PageResponse = await res.json();
      setProjects(data.items);
      setNextPageToken(data.nextPageToken);
    } catch {
      // Silently ignore — keeps UI usable if BFF is temporarily down
    } finally {
      setLoading(false);
    }
  }, []);

  // ---------------------------------------------------------------------------
  // On mount: load first page
  // ---------------------------------------------------------------------------

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchFirstPage("");
  }, [fetchFirstPage]);

  // ---------------------------------------------------------------------------
  // Debounced search: re-fetch first page when q changes
  // ---------------------------------------------------------------------------

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      fetchFirstPage(q);
    }, 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [q, fetchFirstPage]);

  // ---------------------------------------------------------------------------
  // Load more (append to list)
  // ---------------------------------------------------------------------------

  const handleLoadMore = async () => {
    if (!nextPageToken || loadingMore) return;
    setLoadingMore(true);
    try {
      const params = new URLSearchParams({ maxPageSize: "24", pageToken: nextPageToken });
      if (q) params.set("q", q);
      const res = await fetch(`/api/projects?${params.toString()}`);
      if (!res.ok) return;
      const data: PageResponse = await res.json();
      setProjects((prev) => [...prev, ...data.items]);
      setNextPageToken(data.nextPageToken);
    } catch {
      // Silently ignore
    } finally {
      setLoadingMore(false);
    }
  };

  // ---------------------------------------------------------------------------
  // Modal / dialog helpers
  // ---------------------------------------------------------------------------

  function openCreate() {
    setFormModal({ open: true, mode: "create", project: undefined });
  }

  function openEdit(project: ProjectSummary) {
    // ProjectCard only has ProjectSummary; cast to ProjectFull for the modal
    // (description and updatedAt may be missing — modal handles undefined gracefully)
    setFormModal({
      open: true,
      mode: "edit",
      project: project as unknown as ProjectFull,
    });
  }

  function openDelete(project: ProjectSummary) {
    setDeleteDialog({ open: true, project });
  }

  function handleFormSaved(_saved: ProjectFull) {
    setFormModal({ open: false, mode: "create" });
    fetchFirstPage(q);
  }

  function handleFormClose() {
    setFormModal((prev) => ({ ...prev, open: false }));
  }

  function handleDeleted() {
    setDeleteDialog({ open: false });
    fetchFirstPage(q);
  }

  function handleDeleteClose() {
    setDeleteDialog((prev) => ({ ...prev, open: false }));
  }

  // ---------------------------------------------------------------------------
  // Empty state condition: no query, no results, not loading
  // ---------------------------------------------------------------------------

  const showEmpty = !loading && projects.length === 0 && !q;

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", overflow: "hidden" }}>
      {/* Viewbar */}
      <Viewbar
        title="Projects"
        search={{ value: q, onChange: setQ }}
        actions={
          <button
            onClick={openCreate}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 4,
              height: 30,
              padding: "0 12px",
              background: "var(--accent-1)",
              color: "var(--accent-1-ink)",
              border: "none",
              borderRadius: "var(--radius-sm)",
              fontSize: 12.5,
              fontWeight: 600,
              cursor: "pointer",
              whiteSpace: "nowrap",
              transition: "opacity 0.12s",
            }}
            onMouseEnter={(e) => { e.currentTarget.style.opacity = "0.88"; }}
            onMouseLeave={(e) => { e.currentTarget.style.opacity = "1"; }}
          >
            + New project
          </button>
        }
      />

      {/* Scrollable content area */}
      <div style={{ flex: 1, overflowY: "auto" }}>
        {/* Empty state */}
        {showEmpty ? (
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              justifyContent: "center",
              gap: 16,
              padding: "80px 24px",
              color: "var(--ink-2)",
            }}
          >
            <span style={{ fontSize: 48 }}>📁</span>
            <p style={{ margin: 0, fontSize: 14 }}>
              No projects yet — create your first
            </p>
            <button
              onClick={openCreate}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 4,
                height: 36,
                padding: "0 16px",
                background: "var(--accent-1)",
                color: "var(--accent-1-ink)",
                border: "none",
                borderRadius: "var(--radius-sm)",
                fontSize: 13,
                fontWeight: 600,
                cursor: "pointer",
                transition: "opacity 0.12s",
              }}
              onMouseEnter={(e) => { e.currentTarget.style.opacity = "0.88"; }}
              onMouseLeave={(e) => { e.currentTarget.style.opacity = "1"; }}
            >
              + New project
            </button>
          </div>
        ) : (
          <div style={{ padding: 24 }}>
            {/* Search-with-no-results state */}
            {!loading && projects.length === 0 && q && (
              <div
                style={{
                  textAlign: "center",
                  padding: "60px 24px",
                  color: "var(--ink-2)",
                  fontSize: 14,
                }}
              >
                No projects match &ldquo;{q}&rdquo;
              </div>
            )}

            {/* Grid */}
            {projects.length > 0 && (
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
                  gap: 16,
                }}
              >
                {projects.map((project) => (
                  <ProjectCard
                    key={project.id}
                    project={project}
                    me={me}
                    onEdit={() => openEdit(project)}
                    onDelete={() => openDelete(project)}
                  />
                ))}
              </div>
            )}

            {/* Load more */}
            {nextPageToken && (
              <div
                style={{
                  display: "flex",
                  justifyContent: "center",
                  marginTop: 24,
                }}
              >
                <button
                  onClick={handleLoadMore}
                  disabled={loadingMore}
                  style={{
                    height: 36,
                    padding: "0 20px",
                    background: "var(--surface-2)",
                    color: "var(--ink-1)",
                    border: "1px solid var(--border)",
                    borderRadius: "var(--radius-sm)",
                    fontSize: 13,
                    fontWeight: 500,
                    cursor: loadingMore ? "not-allowed" : "pointer",
                    opacity: loadingMore ? 0.6 : 1,
                    transition: "background 0.12s",
                  }}
                  onMouseEnter={(e) => {
                    if (!loadingMore) e.currentTarget.style.background = "var(--surface-3)";
                  }}
                  onMouseLeave={(e) => {
                    if (!loadingMore) e.currentTarget.style.background = "var(--surface-2)";
                  }}
                >
                  {loadingMore ? "Loading…" : "Load more"}
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Modals */}
      <ProjectFormModal
        open={formModal.open}
        mode={formModal.mode}
        project={formModal.project}
        onClose={handleFormClose}
        onSaved={handleFormSaved}
      />

      {deleteDialog.project && (
        <DeleteProjectDialog
          open={deleteDialog.open}
          project={deleteDialog.project}
          onClose={handleDeleteClose}
          onDeleted={handleDeleted}
        />
      )}
    </div>
  );
}
