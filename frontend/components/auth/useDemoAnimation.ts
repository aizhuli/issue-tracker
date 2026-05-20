"use client";

import { Dispatch, SetStateAction, useEffect, useRef } from "react";
import { IssueStatus, MockIssue, createMockIssue } from "@/lib/mock-issues";

const STATUS_ORDER: IssueStatus[] = [
  "backlog",
  "todo",
  "in_progress",
  "in_review",
  "done",
];

const NON_DONE = STATUS_ORDER.filter((s) => s !== "done");

export function useDemoAnimation(
  _issues: MockIssue[],
  setIssues: Dispatch<SetStateAction<MockIssue[]>>,
) {
  const newCardCountRef = useRef(0);
  const timer2PausedRef = useRef(false);

  useEffect(() => {
    // Timer 1: advance a random non-done card every 3.5s.
    // Uses functional update so no stale closure on issues.
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
                x.id === target.id ? { ...x, status: "backlog" as IssueStatus } : x,
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
      const newCard = createMockIssue(randomNonDone);

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
