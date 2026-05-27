"use client";

import { useState } from "react";
import { Icon } from "@/components/ui/Icon";
import {
  DemoIssue,
  DemoIssueStatus,
  DemoIssuePriority,
  useDemoAnimation,
} from "./useDemoAnimation";

// ---------------------------------------------------------------------------
// Static mock data (was previously in lib/mock-issues.ts)
// ---------------------------------------------------------------------------

const MEMBERS: Record<string, { name: string; initials: string; hue: number }> = {
  u1: { name: "Maya Chen",    initials: "MC", hue: 142 },
  u2: { name: "Jordan Reyes", initials: "JR", hue: 28  },
  u3: { name: "Priya Shah",   initials: "PS", hue: 268 },
  u4: { name: "Theo Nilsson", initials: "TN", hue: 200 },
  u5: { name: "Imani Brooks", initials: "IB", hue: 12  },
  u6: { name: "Sam Okafor",   initials: "SO", hue: 178 },
};

const PRIORITIES: Record<DemoIssuePriority, { label: string; color: string }> = {
  urgent: { label: "Urgent", color: "#C5523E" },
  high:   { label: "High",   color: "#D4A24C" },
  medium: { label: "Medium", color: "#5A6B5E" },
  low:    { label: "Low",    color: "#8A9489" },
};

const STATUSES: { id: DemoIssueStatus; label: string; dot: string }[] = [
  { id: "backlog",     label: "Backlog",     dot: "#A8B0A2" },
  { id: "todo",        label: "Todo",        dot: "#7B95B8" },
  { id: "in_progress", label: "In Progress", dot: "#D4A24C" },
  { id: "in_review",   label: "In Review",   dot: "#9E7BC1" },
  { id: "done",        label: "Done",        dot: "#6FAE5A" },
];

const MOCK_ISSUES: DemoIssue[] = [
  { id: "WEB-148", title: "Audit accessibility of the onboarding flow",      status: "backlog",     priority: "medium", points: 5, assignee: "u1", labels: ["a11y","research"], comments: 1 },
  { id: "WEB-152", title: "Investigate slow dashboard queries on Postgres",   status: "backlog",     priority: "high",   points: 8, assignee: "u4", labels: ["perf","backend"],  comments: 3 },
  { id: "WEB-153", title: "Spec: AI-assisted issue triage",                  status: "backlog",     priority: "medium", points: 3, assignee: "u3", labels: ["ai","spec"],       comments: 5 },
  { id: "WEB-160", title: "Empty state illustrations for boards",            status: "backlog",     priority: "low",    points: 2, assignee: "u6", labels: ["design"],          comments: 0 },
  { id: "WEB-141", title: "Implement drag-and-drop reorder within a column", status: "todo",        priority: "high",   points: 5, assignee: "u2", labels: ["frontend","board"],comments: 2 },
  { id: "WEB-145", title: "Add Markdown support to issue descriptions",      status: "todo",        priority: "medium", points: 3, assignee: "u5", labels: ["frontend"],        comments: 0 },
  { id: "WEB-149", title: "Filter chips: by assignee, label, priority",     status: "todo",        priority: "medium", points: 3, assignee: "u1", labels: ["frontend","filters"],comments: 1 },
  { id: "WEB-136", title: "Issue detail drawer: comments + activity",        status: "in_progress", priority: "high",   points: 8, assignee: "u2", labels: ["frontend","drawer"],comments: 7 },
  { id: "WEB-139", title: "Natural-language quick-create command bar",       status: "in_progress", priority: "urgent", points: 5, assignee: "u3", labels: ["ai","frontend"],   comments: 4 },
  { id: "WEB-142", title: "Column WIP limits with soft warning",             status: "in_progress", priority: "medium", points: 2, assignee: "u5", labels: ["board"],           comments: 0 },
  { id: "WEB-128", title: "Sidebar redesign — workspace switcher",           status: "in_review",   priority: "medium", points: 5, assignee: "u6", labels: ["design","frontend"],comments: 11 },
  { id: "WEB-133", title: "Keyboard shortcuts: J/K to move between cards",  status: "in_review",   priority: "low",    points: 2, assignee: "u4", labels: ["frontend","a11y"], comments: 3 },
  { id: "WEB-118", title: "Auth: passkey sign-in",                           status: "done",        priority: "high",   points: 8, assignee: "u4", labels: ["auth","backend"],  comments: 6 },
  { id: "WEB-122", title: "Notification center skeleton",                    status: "done",        priority: "medium", points: 3, assignee: "u2", labels: ["frontend"],        comments: 2 },
  { id: "WEB-125", title: "Brand tokens: type scale + spacing",              status: "done",        priority: "low",    points: 2, assignee: "u6", labels: ["design","tokens"], comments: 0 },
];

// ---------------------------------------------------------------------------
// Sub-components (inlined from old KanbanBoard.tsx)
// ---------------------------------------------------------------------------

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

function PriorityGlyph({ level, size = 12 }: { level: DemoIssuePriority; size?: number }) {
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

function StatusDot({ id, size = 9 }: { id: DemoIssueStatus; size?: number }) {
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

const LABEL_PALETTE: [string, string][] = [
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

function DemoIssueCard({ issue }: { issue: DemoIssue }) {
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

function DemoColumn({ status, issues }: { status: (typeof STATUSES)[0]; issues: DemoIssue[] }) {
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
          <DemoIssueCard key={i.id} issue={i} />
        ))}
      </div>
    </div>
  );
}

function DemoKanbanBoard({ issues }: { issues: DemoIssue[] }) {
  const grouped: Record<DemoIssueStatus, DemoIssue[]> = {
    backlog: [], todo: [], in_progress: [], in_review: [], done: [],
  };
  for (const issue of issues) {
    (grouped[issue.status] ?? (grouped[issue.status] = [])).push(issue);
  }
  return (
    <div className="board">
      {STATUSES.map((s) => (
        <DemoColumn key={s.id} status={s} issues={grouped[s.id] ?? []} />
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// DemoBoard (exported)
// ---------------------------------------------------------------------------

export function DemoBoard() {
  const [issues, setIssues] = useState(MOCK_ISSUES);
  useDemoAnimation(issues, setIssues);

  return (
    <div
      style={{
        position: "absolute",
        inset: 0,
        overflow: "hidden",
        pointerEvents: "none",
        userSelect: "none",
        filter: "blur(5px) saturate(0.85) brightness(0.97)",
        transform: "scale(1.06)",
        transformOrigin: "center center",
      }}
    >
      <DemoKanbanBoard issues={issues} />
    </div>
  );
}
