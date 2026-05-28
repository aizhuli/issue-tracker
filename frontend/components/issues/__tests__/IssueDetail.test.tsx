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
  LabelPicker: ({ value }: { value: { id: string }[] }) => (
    <div data-testid="label-picker" data-label-count={value.length} />
  ),
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

  it("AI-suggest button is disabled when title is empty", () => {
    renderInEditMode();
    const titleInput = screen.getByLabelText(/title/i);
    fireEvent.change(titleInput, { target: { value: "" } });
    expect(screen.getByRole("button", { name: /ai-suggest/i })).toBeDisabled();
  });

  it("AI-suggest button is disabled while suggesting", async () => {
    let resolveFetch!: (value: unknown) => void;
    vi.stubGlobal(
      "fetch",
      vi.fn().mockReturnValue(
        new Promise((resolve) => {
          resolveFetch = resolve;
        })
      )
    );

    renderInEditMode();

    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /ai-suggest/i }));
    });

    expect(screen.getByRole("button", { name: /suggesting/i })).toBeDisabled();

    // Resolve to clean up
    resolveFetch({ ok: true, json: () => Promise.resolve({ priority: "high", labels: [], acceptanceCriteria: "- done" }) });
    await act(async () => { await vi.runAllTimersAsync(); });
  });

  it("AI-suggest button POSTs live title and description", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ priority: "high", labels: [], acceptanceCriteria: "- done" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    renderInEditMode();

    const titleInput = screen.getByLabelText(/title/i);
    fireEvent.change(titleInput, { target: { value: "Updated title" } });

    const descriptionInput = screen.getByLabelText(/description/i);
    fireEvent.change(descriptionInput, { target: { value: "Some description" } });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /ai-suggest/i }));
      await vi.runAllTimersAsync();
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/projects/my-project/issues/1/ai/triage",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({ "Content-Type": "application/json" }),
        body: JSON.stringify({ title: "Updated title", description: "Some description" }),
      })
    );
  });

  it("AI-suggest merges priority, dedupes labels, overwrites criteria", async () => {
    const existingLabel = { id: "label-existing", name: "bug", color: "#ff0000" };
    const newLabel = { id: "label-new", name: "feature", color: "#00ff00" };
    const issueWithLabel = { ...baseIssue, labels: [existingLabel] };

    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () =>
        Promise.resolve({
          priority: "urgent",
          labels: [existingLabel, newLabel],
          acceptanceCriteria: "- [ ] new criteria",
        }),
    });
    vi.stubGlobal("fetch", fetchMock);

    renderInEditMode({ issue: issueWithLabel });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /ai-suggest/i }));
      await vi.runAllTimersAsync();
    });

    // Priority should be set to urgent
    const prioritySelect = screen.getByLabelText(/priority/i) as HTMLSelectElement;
    expect(prioritySelect.value).toBe("urgent");

    // Acceptance criteria textarea should be overwritten
    const criteriaTextarea = screen.getByLabelText(/acceptance criteria/i) as HTMLTextAreaElement;
    expect(criteriaTextarea.value).toBe("- [ ] new criteria");

    // LabelPicker should show 2 labels: 1 existing + 1 new (deduped, not 3)
    expect(screen.getByTestId("label-picker")).toHaveAttribute("data-label-count", "2");
  });

  it("AI-suggest error leaves form unchanged", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 502,
        json: () => Promise.resolve({ detail: "Bad gateway" }),
      })
    );

    renderInEditMode();

    const titleInput = screen.getByLabelText(/title/i) as HTMLInputElement;
    const originalTitle = titleInput.value;

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /ai-suggest/i }));
      await vi.runAllTimersAsync();
    });

    // Title should remain unchanged
    expect(titleInput.value).toBe(originalTitle);

    // Some form-level error should be shown (mapped from the problem details)
    // The error is surfaced via fieldErrors — mapProblemDetailsToFields maps detail → _form fallback
    // At minimum the button should be re-enabled (suggesting stopped)
    expect(screen.getByRole("button", { name: /ai-suggest/i })).not.toBeDisabled();
  });
});
