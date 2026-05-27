import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { IssueCard } from "@/components/issues/IssueCard";
import type { IssueSummary, IssueStatus } from "@/lib/types/issues";

const baseIssue: IssueSummary = {
  id: "issue-1",
  number: 1,
  displayKey: "PROJ-1",
  title: "Fix login bug",
  status: "todo",
  priority: "medium",
  assignee: null,
  labels: [],
  commentCount: 0,
  updatedAt: "2024-01-01T00:00:00Z",
};

describe("IssueCard", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("renders the displayKey in .issue-card__key", () => {
    render(
      <IssueCard issue={baseIssue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    const key = document.querySelector(".issue-card__key");
    expect(key).not.toBeNull();
    expect(key!.textContent).toBe("PROJ-1");
  });

  it("renders priority glyph with correct class", () => {
    render(
      <IssueCard issue={baseIssue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    const priorityIcon = document.querySelector(".priority-icon--medium");
    expect(priorityIcon).not.toBeNull();
  });

  it("renders comment count when > 0", () => {
    const issue = { ...baseIssue, commentCount: 5 };
    render(
      <IssueCard issue={issue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    expect(screen.getByText("💬 5")).toBeInTheDocument();
  });

  it("does not render comment count when 0", () => {
    render(
      <IssueCard issue={baseIssue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    expect(screen.queryByText(/💬/)).toBeNull();
  });

  it("shows overflow chip +2 when there are 4 labels (max 2 visible)", () => {
    const issue = {
      ...baseIssue,
      labels: [
        { id: "l1", name: "bug", color: "#ff0000" },
        { id: "l2", name: "feat", color: "#00ff00" },
        { id: "l3", name: "ui", color: "#0000ff" },
        { id: "l4", name: "api", color: "#ffff00" },
      ],
    };
    render(
      <IssueCard issue={issue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    const overflow = document.querySelector(".label-chip--overflow");
    expect(overflow).not.toBeNull();
    expect(overflow!.textContent).toBe("+2");
  });

  it("does not show overflow chip when labels <= 2", () => {
    const issue = {
      ...baseIssue,
      labels: [
        { id: "l1", name: "bug", color: "#ff0000" },
        { id: "l2", name: "feat", color: "#00ff00" },
      ],
    };
    render(
      <IssueCard issue={issue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    const overflow = document.querySelector(".label-chip--overflow");
    expect(overflow).toBeNull();
  });

  it("caret button has aria-label Change status", () => {
    render(
      <IssueCard issue={baseIssue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    expect(screen.getByRole("button", { name: /change status/i })).toBeInTheDocument();
  });

  it("caret click opens status menu with all statuses", () => {
    render(
      <IssueCard issue={baseIssue} onOpen={vi.fn()} onStatusChange={vi.fn()} />
    );
    const caretBtn = screen.getByRole("button", { name: /change status/i });
    fireEvent.click(caretBtn);
    expect(screen.getByText("Backlog")).toBeInTheDocument();
    expect(screen.getByText("Todo")).toBeInTheDocument();
    expect(screen.getByText("In Progress")).toBeInTheDocument();
    expect(screen.getByText("In Review")).toBeInTheDocument();
    expect(screen.getByText("Done")).toBeInTheDocument();
  });

  it("selecting a status from the menu invokes onStatusChange with that status", () => {
    const onStatusChange = vi.fn();
    render(
      <IssueCard issue={baseIssue} onOpen={vi.fn()} onStatusChange={onStatusChange} />
    );
    const caretBtn = screen.getByRole("button", { name: /change status/i });
    fireEvent.click(caretBtn);
    fireEvent.click(screen.getByText("In Progress"));
    expect(onStatusChange).toHaveBeenCalledWith("in-progress");
  });

  it("card click calls onOpen", () => {
    const onOpen = vi.fn();
    render(
      <IssueCard issue={baseIssue} onOpen={onOpen} onStatusChange={vi.fn()} />
    );
    const card = document.querySelector(".issue-card")!;
    fireEvent.click(card);
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it("caret click does not propagate to card (does not call onOpen)", () => {
    const onOpen = vi.fn();
    render(
      <IssueCard issue={baseIssue} onOpen={onOpen} onStatusChange={vi.fn()} />
    );
    const caretBtn = screen.getByRole("button", { name: /change status/i });
    fireEvent.click(caretBtn);
    expect(onOpen).not.toHaveBeenCalled();
  });
});
