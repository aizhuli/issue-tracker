"use client";

import {
  IssueStatus,
  IssuePriority,
  MockIssue,
  STATUSES,
  PRIORITIES,
  MEMBERS,
} from "@/lib/mock-issues";
import { Icon } from "@/components/ui/Icon";

/* ── Avatar ─────────────────────────────────────────────── */

function Avatar({ id, size = 20 }: { id?: string; size?: number }) {
  if (!id) return null;
  const m = MEMBERS[id];
  if (!m) return null;
  const bg = `oklch(0.78 0.07 ${m.hue})`;
  const fg = `oklch(0.28 0.08 ${m.hue})`;
  return (
    <div
      title={m.name}
      style={{
        width: size, height: size, borderRadius: "50%",
        background: bg, color: fg,
        display: "inline-flex", alignItems: "center", justifyContent: "center",
        fontSize: size * 0.42, fontWeight: 600, letterSpacing: "0.02em",
        flexShrink: 0,
        boxShadow: "inset 0 0 0 1.5px rgba(255,255,255,0.6)",
      }}
    >
      {m.initials}
    </div>
  );
}

/* ── Priority glyph ──────────────────────────────────────── */

function PriorityGlyph({ level, size = 12 }: { level: IssuePriority; size?: number }) {
  const p = PRIORITIES[level];
  if (!p) return null;
  const heights =
    level === "urgent" ? [1, 1, 1] :
    level === "high"   ? [0.5, 0.8, 1] :
    level === "medium" ? [0.5, 0.8, 0.4] :
                         [0.5, 0.3, 0.3];
  const active =
    level === "urgent" ? [1,1,1] :
    level === "high"   ? [1,1,1] :
    level === "medium" ? [1,1,0] :
                         [1,0,0];
  return (
    <span
      title={p.label + " priority"}
      style={{ display: "inline-flex", alignItems: "flex-end", gap: 1.5, height: size, width: size, flexShrink: 0 }}
    >
      {heights.map((h, i) => (
        <span
          key={i}
          style={{
            width: 3, height: h * size,
            borderRadius: 1,
            background: active[i] ? p.color : "var(--border-2)",
          }}
        />
      ))}
    </span>
  );
}

/* ── Status dot ──────────────────────────────────────────── */

function StatusDot({ id, size = 9 }: { id: IssueStatus; size?: number }) {
  const s = STATUSES.find((x) => x.id === id);
  if (!s) return null;
  if (id === "done") {
    return (
      <span
        style={{
          width: size, height: size, borderRadius: "50%",
          background: s.dot,
          display: "inline-flex", alignItems: "center", justifyContent: "center",
          color: "#fff", fontSize: size * 0.7, fontWeight: 700,
          flexShrink: 0,
        }}
      >
        ✓
      </span>
    );
  }
  const pct =
    id === "backlog"     ? 0 :
    id === "todo"        ? 0 :
    id === "in_progress" ? 0.4 :
    id === "in_review"   ? 0.75 : 1;
  return (
    <span
      style={{
        width: size, height: size, borderRadius: "50%",
        background: `conic-gradient(${s.dot} ${pct * 360}deg, transparent 0)`,
        boxShadow: `inset 0 0 0 1.4px ${s.dot}`,
        flexShrink: 0, display: "inline-block",
      }}
    />
  );
}

/* ── Label chip ──────────────────────────────────────────── */

const LABEL_PALETTE = [
  ["#E4EFD6", "#3D5424"],
  ["#F2E4CB", "#7A571E"],
  ["#DDE6F0", "#3A5573"],
  ["#ECDDF0", "#5D3373"],
  ["#F0DDDD", "#7A3535"],
  ["#DDF0EA", "#1F5C50"],
];

function LabelChip({ text }: { text: string }) {
  let h = 0;
  for (let i = 0; i < text.length; i++) h = (h * 31 + text.charCodeAt(i)) >>> 0;
  const [bg, fg] = LABEL_PALETTE[h % LABEL_PALETTE.length];
  return (
    <span
      style={{
        display: "inline-flex", alignItems: "center", gap: 4,
        padding: "2px 7px", borderRadius: 999,
        background: bg, color: fg,
        fontSize: 10.5, fontWeight: 600, letterSpacing: "0.01em",
      }}
    >
      <span style={{ width: 5, height: 5, borderRadius: "50%", background: fg, opacity: 0.6 }} />
      {text}
    </span>
  );
}

/* ── Issue card ──────────────────────────────────────────── */

function IssueCard({ issue }: { issue: MockIssue }) {
  return (
    <div className={`card${issue.entering ? " card-entering" : ""}`}>
      <div className="card-top">
        <span className="card-id">{issue.id}</span>
        <PriorityGlyph level={issue.priority} size={12} />
      </div>
      <div className="card-title">{issue.title}</div>
      {issue.labels && issue.labels.length > 0 && (
        <div className="card-labels">
          {issue.labels.map((l) => <LabelChip key={l} text={l} />)}
        </div>
      )}
      <div className="card-bottom">
        <div className="card-meta">
          {issue.points != null && (
            <span
              style={{
                display: "inline-flex", alignItems: "center", justifyContent: "center",
                minWidth: 18, height: 18, padding: "0 5px",
                borderRadius: 5,
                background: "var(--surface-2)", color: "var(--ink-1)",
                fontSize: 10.5, fontWeight: 700,
              }}
            >
              {issue.points}
            </span>
          )}
          {(issue.comments ?? 0) > 0 && (
            <span style={{ display: "inline-flex", alignItems: "center", gap: 3, fontSize: 11, color: "var(--ink-2)" }}>
              <Icon name="comment" size={11} />
              {issue.comments}
            </span>
          )}
        </div>
        <Avatar id={issue.assignee} size={20} />
      </div>
    </div>
  );
}

/* ── Kanban column ───────────────────────────────────────── */

function KanbanColumn({ status, issues }: { status: (typeof STATUSES)[0]; issues: MockIssue[] }) {
  return (
    <div className="col">
      <div className="col-hd">
        <div className="col-hd-l">
          <StatusDot id={status.id} size={10} />
          <span className="col-name">{status.label}</span>
          <span className="col-count">{issues.length}</span>
        </div>
      </div>
      <div className="col-body">
        {issues.map((i) => (
          <IssueCard key={i.id} issue={i} />
        ))}
      </div>
    </div>
  );
}

/* ── KanbanBoard (main export) ───────────────────────────── */

export function KanbanBoard({ issues }: { issues: MockIssue[] }) {
  const grouped: Record<IssueStatus, MockIssue[]> = {
    backlog: [], todo: [], in_progress: [], in_review: [], done: [],
  };
  for (const issue of issues) {
    (grouped[issue.status] ?? (grouped[issue.status] = [])).push(issue);
  }
  return (
    <div className="board">
      {STATUSES.map((s) => (
        <KanbanColumn key={s.id} status={s} issues={grouped[s.id] ?? []} />
      ))}
    </div>
  );
}
