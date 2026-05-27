"use client";

import { useEffect, useRef, useState } from "react";
import { useDraggable } from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import { Avatar } from "@/components/ui/Avatar";
import type { IssueSummary, IssueStatus, LabelDto } from "@/lib/types/issues";

const STATUS_LABELS: Record<IssueStatus, string> = {
  backlog: "Backlog",
  todo: "Todo",
  "in-progress": "In Progress",
  "in-review": "In Review",
  done: "Done",
};

const STATUS_ORDER: IssueStatus[] = ["backlog", "todo", "in-progress", "in-review", "done"];

const PRIORITY_GLYPHS: Record<string, string> = {
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

interface IssueCardProps {
  issue: IssueSummary;
  onOpen: () => void;
  onStatusChange: (next: IssueStatus) => void;
}

export function IssueCard({ issue, onOpen, onStatusChange }: IssueCardProps) {
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: issue.id,
  });

  useEffect(() => {
    if (!dropdownOpen) return;
    function handleMouseDown(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    document.addEventListener("mousedown", handleMouseDown);
    return () => document.removeEventListener("mousedown", handleMouseDown);
  }, [dropdownOpen]);

  const visibleLabels = issue.labels.slice(0, 2);
  const overflowCount = issue.labels.length - visibleLabels.length;

  return (
    <div
      ref={setNodeRef}
      className="issue-card"
      data-issue-id={issue.id}
      data-issue-number={issue.number}
      style={{
        transform: CSS.Translate.toString(transform),
        opacity: isDragging ? 0 : undefined,
        cursor: "grab",
        transition: isDragging ? undefined : "opacity 150ms",
      }}
      {...attributes}
      {...listeners}
      onClick={onOpen}
    >
      <div className="issue-card__top">
        <span className="issue-card__key">{issue.displayKey}</span>
        <div ref={dropdownRef} style={{ position: "relative" }}>
          <button
            className="issue-card__caret"
            aria-label="Change status"
            onClick={(e) => {
              e.stopPropagation();
              setDropdownOpen((prev) => !prev);
            }}
          >
            ▾
          </button>
          {dropdownOpen && (
            <div
              style={{
                position: "absolute",
                top: "100%",
                right: 0,
                zIndex: 20,
                background: "var(--surface-2, #1e1e2e)",
                border: "1px solid var(--border, #2e2e3e)",
                borderRadius: 6,
                minWidth: 140,
                boxShadow: "0 4px 16px rgba(0,0,0,0.25)",
                overflow: "hidden",
              }}
              onClick={(e) => e.stopPropagation()}
            >
              {STATUS_ORDER.map((s) => (
                <button
                  key={s}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    width: "100%",
                    padding: "7px 12px",
                    background: s === issue.status ? "var(--surface-3, #2a2a3a)" : "transparent",
                    border: "none",
                    cursor: "pointer",
                    color: "var(--text-primary, #e0e0ef)",
                    fontSize: 13,
                    textAlign: "left",
                  }}
                  onClick={() => {
                    onStatusChange(s);
                    setDropdownOpen(false);
                  }}
                >
                  <span className={`status-dot--${s}`} />
                  {STATUS_LABELS[s]}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="issue-card__title">{issue.title}</div>

      <div className="issue-card__meta">
        <div className="issue-card__meta-left">
          <span
            className={`priority-icon priority-icon--${issue.priority}`}
            aria-label={issue.priority}
          >
            {PRIORITY_GLYPHS[issue.priority]}
          </span>
          {issue.assignee && (
            <Avatar id={issue.assignee.id} name={issue.assignee.name} size={20} />
          )}
          {issue.commentCount > 0 && (
            <span style={{ fontSize: 12, color: "var(--text-secondary, #888)" }}>
              💬 {issue.commentCount}
            </span>
          )}
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: 4, flexWrap: "nowrap" }}>
          {visibleLabels.map((label: LabelDto) => (
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
          {overflowCount > 0 && (
            <span className="label-chip label-chip--overflow">+{overflowCount}</span>
          )}
        </div>
      </div>
    </div>
  );
}
