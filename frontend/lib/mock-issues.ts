export type IssueStatus =
  | "backlog"
  | "todo"
  | "in_progress"
  | "in_review"
  | "done";

export type IssuePriority = "urgent" | "high" | "medium" | "low";

export interface MockIssue {
  id: string;
  title: string;
  status: IssueStatus;
  priority: IssuePriority;
  points?: number;
  assignee?: string;
  labels?: string[];
  comments?: number;
  entering?: boolean;
}

export const STATUSES: { id: IssueStatus; label: string; dot: string }[] = [
  { id: "backlog",     label: "Backlog",     dot: "#A8B0A2" },
  { id: "todo",        label: "Todo",        dot: "#7B95B8" },
  { id: "in_progress", label: "In Progress", dot: "#D4A24C" },
  { id: "in_review",   label: "In Review",   dot: "#9E7BC1" },
  { id: "done",        label: "Done",        dot: "#6FAE5A" },
];

export const PRIORITIES: Record<
  IssuePriority,
  { label: string; color: string }
> = {
  urgent: { label: "Urgent", color: "#C5523E" },
  high:   { label: "High",   color: "#D4A24C" },
  medium: { label: "Medium", color: "#5A6B5E" },
  low:    { label: "Low",    color: "#8A9489" },
};

export const MEMBERS: Record<string, { name: string; initials: string; hue: number }> = {
  u1: { name: "Maya Chen",    initials: "MC", hue: 142 },
  u2: { name: "Jordan Reyes", initials: "JR", hue: 28  },
  u3: { name: "Priya Shah",   initials: "PS", hue: 268 },
  u4: { name: "Theo Nilsson", initials: "TN", hue: 200 },
  u5: { name: "Imani Brooks", initials: "IB", hue: 12  },
  u6: { name: "Sam Okafor",   initials: "SO", hue: 178 },
};

export const MOCK_ISSUES: MockIssue[] = [
  { id: "WEB-148", title: "Audit accessibility of the onboarding flow",       status: "backlog",     priority: "medium", points: 5, assignee: "u1", labels: ["a11y","research"], comments: 1 },
  { id: "WEB-152", title: "Investigate slow dashboard queries on Postgres",    status: "backlog",     priority: "high",   points: 8, assignee: "u4", labels: ["perf","backend"],  comments: 3 },
  { id: "WEB-153", title: "Spec: AI-assisted issue triage",                   status: "backlog",     priority: "medium", points: 3, assignee: "u3", labels: ["ai","spec"],       comments: 5 },
  { id: "WEB-160", title: "Empty state illustrations for boards",             status: "backlog",     priority: "low",    points: 2, assignee: "u6", labels: ["design"],          comments: 0 },
  { id: "WEB-141", title: "Implement drag-and-drop reorder within a column",  status: "todo",        priority: "high",   points: 5, assignee: "u2", labels: ["frontend","board"],comments: 2 },
  { id: "WEB-145", title: "Add Markdown support to issue descriptions",       status: "todo",        priority: "medium", points: 3, assignee: "u5", labels: ["frontend"],        comments: 0 },
  { id: "WEB-149", title: "Filter chips: by assignee, label, priority",      status: "todo",        priority: "medium", points: 3, assignee: "u1", labels: ["frontend","filters"],comments: 1 },
  { id: "WEB-136", title: "Issue detail drawer: comments + activity",         status: "in_progress", priority: "high",   points: 8, assignee: "u2", labels: ["frontend","drawer"],comments: 7 },
  { id: "WEB-139", title: "Natural-language quick-create command bar",        status: "in_progress", priority: "urgent", points: 5, assignee: "u3", labels: ["ai","frontend"],   comments: 4 },
  { id: "WEB-142", title: "Column WIP limits with soft warning",              status: "in_progress", priority: "medium", points: 2, assignee: "u5", labels: ["board"],           comments: 0 },
  { id: "WEB-128", title: "Sidebar redesign — workspace switcher",            status: "in_review",   priority: "medium", points: 5, assignee: "u6", labels: ["design","frontend"],comments: 11 },
  { id: "WEB-133", title: "Keyboard shortcuts: J/K to move between cards",   status: "in_review",   priority: "low",    points: 2, assignee: "u4", labels: ["frontend","a11y"], comments: 3 },
  { id: "WEB-118", title: "Auth: passkey sign-in",                            status: "done",        priority: "high",   points: 8, assignee: "u4", labels: ["auth","backend"],  comments: 6 },
  { id: "WEB-122", title: "Notification center skeleton",                     status: "done",        priority: "medium", points: 3, assignee: "u2", labels: ["frontend"],        comments: 2 },
  { id: "WEB-125", title: "Brand tokens: type scale + spacing",               status: "done",        priority: "low",    points: 2, assignee: "u6", labels: ["design","tokens"], comments: 0 },
];

let _idCounter = 200;
export function createMockIssue(status: IssueStatus): MockIssue {
  const ids = ["WEB", "MOB", "API", "DSN"];
  const prefix = ids[Math.floor(Math.random() * ids.length)];
  const titles = [
    "Fix flaky test in CI pipeline",
    "Refactor token refresh logic",
    "Add pagination to search results",
    "Update onboarding copy",
    "Investigate memory leak in worker",
    "Improve error messages for validation",
    "Dark mode polish pass",
    "Add export to CSV feature",
  ];
  const labelSets = [
    ["frontend"], ["backend"], ["design"], ["ai"], ["a11y"], ["perf"],
  ];
  const assignees = Object.keys(MEMBERS);
  return {
    id: `${prefix}-${++_idCounter}`,
    title: titles[Math.floor(Math.random() * titles.length)],
    status,
    priority: (["low", "medium", "high", "urgent"] as IssuePriority[])[
      Math.floor(Math.random() * 4)
    ],
    points: [1, 2, 3, 5, 8][Math.floor(Math.random() * 5)],
    assignee: assignees[Math.floor(Math.random() * assignees.length)],
    labels: labelSets[Math.floor(Math.random() * labelSets.length)],
    comments: 0,
    entering: true,
  };
}
