"use client";

import {
  useCallback,
  useEffect,
  useRef,
  useState,
} from "react";
import { useParams } from "next/navigation";
import {
  DndContext,
  DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import { Viewbar } from "@/components/shell/Viewbar";
import { BoardColumn } from "@/components/issues/BoardColumn";
import { Modal } from "@/components/ui/Modal";
import { IssueDetail } from "@/components/issues/IssueDetail";
import { CreateIssueModal } from "@/components/issues/CreateIssueModal";
import type {
  IssueFull,
  IssueSummary,
  IssueStatus,
  IssuePriority,
  UserSummary,
} from "@/lib/types/issues";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const ACTIVE_STATUSES: IssueStatus[] = ["backlog", "todo", "in-progress", "in-review"];
const DONE_STATUS: IssueStatus = "done";
const ALL_STATUSES: IssueStatus[] = [...ACTIVE_STATUSES, DONE_STATUS];

const COLUMN_LABELS: Record<IssueStatus, string> = {
  backlog: "Backlog",
  todo: "Todo",
  "in-progress": "In Progress",
  "in-review": "In Review",
  done: "Done",
};

const PRIORITY_OPTIONS: IssuePriority[] = ["low", "medium", "high", "urgent"];
const PRIORITY_LABELS: Record<IssuePriority, string> = {
  low: "Low",
  medium: "Medium",
  high: "High",
  urgent: "Urgent",
};

const PAGE_SIZE = 20;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type ProjectInfo = {
  id: string;
  slug: string;
  name: string;
  ownerId: string;
  ownerName: string;
};

type MeInfo = {
  id: string;
  name: string;
  email: string;
};

type ColumnState = {
  items: IssueSummary[];
  nextPageToken: string | null;
  loading: boolean;
};

type BoardState = Record<IssueStatus, ColumnState>;

type Filters = {
  q: string;
  priorities: IssuePriority[];
  assigneeIds: string[];
  includeUnassigned: boolean;
  showDone: boolean;
};

type DetailModal = {
  open: boolean;
  issue: IssueFull | null;
  loading: boolean;
};

type CreateModal = {
  open: boolean;
};

type Toast = {
  id: number;
  message: string;
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function emptyColumn(): ColumnState {
  return { items: [], nextPageToken: null, loading: false };
}

function emptyBoardState(): BoardState {
  return {
    backlog: emptyColumn(),
    todo: emptyColumn(),
    "in-progress": emptyColumn(),
    "in-review": emptyColumn(),
    done: emptyColumn(),
  };
}

// ---------------------------------------------------------------------------
// Page component
// ---------------------------------------------------------------------------

export default function ProjectBoardPage() {
  const params = useParams<{ slug: string }>();
  const slug = params.slug;

  const [project, setProject] = useState<ProjectInfo | null>(null);
  const [me, setMe] = useState<MeInfo | null>(null);

  const [boardState, setBoardState] = useState<BoardState>(emptyBoardState());
  const [filters, setFilters] = useState<Filters>({
    q: "",
    priorities: [],
    assigneeIds: [],
    includeUnassigned: false,
    showDone: false,
  });

  const [detailModal, setDetailModal] = useState<DetailModal>({
    open: false,
    issue: null,
    loading: false,
  });
  const [createModal, setCreateModal] = useState<CreateModal>({ open: false });

  // Inline toast state
  const [toasts, setToasts] = useState<Toast[]>([]);
  const toastCounterRef = useRef(0);

  // FIX #1 — generation counter to discard stale fetch results
  const fetchGenRef = useRef(0);

  // Assignee popover
  const [assigneePopoverOpen, setAssigneePopoverOpen] = useState(false);
  const [assigneeSearch, setAssigneeSearch] = useState("");
  const [assigneeResults, setAssigneeResults] = useState<UserSummary[]>([]);
  // FIX #6 — loading state to prevent "No users found" flash
  const [loadingUsers, setLoadingUsers] = useState(false);
  const assigneePopoverRef = useRef<HTMLDivElement>(null);
  const assigneeDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Search debounce
  const searchDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [searchInput, setSearchInput] = useState("");

  // ---------------------------------------------------------------------------
  // FIX #5 — showToast wrapped in useCallback with stable [setToasts] dep
  // ---------------------------------------------------------------------------

  const showToast = useCallback((message: string) => {
    const id = ++toastCounterRef.current;
    setToasts((prev) => [...prev, { id, message }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 3500);
  }, []);

  // ---------------------------------------------------------------------------
  // Fetch project + me on mount
  // ---------------------------------------------------------------------------

  useEffect(() => {
    Promise.all([
      fetch(`/api/projects/${slug}`).then((r) => (r.ok ? r.json() : null)),
      fetch("/api/auth/me").then((r) => (r.ok ? r.json() : null)),
    ]).then(([proj, meData]) => {
      if (proj) setProject(proj);
      if (meData) setMe(meData);
    });
  }, [slug]);

  // ---------------------------------------------------------------------------
  // Build fetch URL for a column
  // ---------------------------------------------------------------------------

  function buildIssueUrl(
    status: IssueStatus,
    pageToken: string | null,
    f: Filters,
  ): string {
    const p = new URLSearchParams();
    p.set("status", status);
    p.set("maxPageSize", String(PAGE_SIZE));
    if (pageToken) p.set("pageToken", pageToken);
    if (f.q) p.set("q", f.q);
    f.priorities.forEach((pri) => p.append("priority", pri));
    f.assigneeIds.forEach((id) => p.append("assignee", id));
    if (f.includeUnassigned) p.set("includeUnassigned", "true");
    return `/api/projects/${slug}/issues?${p.toString()}`;
  }

  // ---------------------------------------------------------------------------
  // FIX #1 — Fetch first page for all visible columns with generation guard
  // ---------------------------------------------------------------------------

  const fetchAllColumns = useCallback(
    async (f: Filters) => {
      // Claim this generation; any older in-flight fetch will be dropped.
      const gen = ++fetchGenRef.current;

      const visibleStatuses: IssueStatus[] = f.showDone
        ? ALL_STATUSES
        : ACTIVE_STATUSES;

      // Mark all visible columns as loading
      setBoardState((prev) => {
        const next = { ...prev };
        for (const status of visibleStatuses) {
          next[status] = { items: [], nextPageToken: null, loading: true };
        }
        return next;
      });

      const fetches = visibleStatuses.map(async (status) => {
        try {
          const res = await fetch(buildIssueUrl(status, null, f));
          if (!res.ok) return { status, items: [], nextPageToken: null };
          const data: { items: IssueSummary[]; nextPageToken: string | null } =
            await res.json();
          return { status, items: data.items, nextPageToken: data.nextPageToken };
        } catch {
          return { status, items: [], nextPageToken: null };
        }
      });

      const results = await Promise.all(fetches);

      setBoardState((prev) => {
        // FIX #1 — discard stale results from a superseded fetch generation
        if (gen !== fetchGenRef.current) return prev;
        const next = { ...prev };
        for (const { status, items, nextPageToken } of results) {
          next[status] = { items, nextPageToken, loading: false };
        }
        return next;
      });
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [slug],
  );

  // ---------------------------------------------------------------------------
  // On mount: fetch columns
  // ---------------------------------------------------------------------------

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchAllColumns(filters);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [slug]); // Only run on mount / slug change — filter changes handled by their own effects

  // ---------------------------------------------------------------------------
  // FIX #3 — Debounced search: no fetchAllColumns inside setFilters updater
  // ---------------------------------------------------------------------------

  useEffect(() => {
    if (searchDebounceRef.current) clearTimeout(searchDebounceRef.current);
    searchDebounceRef.current = setTimeout(() => {
      const next = { ...filters, q: searchInput };
      setFilters(next);
      fetchAllColumns(next);
    }, 300);
    return () => {
      if (searchDebounceRef.current) clearTimeout(searchDebounceRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput]);

  // ---------------------------------------------------------------------------
  // Re-fetch when non-search filters change (applyFilters)
  // Already uses the correct pattern: setFilters then fetchAllColumns separately
  // ---------------------------------------------------------------------------

  function applyFilters(next: Filters) {
    setFilters(next);
    fetchAllColumns(next);
  }

  // ---------------------------------------------------------------------------
  // Load more for a column
  // ---------------------------------------------------------------------------

  async function handleLoadMore(status: IssueStatus) {
    const col = boardState[status];
    if (!col.nextPageToken || col.loading) return;

    setBoardState((prev) => ({
      ...prev,
      [status]: { ...prev[status], loading: true },
    }));

    try {
      const res = await fetch(buildIssueUrl(status, col.nextPageToken, filters));
      if (!res.ok) return;
      const data: { items: IssueSummary[]; nextPageToken: string | null } =
        await res.json();
      setBoardState((prev) => ({
        ...prev,
        [status]: {
          items: [...prev[status].items, ...data.items],
          nextPageToken: data.nextPageToken,
          loading: false,
        },
      }));
    } catch {
      setBoardState((prev) => ({
        ...prev,
        [status]: { ...prev[status], loading: false },
      }));
    }
  }

  // ---------------------------------------------------------------------------
  // Card click → fetch full issue → open detail modal
  // ---------------------------------------------------------------------------

  async function handleCardOpen(issue: IssueSummary) {
    setDetailModal({ open: true, issue: null, loading: true });
    try {
      const res = await fetch(
        `/api/projects/${slug}/issues/${issue.number}`,
      );
      if (res.ok) {
        const full: IssueFull = await res.json();
        setDetailModal({ open: true, issue: full, loading: false });
      } else {
        setDetailModal({ open: false, issue: null, loading: false });
      }
    } catch {
      setDetailModal({ open: false, issue: null, loading: false });
    }
  }

  // ---------------------------------------------------------------------------
  // Status change from card context menu (non-DnD)
  // ---------------------------------------------------------------------------

  async function handleStatusChange(issue: IssueSummary, next: IssueStatus) {
    await doPatchStatus(issue.id, issue.status, next, issue.number);
  }

  // ---------------------------------------------------------------------------
  // DnD sensors
  // ---------------------------------------------------------------------------

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor),
  );

  // ---------------------------------------------------------------------------
  // DnD drag end — optimistic update + PATCH + FIX #2 functional rollback
  // ---------------------------------------------------------------------------

  async function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over) return;

    const issueId = active.id as string;
    const targetStatus = over.id as IssueStatus;

    // Find the source column
    let sourceStatus: IssueStatus | null = null;
    let draggedIssue: IssueSummary | null = null;
    for (const status of ALL_STATUSES) {
      const found = boardState[status].items.find((i) => i.id === issueId);
      if (found) {
        sourceStatus = status;
        draggedIssue = found;
        break;
      }
    }

    if (!sourceStatus || !draggedIssue || sourceStatus === targetStatus) return;

    const issueNumber = draggedIssue.number;
    // Capture snapshot before optimistic update for atomic rollback
    const snapshot = boardState;

    // Optimistic: remove from source, prepend to target
    const updatedIssue: IssueSummary = { ...draggedIssue, status: targetStatus };
    setBoardState((prev) => ({
      ...prev,
      [sourceStatus!]: {
        ...prev[sourceStatus!],
        items: prev[sourceStatus!].items.filter((i) => i.id !== issueId),
      },
      [targetStatus]: {
        ...prev[targetStatus],
        items: [updatedIssue, ...prev[targetStatus].items],
      },
    }));

    try {
      const res = await fetch(
        `/api/projects/${slug}/issues/${issueNumber}/status`,
        {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ status: targetStatus }),
        },
      );

      if (!res.ok) {
        // FIX #2 — functional updater so rollback applies atomically
        setBoardState(() => snapshot);
        showToast("Failed to move issue. Please try again.");
        return;
      }

      // Background refetch both columns' first pages to reconcile server order
      const [srcResult, dstResult] = await Promise.all([
        fetch(buildIssueUrl(sourceStatus, null, filters)).then((r) =>
          r.ok ? r.json() : null,
        ),
        fetch(buildIssueUrl(targetStatus, null, filters)).then((r) =>
          r.ok ? r.json() : null,
        ),
      ]);

      setBoardState((prev) => {
        const next = { ...prev };
        if (srcResult) {
          next[sourceStatus!] = {
            items: srcResult.items,
            nextPageToken: srcResult.nextPageToken,
            loading: false,
          };
        }
        if (dstResult) {
          next[targetStatus] = {
            items: dstResult.items,
            nextPageToken: dstResult.nextPageToken,
            loading: false,
          };
        }
        return next;
      });
    } catch {
      // FIX #2 — functional updater so rollback applies atomically
      setBoardState(() => snapshot);
      showToast("Network error. Please try again.");
    }
  }

  // ---------------------------------------------------------------------------
  // Shared PATCH status helper (used by card's onStatusChange)
  // FIX #2 — functional updater in all rollback paths
  // ---------------------------------------------------------------------------

  async function doPatchStatus(
    issueId: string,
    sourceStatus: IssueStatus,
    targetStatus: IssueStatus,
    issueNumber: number,
  ) {
    if (sourceStatus === targetStatus) return;

    const snapshot = boardState;
    const draggedIssue = boardState[sourceStatus].items.find(
      (i) => i.id === issueId,
    );
    if (!draggedIssue) return;

    const updatedIssue: IssueSummary = { ...draggedIssue, status: targetStatus };
    setBoardState((prev) => ({
      ...prev,
      [sourceStatus]: {
        ...prev[sourceStatus],
        items: prev[sourceStatus].items.filter((i) => i.id !== issueId),
      },
      [targetStatus]: {
        ...prev[targetStatus],
        items: [updatedIssue, ...prev[targetStatus].items],
      },
    }));

    try {
      const res = await fetch(
        `/api/projects/${slug}/issues/${issueNumber}/status`,
        {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ status: targetStatus }),
        },
      );
      if (!res.ok) {
        setBoardState(() => snapshot); // FIX #2
        showToast("Failed to update status. Please try again.");
      }
    } catch {
      setBoardState(() => snapshot); // FIX #2
      showToast("Network error. Please try again.");
    }
  }

  // ---------------------------------------------------------------------------
  // Create issue flow
  // ---------------------------------------------------------------------------

  function handleCreated(newIssue: IssueFull) {
    // Add to backlog column (prepend)
    const summary: IssueSummary = {
      id: newIssue.id,
      number: newIssue.number,
      displayKey: newIssue.displayKey,
      title: newIssue.title,
      status: newIssue.status,
      priority: newIssue.priority,
      assignee: newIssue.assignee,
      labels: newIssue.labels,
      commentCount: newIssue.commentCount,
      updatedAt: newIssue.updatedAt,
    };
    setBoardState((prev) => ({
      ...prev,
      backlog: {
        ...prev.backlog,
        items: [summary, ...prev.backlog.items],
      },
    }));
    // Open detail modal with the new issue
    setDetailModal({ open: true, issue: newIssue, loading: false });
  }

  // ---------------------------------------------------------------------------
  // FIX #4 — handleDetailChange wrapped in useCallback to avoid stale closure
  // ---------------------------------------------------------------------------

  const handleDetailChange = useCallback(
    (updated: IssueFull) => {
      // Update the modal with the freshest data
      setDetailModal((prev) => ({ ...prev, issue: updated }));

      // Update the card in-place across all columns (handles title/priority/label/assignee changes),
      // then do a background refetch to reconcile status transitions (card may have moved columns).
      setBoardState((prev) => {
        const next = { ...prev };
        for (const status of ALL_STATUSES) {
          next[status] = {
            ...next[status],
            items: next[status].items.map((i) =>
              i.id === updated.id
                ? {
                    id: updated.id,
                    number: updated.number,
                    displayKey: updated.displayKey,
                    title: updated.title,
                    status: updated.status,
                    priority: updated.priority,
                    assignee: updated.assignee,
                    labels: updated.labels,
                    commentCount: updated.commentCount,
                    updatedAt: updated.updatedAt,
                  }
                : i,
            ),
          };
        }
        return next;
      });

      // Background refetch to reconcile any status change (card may need to move columns)
      fetchAllColumns(filters);
    },
    [filters, slug, fetchAllColumns], // eslint-disable-line react-hooks/exhaustive-deps
  );

  function handleDetailDeleted() {
    if (!detailModal.issue) return;
    const deletedId = detailModal.issue.id;
    setDetailModal({ open: false, issue: null, loading: false });
    setBoardState((prev) => {
      const next = { ...prev };
      for (const status of ALL_STATUSES) {
        next[status] = {
          ...next[status],
          items: next[status].items.filter((i) => i.id !== deletedId),
        };
      }
      return next;
    });
  }

  // ---------------------------------------------------------------------------
  // Assignee popover: close on outside click
  // ---------------------------------------------------------------------------

  useEffect(() => {
    if (!assigneePopoverOpen) return;
    function handleClickOutside(e: MouseEvent) {
      if (
        assigneePopoverRef.current &&
        !assigneePopoverRef.current.contains(e.target as Node)
      ) {
        setAssigneePopoverOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [assigneePopoverOpen]);

  // ---------------------------------------------------------------------------
  // FIX #6 — Assignee search debounce with loadingUsers guard
  // ---------------------------------------------------------------------------

  useEffect(() => {
    if (!assigneePopoverOpen) return;
    if (assigneeDebounceRef.current) clearTimeout(assigneeDebounceRef.current);
    assigneeDebounceRef.current = setTimeout(async () => {
      setLoadingUsers(true);
      try {
        const p = new URLSearchParams({ maxPageSize: "10" });
        if (assigneeSearch) p.set("q", assigneeSearch);
        const res = await fetch(`/api/users/search?${p.toString()}`);
        if (res.ok) {
          const data: UserSummary[] = await res.json();
          setAssigneeResults(data);
        }
      } catch {
        // Silently ignore
      } finally {
        setLoadingUsers(false);
      }
    }, 300);
    return () => {
      if (assigneeDebounceRef.current) clearTimeout(assigneeDebounceRef.current);
    };
  }, [assigneeSearch, assigneePopoverOpen]);

  // Open popover → load initial results
  useEffect(() => {
    if (assigneePopoverOpen && assigneeResults.length === 0) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setLoadingUsers(true);
      fetch(`/api/users/search?maxPageSize=10`)
        .then((r) => (r.ok ? r.json() : []))
        .then((data: UserSummary[]) => setAssigneeResults(data))
        .catch(() => {})
        .finally(() => setLoadingUsers(false));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [assigneePopoverOpen]);

  // ---------------------------------------------------------------------------
  // Toggle helpers
  // ---------------------------------------------------------------------------

  function togglePriority(p: IssuePriority) {
    const next: Filters = {
      ...filters,
      priorities: filters.priorities.includes(p)
        ? filters.priorities.filter((x) => x !== p)
        : [...filters.priorities, p],
    };
    applyFilters(next);
  }

  function toggleAssignee(id: string) {
    const next: Filters = {
      ...filters,
      assigneeIds: filters.assigneeIds.includes(id)
        ? filters.assigneeIds.filter((x) => x !== id)
        : [...filters.assigneeIds, id],
    };
    applyFilters(next);
  }

  function toggleUnassigned() {
    const next: Filters = {
      ...filters,
      includeUnassigned: !filters.includeUnassigned,
    };
    applyFilters(next);
  }

  function toggleShowDone() {
    const next: Filters = {
      ...filters,
      showDone: !filters.showDone,
    };
    applyFilters(next);
  }

  // ---------------------------------------------------------------------------
  // Visible columns
  // ---------------------------------------------------------------------------

  const visibleStatuses: IssueStatus[] = filters.showDone
    ? ALL_STATUSES
    : ACTIVE_STATUSES;

  // ---------------------------------------------------------------------------
  // Viewbar actions
  // ---------------------------------------------------------------------------

  const viewbarActions = (
    <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
      {/* Priority filter chips */}
      <div className="chip-group">
        {PRIORITY_OPTIONS.map((p) => (
          <button
            key={p}
            type="button"
            className={`chip-group__item${filters.priorities.includes(p) ? " active" : ""}`}
            aria-pressed={filters.priorities.includes(p)}
            onClick={() => togglePriority(p)}
          >
            {PRIORITY_LABELS[p]}
          </button>
        ))}
      </div>

      {/* Assignee popover */}
      <div style={{ position: "relative" }} ref={assigneePopoverRef}>
        <button
          type="button"
          className={`chip-group__item${filters.assigneeIds.length > 0 || filters.includeUnassigned ? " active" : ""}`}
          aria-pressed={filters.assigneeIds.length > 0 || filters.includeUnassigned}
          onClick={() => setAssigneePopoverOpen((v) => !v)}
        >
          Assignee
          {(filters.assigneeIds.length > 0 || filters.includeUnassigned) && (
            <span
              style={{
                marginLeft: 4,
                background: "var(--accent-1-strong)",
                color: "var(--accent-1-ink)",
                borderRadius: 999,
                padding: "0 5px",
                fontSize: 10,
                fontWeight: 700,
              }}
            >
              {filters.assigneeIds.length + (filters.includeUnassigned ? 1 : 0)}
            </span>
          )}
        </button>

        {assigneePopoverOpen && (
          <div
            style={{
              position: "absolute",
              top: "calc(100% + 6px)",
              right: 0,
              zIndex: 200,
              background: "var(--surface)",
              border: "1px solid var(--border)",
              borderRadius: "var(--radius)",
              boxShadow: "var(--shadow-md)",
              width: 220,
              padding: "8px 0",
            }}
          >
            {/* Search */}
            <div style={{ padding: "4px 10px 8px" }}>
              <input
                type="search"
                value={assigneeSearch}
                onChange={(e) => setAssigneeSearch(e.target.value)}
                placeholder="Search users…"
                autoFocus
                style={{
                  width: "100%",
                  padding: "5px 8px",
                  border: "1px solid var(--border-3)",
                  borderRadius: "var(--radius-sm)",
                  background: "var(--surface-2)",
                  fontSize: 12.5,
                  color: "var(--ink-0)",
                  outline: "none",
                }}
              />
            </div>

            {/* Unassigned row */}
            <label
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                padding: "5px 12px",
                cursor: "pointer",
                fontSize: 12.5,
                color: "var(--ink-1)",
              }}
            >
              <input
                type="checkbox"
                checked={filters.includeUnassigned}
                onChange={toggleUnassigned}
                style={{ accentColor: "var(--accent-1-strong)" }}
              />
              Unassigned
            </label>

            {/* User rows */}
            {assigneeResults.map((user) => (
              <label
                key={user.id}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  padding: "5px 12px",
                  cursor: "pointer",
                  fontSize: 12.5,
                  color: "var(--ink-1)",
                }}
              >
                <input
                  type="checkbox"
                  checked={filters.assigneeIds.includes(user.id)}
                  onChange={() => toggleAssignee(user.id)}
                  style={{ accentColor: "var(--accent-1-strong)" }}
                />
                {user.name}
              </label>
            ))}

            {/* FIX #6 — only show empty state when not loading and user has typed a query */}
            {!loadingUsers && assigneeResults.length === 0 && assigneeSearch && (
              <div
                style={{
                  padding: "6px 12px",
                  fontSize: 12,
                  color: "var(--ink-3)",
                  fontStyle: "italic",
                }}
              >
                No users found
              </div>
            )}
          </div>
        )}
      </div>

      {/* Show Done toggle */}
      <button
        type="button"
        className={`chip-group__item${filters.showDone ? " active" : ""}`}
        aria-pressed={filters.showDone}
        onClick={toggleShowDone}
      >
        Show Done
      </button>

      {/* New issue button */}
      <button
        type="button"
        onClick={() => setCreateModal({ open: true })}
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
        onMouseEnter={(e) => {
          e.currentTarget.style.opacity = "0.88";
        }}
        onMouseLeave={(e) => {
          e.currentTarget.style.opacity = "1";
        }}
      >
        + New issue
      </button>
    </div>
  );

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        height: "100%",
        overflow: "hidden",
      }}
    >
      {/* Viewbar */}
      <Viewbar
        title={project ? project.name : slug}
        search={{ value: searchInput, onChange: setSearchInput }}
        actions={viewbarActions}
      />

      {/* Board */}
      <DndContext sensors={sensors} onDragEnd={handleDragEnd}>
        <div className="board-row">
          {visibleStatuses.map((status) => {
            const col = boardState[status];
            return (
              <BoardColumn
                key={status}
                status={status}
                label={COLUMN_LABELS[status]}
                items={col.items}
                loading={col.loading}
                nextPageToken={col.nextPageToken}
                onLoadMore={() => handleLoadMore(status)}
                onCardOpen={handleCardOpen}
                onStatusChange={handleStatusChange}
              />
            );
          })}
        </div>
      </DndContext>

      {/* Detail modal */}
      <Modal
        open={detailModal.open}
        onClose={() => setDetailModal({ open: false, issue: null, loading: false })}
        className="ai-modal--wide"
      >
        {detailModal.loading || !detailModal.issue ? (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              height: 200,
            }}
          >
            <span
              aria-label="Loading…"
              style={{
                display: "inline-block",
                width: 28,
                height: 28,
                border: "3px solid var(--border-3)",
                borderTopColor: "var(--ink-2)",
                borderRadius: "50%",
                animation: "spin 0.7s linear infinite",
              }}
            />
          </div>
        ) : (
          <IssueDetail
            issue={detailModal.issue}
            me={me ?? { id: "", name: "", email: "" }}
            projectOwnerId={project?.ownerId ?? ""}
            projectSlug={slug}
            onChange={handleDetailChange}
            onDeleted={handleDetailDeleted}
            onClose={() =>
              setDetailModal({ open: false, issue: null, loading: false })
            }
            openInPageUrl={`/projects/${slug}/issues/${detailModal.issue.number}`}
          />
        )}
      </Modal>

      {/* Create issue modal */}
      <CreateIssueModal
        open={createModal.open}
        projectSlug={slug}
        onClose={() => setCreateModal({ open: false })}
        onCreated={(issue) => {
          setCreateModal({ open: false });
          handleCreated(issue);
        }}
      />

      {/* Inline toasts */}
      {toasts.length > 0 && (
        <div
          style={{
            position: "fixed",
            bottom: 24,
            left: "50%",
            transform: "translateX(-50%)",
            zIndex: 300,
            display: "flex",
            flexDirection: "column",
            gap: 8,
            alignItems: "center",
            pointerEvents: "none",
          }}
        >
          {toasts.map((t) => (
            <div
              key={t.id}
              role="alert"
              style={{
                background: "var(--ink-0)",
                color: "var(--surface)",
                padding: "9px 18px",
                borderRadius: "var(--radius-sm)",
                fontSize: 13,
                fontWeight: 500,
                boxShadow: "var(--shadow-md)",
                animation: "scrim-in 160ms ease-out both",
              }}
            >
              {t.message}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
