"use client";

import { Dispatch, SetStateAction, useEffect, useRef } from "react";

export type DemoIssueStatus =
  | "backlog"
  | "todo"
  | "in_progress"
  | "in_review"
  | "done";

export type DemoIssuePriority = "urgent" | "high" | "medium" | "low";

export interface DemoIssue {
  id: string;
  title: string;
  status: DemoIssueStatus;
  priority: DemoIssuePriority;
  points?: number;
  assignee?: string;
  labels?: string[];
  comments?: number;
  entering?: boolean;
}

const STATUS_ORDER: DemoIssueStatus[] = [
  "backlog",
  "todo",
  "in_progress",
  "in_review",
  "done",
];

const NON_DONE = STATUS_ORDER.filter((s) => s !== "done");

let _idCounter = 200;

export function createDemoIssue(status: DemoIssueStatus): DemoIssue {
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
  const assignees = ["u1", "u2", "u3", "u4", "u5", "u6"];
  return {
    id: `${prefix}-${++_idCounter}`,
    title: titles[Math.floor(Math.random() * titles.length)],
    status,
    priority: (["low", "medium", "high", "urgent"] as DemoIssuePriority[])[
      Math.floor(Math.random() * 4)
    ],
    points: [1, 2, 3, 5, 8][Math.floor(Math.random() * 5)],
    assignee: assignees[Math.floor(Math.random() * assignees.length)],
    labels: labelSets[Math.floor(Math.random() * labelSets.length)],
    comments: 0,
    entering: true,
  };
}

export function useDemoAnimation(
  _issues: DemoIssue[],
  setIssues: Dispatch<SetStateAction<DemoIssue[]>>,
) {
  const newCardCountRef = useRef(0);
  const timer2PausedRef = useRef(false);

  useEffect(() => {
    // Timer 1: advance a random non-done card every 3.5s.
    const t1 = setInterval(() => {
      setIssues((prev) => {
        const moveable = prev.filter((i) => i.status !== "done");
        if (moveable.length === 0) return prev;

        const target = moveable[Math.floor(Math.random() * moveable.length)];
        const idx = STATUS_ORDER.indexOf(target.status);
        const nextStatus = STATUS_ORDER[idx + 1];

        if (nextStatus === "done") {
          setTimeout(() => {
            setIssues((p) =>
              p.map((x) =>
                x.id === target.id ? { ...x, status: "backlog" as DemoIssueStatus } : x,
              ),
            );
          }, 2000);
        }

        return prev.map((i) =>
          i.id === target.id ? { ...i, status: nextStatus, entering: false } : i,
        );
      });
    }, 3500);

    // Timer 2: insert a new card every 6s; pause for 12s after 4 insertions.
    const t2 = setInterval(() => {
      if (timer2PausedRef.current) return;

      const randomNonDone = NON_DONE[Math.floor(Math.random() * NON_DONE.length)];
      const newCard = createDemoIssue(randomNonDone);

      setIssues((prev) => [...prev, newCard]);

      // Clear the entering flag after the CSS animation completes
      setTimeout(() => {
        setIssues((prev) =>
          prev.map((i) => (i.id === newCard.id ? { ...i, entering: false } : i)),
        );
      }, 300);

      newCardCountRef.current += 1;
      if (newCardCountRef.current >= 4) {
        newCardCountRef.current = 0;
        timer2PausedRef.current = true;
        setTimeout(() => {
          timer2PausedRef.current = false;
        }, 12000);
      }
    }, 6000);

    return () => {
      clearInterval(t1);
      clearInterval(t2);
    };
  }, [setIssues]);
}
