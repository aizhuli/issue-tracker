"use client";

import { useState } from "react";
import { KanbanBoard } from "@/components/kanban/KanbanBoard";
import { MOCK_ISSUES } from "@/lib/mock-issues";
import { useDemoAnimation } from "./useDemoAnimation";

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
      <KanbanBoard issues={issues} />
    </div>
  );
}
