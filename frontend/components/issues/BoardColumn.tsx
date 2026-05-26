"use client";

import { useDroppable } from "@dnd-kit/core";
import { IssueCard } from "@/components/issues/IssueCard";
import type { IssueSummary, IssueStatus } from "@/lib/types/issues";

function statusToCssClass(status: IssueStatus): string {
  return ({ "in-progress": "progress", "in-review": "review" } as Record<string, string>)[status] ?? status;
}

interface BoardColumnProps {
  status: IssueStatus;
  label: string;
  items: IssueSummary[];
  loading: boolean;
  nextPageToken: string | null;
  onLoadMore: () => void;
  onCardOpen: (issue: IssueSummary) => void;
  onStatusChange: (issue: IssueSummary, next: IssueStatus) => void;
}

export function BoardColumn({
  status,
  label,
  items,
  loading,
  nextPageToken,
  onLoadMore,
  onCardOpen,
  onStatusChange,
}: BoardColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: status });

  const cssStatus = statusToCssClass(status);

  return (
    <div className={`board-col board-col--${cssStatus}`}>
      <div className="board-col__header">
        <div className="board-col__header-left">
          <span className={`status-dot--${status}`} />
          <span
            style={{
              fontSize: 12.5,
              fontWeight: 600,
              color: "var(--ink-1)",
              fontFamily: "Manrope, sans-serif",
            }}
          >
            {label}
          </span>
          <span
            style={{
              fontSize: 10.5,
              fontWeight: 700,
              color: "var(--ink-3)",
              background: "var(--surface-2)",
              borderRadius: 999,
              padding: "1px 7px",
              fontFamily: "Manrope, sans-serif",
            }}
          >
            {items.length}
          </span>
        </div>
      </div>

      <div
        ref={setNodeRef}
        className="board-col__body"
        style={
          isOver
            ? { boxShadow: "inset 0 0 0 2px var(--border-3)", transition: "box-shadow 150ms" }
            : { transition: "box-shadow 150ms" }
        }
      >
        {loading && items.length === 0 ? (
          <>
            <div style={{ height: 72, borderRadius: 6, background: "var(--surface-2)" }} />
            <div style={{ height: 72, borderRadius: 6, background: "var(--surface-2)" }} />
            <div style={{ height: 72, borderRadius: 6, background: "var(--surface-2)" }} />
          </>
        ) : (
          items.map((issue) => (
            <IssueCard
              key={issue.id}
              issue={issue}
              onOpen={() => onCardOpen(issue)}
              onStatusChange={(next) => onStatusChange(issue, next)}
            />
          ))
        )}
      </div>

      {nextPageToken !== null && (
        <div className="board-col__footer">
          <button
            onClick={onLoadMore}
            style={{
              width: "100%",
              padding: "6px 0",
              background: "transparent",
              border: "1px solid var(--border)",
              borderRadius: "var(--radius-sm)",
              cursor: "pointer",
              fontSize: 12,
              fontWeight: 600,
              color: "var(--ink-2)",
              fontFamily: "Manrope, sans-serif",
            }}
          >
            Load more
          </button>
        </div>
      )}
    </div>
  );
}
