import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { IssueDetail } from "@/components/issues/IssueDetail";
import type { IssueFull } from "@/lib/types/issues";

vi.mock("react-dom", async () => {
  const actual = await vi.importActual<typeof import("react-dom")>("react-dom");
  return { ...actual, createPortal: (node: React.ReactNode) => node };
});

vi.mock("react-markdown", () => ({
  default: ({ children }: { children: string }) => <p data-testid="markdown">{children}</p>,
}));

vi.mock("remark-gfm", () => ({ default: () => {} }));

// AssigneePicker and LabelPicker make their own fetch calls — stub them out
vi.mock("@/components/issues/AssigneePicker", () => ({
  AssigneePicker: ({ value }: { value: unknown }) => (
    <div data-testid="assignee-picker">{value ? (value as { name: string }).name : "Unassigned"}</div>
  ),
}));

vi.mock("@/components/issues/LabelPicker", () => ({
  LabelPicker: () => <div data-testid="label-picker" />,
}));

vi.mock("@/components/issues/CommentsSection", () => ({
  CommentsSection: () => <div data-testid="comments-section" />,
}));

vi.mock("@/components/issues/DeleteIssueDialog", () => ({
  DeleteIssueDialog: ({ open }: { open: boolean }) =>
    open ? <div data-testid="delete-dialog" /> : null,
}));

const reporterUser = {
  id: "user-reporter",
  name: "Alice",
  email: "alice@example.com",
};

const otherUser = {
  id: "user-other",
  name: "Bob",
  email: "bob@example.com",
};

const baseIssue: IssueFull = {
  id: "issue-1",
  number: 1,
  displayKey: "PROJ-1",
  title: "Fix login bug",
  description: "Some **markdown** description",
  status: "todo",
  priority: "medium",
  assignee: null,
  reporter: reporterUser,
  labels: [],
  acceptanceCriteria: null,
  acceptanceCriteriaAiSuggested: false,
  commentCount: 0,
  createdAt: "2024-01-01T00:00:00Z",
  updatedAt: "2024-01-02T00:00:00Z",
  closedAt: null,
};

function renderDetail(overrides: Partial<Parameters<typeof IssueDetail>[0]> = {}) {
  const defaults = {
    issue: baseIssue,
    me: { id: "user-reporter", name: "Alice", email: "alice@example.com" },
    projectOwnerId: "owner-1",
    projectSlug: "my-project",
    onChange: vi.fn(),
    onDeleted: vi.fn(),
  };
  return render(<IssueDetail {...defaults} {...overrides} />);
}

describe("IssueDetail — read mode", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("renders issue title", () => {
    renderDetail();
    expect(screen.getByText("Fix login bug")).toBeInTheDocument();
  });

  it("renders description via ReactMarkdown", () => {
    renderDetail();
    expect(screen.getByTestId("markdown")).toBeInTheDocument();
    expect(screen.getByTestId("markdown").textContent).toBe("Some **markdown** description");
  });

  it("does not show AI-suggested badge when flag is false", () => {
    renderDetail();
    expect(screen.queryByText(/AI-suggested/i)).toBeNull();
  });

  it("shows AI-suggested badge when acceptanceCriteriaAiSuggested is true", () => {
    renderDetail({
      issue: {
        ...baseIssue,
        acceptanceCriteria: "- [ ] criteria",
        acceptanceCriteriaAiSuggested: true,
      },
    });
    expect(screen.getByText(/AI-suggested/i)).toBeInTheDocument();
  });

  it("shows Delete button when me is reporter", () => {
    renderDetail({
      me: { id: "user-reporter", name: "Alice", email: "alice@example.com" },
    });
    expect(screen.getByRole("button", { name: /delete/i })).toBeInTheDocument();
  });

  it("shows Delete button when me is project owner", () => {
    renderDetail({
      me: { id: "owner-1", name: "Owner", email: "owner@example.com" },
      projectOwnerId: "owner-1",
    });
    expect(screen.getByRole("button", { name: /delete/i })).toBeInTheDocument();
  });

  it("hides Delete button for non-reporter non-owner", () => {
    renderDetail({
      me: { id: "some-other-user", name: "Other", email: "other@example.com" },
      projectOwnerId: "owner-1",
    });
    expect(screen.queryByRole("button", { name: /delete/i })).toBeNull();
  });

  it("Edit button switches to edit mode", () => {
    renderDetail();
    fireEvent.click(screen.getByRole("button", { name: /^edit$/i }));
    expect(screen.getByText("Edit issue")).toBeInTheDocument();
  });
});

describe("IssueDetail — edit mode", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  function renderInEditMode(overrides: Partial<Parameters<typeof IssueDetail>[0]> = {}) {
    const utils = renderDetail(overrides);
    fireEvent.click(screen.getByRole("button", { name: /^edit$/i }));
    return utils;
  }

  it("Cancel button exits edit mode", () => {
    renderInEditMode();
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(screen.queryByText("Edit issue")).toBeNull();
    expect(screen.getByText("Fix login bug")).toBeInTheDocument();
  });

  it("Save calls PUT with full payload", async () => {
    const updatedIssue = { ...baseIssue, title: "Fix login bug" };
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(updatedIssue),
    });
    vi.stubGlobal("fetch", fetchMock);

    const onChange = vi.fn();
    renderInEditMode({ onChange });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /^save$/i }));
      await vi.runAllTimersAsync();
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/projects/my-project/issues/1",
      expect.objectContaining({
        method: "PUT",
        headers: expect.objectContaining({ "Content-Type": "application/json" }),
      })
    );
  });

  it("Save exits edit mode on success and calls onChange", async () => {
    const updatedIssue = { ...baseIssue, title: "Fix login bug" };
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(updatedIssue),
      })
    );

    const onChange = vi.fn();
    renderInEditMode({ onChange });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /^save$/i }));
      await vi.runAllTimersAsync();
    });

    expect(onChange).toHaveBeenCalledWith(updatedIssue);
    expect(screen.queryByText("Edit issue")).toBeNull();
  });

  it("4xx response surfaces inline field error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 422,
        json: () =>
          Promise.resolve({
            errorCode: "issues:issue:title:required_or_too_long",
            detail: "Title is required",
          }),
      })
    );

    renderInEditMode();

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /^save$/i }));
      await vi.runAllTimersAsync();
    });

    expect(screen.getByText("Title is required")).toBeInTheDocument();
  });

  it("Save button is disabled when title is empty", () => {
    renderInEditMode();
    const titleInput = screen.getByLabelText(/title/i);
    fireEvent.change(titleInput, { target: { value: "" } });
    expect(screen.getByRole("button", { name: /^save$/i })).toBeDisabled();
  });
});
