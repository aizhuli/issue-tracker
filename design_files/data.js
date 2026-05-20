// Mock data for the issue tracker.
window.MOCK_DATA = (() => {
  const members = [
    { id: 'u1', name: 'Maya Chen',    initials: 'MC', hue: 142, role: 'Design' },
    { id: 'u2', name: 'Jordan Reyes', initials: 'JR', hue: 28,  role: 'Eng' },
    { id: 'u3', name: 'Priya Shah',   initials: 'PS', hue: 268, role: 'PM' },
    { id: 'u4', name: 'Theo Nilsson', initials: 'TN', hue: 200, role: 'Eng' },
    { id: 'u5', name: 'Imani Brooks', initials: 'IB', hue: 12,  role: 'Eng' },
    { id: 'u6', name: 'Sam Okafor',   initials: 'SO', hue: 178, role: 'Design' },
  ];

  const projects = [
    { id: 'p1', key: 'WEB', name: 'Web Platform',  color: '#7AB85E', emoji: '◐' },
    { id: 'p2', key: 'MOB', name: 'Mobile App',    color: '#D4A24C', emoji: '◑' },
    { id: 'p3', key: 'API', name: 'Public API',    color: '#7B95B8', emoji: '◒' },
    { id: 'p4', key: 'DSN', name: 'Design System', color: '#9E7BC1', emoji: '◓' },
  ];

  // statuses: backlog, todo, in_progress, in_review, done
  const issues = [
    // BACKLOG
    { id: 'WEB-148', title: 'Audit accessibility of the onboarding flow', desc: 'Run a full WCAG 2.2 AA pass on the onboarding flow. Focus on keyboard nav and screen reader labels.', status: 'backlog', priority: 'medium', points: 5, assignee: 'u1', labels: ['a11y','research'], created: 'May 2', updated: '2d', comments: 1 },
    { id: 'WEB-152', title: 'Investigate slow dashboard queries on Postgres', desc: '', status: 'backlog', priority: 'high', points: 8, assignee: 'u4', labels: ['perf','backend'], created: 'May 4', updated: '1d', comments: 3 },
    { id: 'WEB-153', title: 'Spec: AI-assisted issue triage', desc: 'Define how the AI suggests labels, assignees, and priority on new issues.', status: 'backlog', priority: 'medium', points: 3, assignee: 'u3', labels: ['ai','spec'], created: 'May 5', updated: '1d', comments: 5 },
    { id: 'WEB-160', title: 'Empty state illustrations for boards', desc: '', status: 'backlog', priority: 'low', points: 2, assignee: 'u6', labels: ['design'], created: 'May 6', updated: '4h', comments: 0 },

    // TODO
    { id: 'WEB-141', title: 'Implement drag-and-drop reorder within a column', desc: 'Cards should reorder within a column with a soft drop indicator. Persist order to backend.', status: 'todo', priority: 'high', points: 5, assignee: 'u2', labels: ['frontend','board'], created: 'Apr 28', updated: '3h', comments: 2 },
    { id: 'WEB-145', title: 'Add Markdown support to issue descriptions', desc: '', status: 'todo', priority: 'medium', points: 3, assignee: 'u5', labels: ['frontend'], created: 'Apr 30', updated: '6h', comments: 0 },
    { id: 'WEB-149', title: 'Filter chips: by assignee, label, priority', desc: '', status: 'todo', priority: 'medium', points: 3, assignee: 'u1', labels: ['frontend','filters'], created: 'May 3', updated: '2d', comments: 1 },

    // IN PROGRESS
    { id: 'WEB-136', title: 'Issue detail drawer: comments + activity', desc: 'Right-side drawer that shows full issue context. Tabs for Comments, Activity, Links.', status: 'in_progress', priority: 'high', points: 8, assignee: 'u2', labels: ['frontend','drawer'], created: 'Apr 24', updated: '12m', comments: 7 },
    { id: 'WEB-139', title: 'Natural-language quick-create command bar', desc: 'Type "Bug in checkout, high priority, assign Maya" and we parse it into a structured issue.', status: 'in_progress', priority: 'urgent', points: 5, assignee: 'u3', labels: ['ai','frontend'], created: 'Apr 26', updated: '1h', comments: 4 },
    { id: 'WEB-142', title: 'Column WIP limits with soft warning', desc: '', status: 'in_progress', priority: 'medium', points: 2, assignee: 'u5', labels: ['board'], created: 'Apr 29', updated: '5h', comments: 0 },

    // IN REVIEW
    { id: 'WEB-128', title: 'Sidebar redesign — workspace switcher', desc: 'New compact sidebar with workspace switcher at the top.', status: 'in_review', priority: 'medium', points: 5, assignee: 'u6', labels: ['design','frontend'], created: 'Apr 18', updated: '20m', comments: 11 },
    { id: 'WEB-133', title: 'Keyboard shortcuts: J/K to move between cards', desc: '', status: 'in_review', priority: 'low', points: 2, assignee: 'u4', labels: ['frontend','a11y'], created: 'Apr 22', updated: '2h', comments: 3 },

    // DONE
    { id: 'WEB-118', title: 'Auth: passkey sign-in', desc: '', status: 'done', priority: 'high', points: 8, assignee: 'u4', labels: ['auth','backend'], created: 'Apr 10', updated: '3d', comments: 6 },
    { id: 'WEB-122', title: 'Notification center skeleton', desc: '', status: 'done', priority: 'medium', points: 3, assignee: 'u2', labels: ['frontend'], created: 'Apr 14', updated: '5d', comments: 2 },
    { id: 'WEB-125', title: 'Brand tokens: type scale + spacing', desc: '', status: 'done', priority: 'low', points: 2, assignee: 'u6', labels: ['design','tokens'], created: 'Apr 16', updated: '6d', comments: 0 },
  ];

  return { members, projects, issues };
})();

window.STATUSES = [
  { id: 'backlog',     label: 'Backlog',     dot: '#A8B0A2' },
  { id: 'todo',        label: 'Todo',        dot: '#7B95B8' },
  { id: 'in_progress', label: 'In Progress', dot: '#D4A24C' },
  { id: 'in_review',   label: 'In Review',   dot: '#9E7BC1' },
  { id: 'done',        label: 'Done',        dot: '#6FAE5A' },
];

window.PRIORITIES = {
  urgent: { label: 'Urgent', color: '#C5523E', glyph: '↑↑' },
  high:   { label: 'High',   color: '#D4A24C', glyph: '↑'  },
  medium: { label: 'Medium', color: '#5A6B5E', glyph: '=' },
  low:    { label: 'Low',    color: '#8A9489', glyph: '↓'  },
};
